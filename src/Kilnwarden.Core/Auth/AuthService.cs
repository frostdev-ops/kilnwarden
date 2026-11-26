// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json.Nodes;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Kap;
using Kilnwarden.Modules;

namespace Kilnwarden.Auth;

/// <summary>Who is asking: the client address (after the trusted-proxy rules) and its User-Agent (for the session list).</summary>
public sealed record ClientInfo(IPAddress Ip, string UserAgent)
{
    /// <summary>The request came through the relay (hosted access), not straight to this machine.</summary>
    public bool Relayed { get; init; }
}

/// <summary>What a sign-in step ended with.</summary>
public abstract record SignInResult
{
    /// <summary>Signed in: <see cref="Token"/> goes in the session cookie.</summary>
    public sealed record Success(string Token, SessionRecord Session, Account Account) : SignInResult;

    /// <summary>The password was right and the account has two-factor: <see cref="Ticket"/> (a short-lived cookie) and a
    /// code finish the sign-in.</summary>
    public sealed record TwoFactorNeeded(string Ticket) : SignInResult;

    /// <summary>The launcher's one-time login before any owner exists: <see cref="SetupToken"/> creates the owner.</summary>
    public sealed record SetupGranted(string SetupToken) : SignInResult;

    public sealed record Refused(int Status, string Code, string Message, TimeSpan? RetryAfter = null) : SignInResult;
}

/// <summary>Accounts, sign-in and the account's own settings (DESIGN §3): local passwords, TOTP two-factor and
/// recovery codes, the first-run setup link, the launcher's one-time login, and the checks behind every session.
/// Uniform answers: a wrong name, a wrong password and a disabled account all get the same message after the same
/// amount of hashing, and lockouts count unknown names like real ones.</summary>
public sealed class AuthService
{
    public const int MaxPasswordLength = 256;
    public static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan LauncherSetupLifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan PendingTotpLifetime = TimeSpan.FromMinutes(15);
    public const int TicketAttempts = 5;
    public static readonly TimeSpan ReauthLifetime = TimeSpan.FromMinutes(10);
    public DateTimeOffset Now => _time.GetUtcNow();

    private readonly Func<AuthSettings> _settings;
    private readonly TimeProvider _time;
    private readonly IWebAdminLog _log;
    private readonly ConcurrentDictionary<string, Ticket> _tickets = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SetupGrant> _setupTokens = new(StringComparer.Ordinal);

    /// <summary>Where a setup token may create the owner (0.1.2). <see cref="SetupScope.ThisMachine"/>: kiln.log's link,
    /// the console's <c>kilnwarden setup</c> on a server without hosted access, the launcher's; straight to this machine
    /// only (a reverse proxy counts), never through the relay unless settings.json remoteSetup is on (as before 0.1.2).
    /// <see cref="SetupScope.Hosted"/>: the console's link for hosted access, made only once the panel's name has its
    /// trusted certificate, good for 30 minutes; never written to kiln.log.</summary>
    public enum SetupScope { ThisMachine, Hosted }

    private sealed record SetupGrant(DateTimeOffset Until, SetupScope Scope);

    /// <summary>settings.json remoteSetup: tokens of <see cref="SetupScope.ThisMachine"/> also work through the relay
    /// (the auth component sets it).</summary>
    internal Func<bool> RemoteSetupOn { get; set; } = () => false;
    private readonly object _setupLock = new();

    public AuthService(AccountStore accounts, RoleStore roles, SessionStore sessions, PasswordHasher hasher, SignInLimiter limiter,
        Func<AuthSettings> settings, AuditLog audit, ModuleRegistry registry, IWebAdminLog log, KapClient? kap = null, TimeProvider? time = null,
        SignInProviders? providers = null)
    {
        Providers = providers ?? new SignInProviders();
        Accounts = accounts;
        Roles = roles;
        Sessions = sessions;
        Hasher = hasher;
        Limiter = limiter;
        Audit = audit;
        Registry = registry;
        Kap = kap;
        _settings = settings;
        _log = log;
        _time = time ?? TimeProvider.System;
    }

    public AccountStore Accounts { get; }
    public RoleStore Roles { get; }
    public SessionStore Sessions { get; }
    public PasswordHasher Hasher { get; }
    public SignInLimiter Limiter { get; }
    public AuditLog Audit { get; }
    public ModuleRegistry Registry { get; }
    public KapClient? Kap { get; }
    public SignInProviders Providers { get; }
    public AuthSettings Settings => _settings();

    // ------------------------------------------------------------------------------------------------ sessions

    /// <summary>The user behind a live session, as of now; null when the account is gone, disabled, or changed its
    /// password or two-factor since (the session is then revoked).</summary>
    /// <summary>The roles an account has from provider mappings that are still configured (set by the identity
    /// component; null: none). Never the owner role.</summary>
    public Func<Account, IEnumerable<string>>? MappedRoles { get; set; }

    /// <summary>Records the roles a provider's mapping gives <paramref name="accountId"/> now (an empty list clears
    /// them); bumps the privilege stamp when they changed, so sessions rotate. Audited.</summary>
    public void SyncRoles(string accountId, string provider, IReadOnlyList<string> roles, IPAddress? ip)
    {
        var clean = roles.Where(r => r != BuiltinRoles.Owner && RoleNames.IsValid(r)).Distinct().Order(StringComparer.Ordinal).ToList();
        var changed = false;
        Accounts.Update(accountId, x =>
        {
            var had = x.Synced.TryGetValue(provider, out var h) ? h : [];
            if (had.SequenceEqual(clean)) return x;
            changed = true;
            var next = new Dictionary<string, IReadOnlyList<string>>(x.Synced);
            if (clean.Count == 0) next.Remove(provider);
            else next[provider] = clean;
            return x with { Synced = next, PrivilegeStamp = x.PrivilegeStamp + 1 };
        });
        if (changed)
            Audit.Record(AuditLog.Entry("auth.roles_synced", null, ip, true, args: new JsonObject
            {
                ["provider"] = provider, ["roles"] = new JsonArray([.. clean.Select(r => (JsonNode)r)]),
            }) with { User = accountId });
    }

    public WebPrincipal? Resolve(SessionRecord? rec)
    {
        if (rec is null) return null;
        var a = Accounts.Find(rec.AccountId);
        if (a is null || a.Disabled || a.CredentialStamp != rec.CredentialStamp)
        {
            Sessions.Revoke(rec.Handle);
            return null;
        }
        var ids = a.Roles.ToList();
        if (MappedRoles is { } mapped && a.Synced.Count > 0)
            foreach (var r in mapped(a)) if (r != BuiltinRoles.Owner && !ids.Contains(r)) ids.Add(r);
        var roles = ids.Select(Roles.Find).OfType<RoleDef>().ToList();
        return new WebPrincipal(a, roles, rec, Registry);
    }

    /// <summary>The user behind a session handle (WebSockets re-check theirs at every message).</summary>
    public WebPrincipal? ResolveHandle(string? handle) => Resolve(Sessions.FindByHandle(handle));

    private SignInResult StartSession(Account a, string via, bool mfa, ClientInfo c, string? replacingHandle)
    {
        if (Accounts.Find(a.Id) is not { Disabled: false } current || current.CredentialStamp != a.CredentialStamp)
            return new SignInResult.Refused(401, "invalid_credentials", "invalid credentials");
        if (replacingHandle is not null) Sessions.Revoke(replacingHandle); // a new id at every sign-in (no fixation)
        var (token, rec) = Sessions.Create(a, via, mfa, c.Ip.ToString(), c.UserAgent);
        var now = _time.GetUtcNow();
        var updated = Accounts.Update(a.Id, x => x with { LastSignIn = now });
        if (Accounts.Find(a.Id) is not { Disabled: false } latest || latest.CredentialStamp != rec.CredentialStamp)
        {
            Sessions.Revoke(rec.Handle);
            return new SignInResult.Refused(401, "invalid_credentials", "invalid credentials");
        }
        Audit.Record(AuditLog.Entry("auth.login", null, c.Ip, true, args: new JsonObject { ["via"] = via, ["session"] = rec.Handle }, userName: a.Name) with { User = a.Id });
        return new SignInResult.Success(token, rec, updated);
    }

    // ------------------------------------------------------------------------------------------------ sign-in

    /// <summary>Name and password. The same answer and the same hashing work for a wrong name, a wrong password and a
    /// disabled account.</summary>
    public async Task<SignInResult> SignInAsync(string? name, string? password, ClientInfo c, string? currentHandle, CancellationToken ct)
    {
        name = name?.Trim() ?? "";
        password ??= "";
        var key = NameKey(name);
        if (Limiter.Attempt(c.Ip, key) is { } wait) return RateLimited("auth.login", c, wait, name);
        if (password.Length is 0 or > MaxPasswordLength)
        {
            await Hasher.BurnAsync("x", ct).ConfigureAwait(false);
            return Wrong(c, key, name);
        }
        var a = Accounts.FindByName(name);
        var (ok, rehash) = await Hasher.VerifyAsync(password, a is { PasswordHash.Length: > 0 } ? a.PasswordHash : null, ct).ConfigureAwait(false);
        if (!ok || a is null || a.Disabled) return Wrong(c, key, name);
        if (Accounts.Find(a.Id)?.CredentialStamp != a.CredentialStamp) return Wrong(c, key, name);
        var verifiedStamp = a.CredentialStamp;
        if (rehash)
        {
            var fresh = await Hasher.HashAsync(password, ct).ConfigureAwait(false);
            a = Accounts.Update(a.Id, x => x.PasswordHash == a.PasswordHash ? x with { PasswordHash = fresh } : x);
            if (a.CredentialStamp != verifiedStamp) return Wrong(c, key, name);
        }
        if (a.TwoFactor)
        {
            var ticket = SecretToken.New();
            PurgeTickets();
            _tickets[SecretToken.Hash(ticket)] = new Ticket(a.Id, a.CredentialStamp, _time.GetUtcNow() + TicketLifetime);
            return new SignInResult.TwoFactorNeeded(ticket);
        }
        Limiter.Succeed(key);
        return StartSession(a, "password", false, c, currentHandle);
    }

    /// <summary>The second step: a TOTP code or a recovery code, with the ticket from <see cref="SignInAsync"/>.</summary>
    public SignInResult CompleteTwoFactor(string? ticket, string? code, string? recoveryCode, ClientInfo c, string? currentHandle)
    {
        if (!SecretToken.IsWellFormed(ticket) || !_tickets.TryGetValue(SecretToken.Hash(ticket!), out var t) || t.Expires <= _time.GetUtcNow())
            return new SignInResult.Refused(401, "2fa_expired", "the sign-in expired: enter your name and password again");
        var hash = SecretToken.Hash(ticket!);
        var key = AccountKey(t.AccountId);
        if (Limiter.Attempt(c.Ip, key) is { } wait) return RateLimited("auth.2fa", c, wait, null, t.AccountId);
        var a = Accounts.Find(t.AccountId);
        if (a is null || a.Disabled || a.CredentialStamp != t.Stamp || a.TotpSecret is null)
        {
            _tickets.TryRemove(hash, out _);
            return new SignInResult.Refused(401, "2fa_expired", "the sign-in expired: enter your name and password again");
        }
        string? via = null;
        if (code is not null && Base32.Decode(a.TotpSecret) is { } secret &&
            Totp.Verify(secret, code, _time.GetUtcNow(), a.TotpLastStep) is { } step)
        {
            via = TryUse(a.Id, x => x.TotpLastStep < step && x.TotpSecret == a.TotpSecret ? x with { TotpLastStep = step } : null) ? "password+2fa" : null;
        }
        else if (code is null && recoveryCode is not null && RecoveryCodes.Match(a.RecoveryCodes, recoveryCode) is var i and >= 0)
        {
            var used = a.RecoveryCodes[i];
            via = TryUse(a.Id, x => x.RecoveryCodes.Contains(used) ? x with { RecoveryCodes = [.. x.RecoveryCodes.Where(h => h != used)] } : null) ? "recovery-code" : null;
        }
        if (via is null)
        {
            Limiter.Fail(c.Ip, key);
            if (Interlocked.Increment(ref t.Attempts) >= TicketAttempts) _tickets.TryRemove(hash, out _);
            Audit.Record(AuditLog.Entry("auth.2fa", null, c.Ip, false, "wrong code", userName: a.Name) with { User = a.Id });
            return new SignInResult.Refused(401, "invalid_code", "wrong code");
        }
        _tickets.TryRemove(hash, out _);
        Limiter.Succeed(key);
        if (via == "recovery-code")
            _log.Warn($"account '{a.Name}' signed in with a recovery code ({Accounts.Find(a.Id)?.RecoveryCodes.Count ?? 0} left)");
        return StartSession(a, via, true, c, currentHandle);
    }

    /// <summary>Applies a one-time use to the account; false when someone used it first (a race or a replay).</summary>
    private bool TryUse(string id, Func<Account, Account?> use)
    {
        var used = false;
        try
        {
            Accounts.Update(id, x =>
            {
                var next = use(x);
                used = next is not null;
                return next ?? x;
            });
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
        return used;
    }

    private void PurgeTickets()
    {
        var now = _time.GetUtcNow();
        foreach (var (k, t) in _tickets)
            if (t.Expires <= now) _tickets.TryRemove(k, out _);
        // a flood of password-right sign-ins cannot grow this without bound
        if (_tickets.Count > 1000)
            foreach (var k in _tickets.OrderBy(kv => kv.Value.Expires).Take(_tickets.Count - 1000).Select(kv => kv.Key).ToList())
                _tickets.TryRemove(k, out _);
    }

    private sealed class Ticket(string accountId, long stamp, DateTimeOffset expires)
    {
        public readonly string AccountId = accountId;
        public readonly long Stamp = stamp;
        public readonly DateTimeOffset Expires = expires;
        public int Attempts;
    }

    // ------------------------------------------------------------------------------------------------ first run

    public bool OwnerExists => Accounts.OwnerExists;

    /// <summary>A setup token (the link in kiln.log, or one the launcher's login gets while no owner exists). Only its
    /// hash is kept; every token stops working once the owner exists.</summary>
    public string NewSetupToken(TimeSpan? lifetime = null, SetupScope scope = SetupScope.ThisMachine)
    {
        if (OwnerExists) throw new InvalidOperationException("the owner exists");
        var t = SecretToken.New();
        _setupTokens[SecretToken.Hash(t)] = new SetupGrant(lifetime is { } l ? _time.GetUtcNow() + l : DateTimeOffset.MaxValue, scope);
        return t;
    }

    /// <summary>Creates the owner account from a setup token (DESIGN §3: no default password, ever).</summary>
    public async Task<SignInResult> SetupOwnerAsync(string? token, string? name, string? password, ClientInfo c, CancellationToken ct)
    {
        if (Limiter.Attempt(c.Ip, null) is { } wait) return RateLimited("auth.setup", c, wait, null);
        if (OwnerExists) return new SignInResult.Refused(409, "owner_exists", "the owner account exists already: sign in");
        if (!SecretToken.IsWellFormed(token) || !_setupTokens.TryGetValue(SecretToken.Hash(token!), out var grant) || grant.Until <= _time.GetUtcNow())
        {
            Limiter.Fail(c.Ip, null);
            Audit.Record(AuditLog.Entry("auth.setup", null, c.Ip, false, "wrong setup token"));
            return new SignInResult.Refused(401, "invalid_setup_token", "the setup link is wrong or was used");
        }
        // kiln.log's link (and the launcher's) creates the owner on this machine only: a log pasted somewhere never opens
        // the panel to whoever reads it, even while hosted access is up for the console's setup (0.1.2)
        if (grant.Scope == SetupScope.ThisMachine && c.Relayed && !RemoteSetupOn())
        {
            Limiter.Fail(c.Ip, null);
            Audit.Record(AuditLog.Entry("auth.setup", null, c.Ip, false, "this machine's setup link used through the relay"));
            return new SignInResult.Refused(403, "setup_link_this_machine",
                "this setup link works on the server's own computer only. To set up over the internet, run kilnwarden setup in the server console");
        }
        name = name?.Trim() ?? "";
        if (!AccountNames.IsValid(name)) return new SignInResult.Refused(422, "invalid", "a name is 3 to 32 of A-Z a-z 0-9 . _ - (starting with a letter or digit)");
        if (PasswordProblem(name, password) is { } problem) return new SignInResult.Refused(422, "weak_password", problem);
        var hash = await Hasher.HashAsync(password!, ct).ConfigureAwait(false);
        Account owner;
        lock (_setupLock)
        {
            if (OwnerExists) return new SignInResult.Refused(409, "owner_exists", "the owner account exists already: sign in");
            // Password work can queue: expiry must still hold when the grant is consumed, not just before hashing.
            if (!_setupTokens.TryRemove(SecretToken.Hash(token!), out var consumed) || consumed.Until <= _time.GetUtcNow())
                return new SignInResult.Refused(401, "invalid_setup_token", "the setup link is wrong or was used");
            owner = Accounts.FindByName(name) is { } existing
                ? Accounts.Update(existing.Id, x => x with { PasswordHash = hash, Roles = [BuiltinRoles.Owner], Disabled = false, Primary = true, CredentialStamp = x.CredentialStamp + 1, PrivilegeStamp = x.PrivilegeStamp + 1 })
                : Accounts.Add(new Account { Name = name, PasswordHash = hash, Roles = [BuiltinRoles.Owner], Primary = true, CreatedBy = "setup" });
            _setupTokens.Clear();
        }
        _log.Info($"{Product.Name}: owner account '{owner.Name}' created from {c.Ip}; the setup link no longer works");
        Audit.Record(AuditLog.Entry("auth.setup", null, c.Ip, true, userName: owner.Name) with { User = owner.Id });
        return StartSession(owner, "setup", false, c, null);
    }

    // ------------------------------------------------------------------------------------------------ launcher

    /// <summary>The launcher's "Open web panel" (admin-protocol.md §11): the launcher holds the admin token, so it is the
    /// owner; it mints a single-use secret for this mod (consumer = the mod id) and opens the panel with it, and this
    /// redeems it over KAP. Signs in as the primary owner (two-factor counts as done: the admin token is a stronger
    /// proof than a code), or, before any owner exists, grants a setup token.</summary>
    public async Task<SignInResult> LauncherAsync(string? secret, ClientInfo c, string? currentHandle, CancellationToken ct)
    {
        if (!Settings.LauncherLogin) return new SignInResult.Refused(403, "launcher_login_off", "the launcher's one-time login is switched off (auth.json launcherLogin)");
        if (Limiter.Attempt(c.Ip, null) is { } wait) return RateLimited("auth.launcher", c, wait, null);
        if (secret is not { Length: 64 } || !secret.All(char.IsAsciiHexDigitLower))
        {
            Limiter.Fail(c.Ip, null);
            Audit.Record(AuditLog.Entry("auth.launcher", null, c.Ip, false, "malformed secret"));
            return new SignInResult.Refused(401, "invalid_login", "the login link is wrong, used or expired");
        }
        if (Kap is null || !Kap.IsReady) return new SignInResult.Refused(503, "kiln_admin_unavailable", "Kiln.Admin is not connected: try again in a moment");
        var r = await Kap.CallAsync(KapCaller.Panel, "login_redeem", new JsonObject { ["consumer"] = Product.ModId, ["secret"] = secret }, ct).ConfigureAwait(false);
        // Kiln launchers released before the rename mint for the old mod id (kiln-webadmin): a secret refused for this
        // id is tried once for that one. Kiln.Admin still checks it (single use, constant time, 60 s).
        if (!r.Ok && r.Code is not ("shutting_down" or "timeout" or "busy" or "unknown_op") && Product.ModId != Hosting.LegacyInstall.OldModId)
            r = await Kap.CallAsync(KapCaller.Panel, "login_redeem", new JsonObject { ["consumer"] = Hosting.LegacyInstall.OldModId, ["secret"] = secret }, ct).ConfigureAwait(false);
        if (!r.Ok)
        {
            if (r.Code is "shutting_down" or "timeout" or "busy" or "unknown_op")
                return new SignInResult.Refused(503, "kiln_admin_unavailable", $"Kiln.Admin could not check the login ({r.Code})");
            Limiter.Fail(c.Ip, null);
            Audit.Record(AuditLog.Entry("auth.launcher", null, c.Ip, false, "refused by Kiln.Admin"));
            return new SignInResult.Refused(401, "invalid_login", "the login link is wrong, used or expired");
        }
        var role = r.Data is JsonObject d ? KapFrames.Str(d, "role") : null;
        var mintedBy = r.Data is JsonObject d2 ? KapFrames.Str(d2, "mintedBy") ?? "?" : "?";
        if (role != "owner") return new SignInResult.Refused(401, "invalid_login", "the login link is wrong, used or expired");
        if (!OwnerExists)
        {
            Audit.Record(AuditLog.Entry("auth.launcher", null, c.Ip, true, args: new JsonObject { ["mintedBy"] = mintedBy, ["result"] = "setup" }));
            return new SignInResult.SetupGranted(NewSetupToken(LauncherSetupLifetime));
        }
        var owner = Accounts.PrimaryOwner!;
        _log.Info($"{Product.Name}: one-time login from the launcher ({mintedBy}) as '{owner.Name}' from {c.Ip}");
        return StartSession(owner, "launcher", true, c, currentHandle);
    }

    // ------------------------------------------------------------------------------------------------ external sign-in (step 2's seam)

    /// <summary>Signs in the account linked to a verified external identity (a registered provider's). The same answer
    /// for "not linked" and "disabled". It is not a second factor: a role that requires two-factor still restricts the
    /// session until the user signs in with password and code.</summary>
    public SignInResult SignInExternal(ExternalIdentity id, ClientInfo c, string? currentHandle)
    {
        if (!Providers.Has(id.Provider) || !SignInProviders.IsSubject(id.Subject))
            return new SignInResult.Refused(400, "unknown_provider", "that sign-in method is not available");
        var key = "ext:" + id.Provider + ":" + id.Subject;
        if (Limiter.Attempt(c.Ip, key) is { } wait) return RateLimited("auth.login", c, wait, null);
        var matches = Accounts.All.Where(x => x.Links.TryGetValue(id.Provider, out var subject) && subject == id.Subject).Take(2).ToList();
        if (matches.Count > 1)
        {
            // never pick one of two (security review S7): the owner unlinks the wrong one
            Audit.Record(AuditLog.Entry("auth.login", null, c.Ip, false, $"this {id.Provider} sign-in is linked to more than one account"));
            return new SignInResult.Refused(409, "linked_twice", $"this {id.Provider} account is linked to more than one panel account: ask an owner to unlink it from the wrong one");
        }
        var a = matches.FirstOrDefault();
        if (a is null || a.Disabled)
        {
            Limiter.Fail(c.Ip, key);
            Audit.Record(AuditLog.Entry("auth.login", null, c.Ip, false, $"no account linked to this {id.Provider} sign-in", userName: a?.Name ?? "")
                with { User = a?.Id ?? "" });
            return new SignInResult.Refused(401, "not_linked", $"no account is linked to this {id.Provider} sign-in");
        }
        Limiter.Succeed(key);
        return StartSession(a, id.Provider, false, c, currentHandle);
    }

    /// <summary>Links a verified external identity to the signed-in user's account (one account per identity).</summary>
    public SignInResult.Refused? Link(WebPrincipal p, ExternalIdentity id, ClientInfo c)
    {
        if (!Providers.Has(id.Provider) || !SignInProviders.IsSubject(id.Subject))
            return new SignInResult.Refused(400, "unknown_provider", "that sign-in method is not available");
        try
        {
            Accounts.LinkIfFree(p.Id, id.Provider, id.Subject);
        }
        catch (InvalidOperationException)
        {
            return new SignInResult.Refused(409, "linked_elsewhere", $"that {id.Provider} account is linked to another panel account");
        }
        Audit.Record(AuditLog.Entry("auth.link", p, c.Ip, true, args: new JsonObject { ["provider"] = id.Provider }));
        return null;
    }

    public bool Unlink(WebPrincipal p, string provider, ClientInfo c)
    {
        if (!p.Account.Links.ContainsKey(provider)) return false;
        Accounts.Update(p.Id, x => x with { Links = x.Links.Where(kv => kv.Key != provider).ToDictionary(kv => kv.Key, kv => kv.Value) });
        Audit.Record(AuditLog.Entry("auth.unlink", p, c.Ip, true, args: new JsonObject { ["provider"] = provider }));
        return true;
    }

    // ------------------------------------------------------------------------------------------------ own account

    /// <summary>Checks the signed-in user's password again (changing the password or two-factor); rate-limited like a
    /// sign-in. Null when right, else the refusal.</summary>
    public async Task<SignInResult.Refused?> ConfirmPasswordAsync(WebPrincipal p, string? password, ClientInfo c, CancellationToken ct)
    {
        var key = AccountKey(p.Id);
        if (Limiter.Attempt(c.Ip, key) is { } wait) return RateLimited("auth.confirm", c, wait, p.Name, p.Id);
        var a = Accounts.Find(p.Id);
        var (ok, _) = await Hasher.VerifyAsync(password is { Length: > 0 and <= MaxPasswordLength } ? password : "x",
            a is { PasswordHash.Length: > 0 } && password is { Length: > 0 and <= MaxPasswordLength } ? a.PasswordHash : null, ct).ConfigureAwait(false);
        if (ok && a is not null && !a.Disabled && a.CredentialStamp == p.Session.CredentialStamp &&
            a.PrivilegeStamp == p.Session.PrivilegeStamp && ResolveHandle(p.Session.Handle) is not null)
        {
            Limiter.Succeed(key);
            return null;
        }
        Limiter.Fail(c.Ip, key);
        Audit.Record(AuditLog.Entry("auth.confirm", p, c.Ip, false, "wrong password"));
        return new SignInResult.Refused(401, "invalid_credentials", "wrong password");
    }

    public string AccountKey(string id) => Accounts.Find(id) is { } a ? NameKey(a.Name) : "name:?malformed";

    public bool RecentlyAuthenticated(WebPrincipal p) => ResolveHandle(p.Session.Handle) is { } live &&
        live.Account.CredentialStamp == p.Account.CredentialStamp && live.Account.PrivilegeStamp == p.Account.PrivilegeStamp &&
        live.Account.PrivilegeStamp == live.Session.PrivilegeStamp && live.Session.HasRecentAuthentication(Now);

    /// <summary>Explicit step-up: password OR a fresh enrolled TOTP. No recovery-code or launcher shortcut.</summary>
    public async Task<SignInResult.Refused?> ReauthenticateAsync(WebPrincipal p, string? password, string? code, ClientInfo c, CancellationToken ct)
    {
        if ((password is null) == (code is null))
            return new SignInResult.Refused(400, "invalid_reauth", "provide exactly one password or enrolled two-factor code");
        if (ResolveHandle(p.Session.Handle) is not { Restriction: Restriction.None } live ||
            live.Session.CredentialStamp != p.Session.CredentialStamp || live.Session.PrivilegeStamp != live.Account.PrivilegeStamp)
            return new SignInResult.Refused(401, "unauthenticated", "sign in again");
        if (password is not null)
        {
            if (await ConfirmPasswordAsync(live, password, c, ct).ConfigureAwait(false) is { } refused) return refused;
        }
        else
        {
            var key = AccountKey(p.Id);
            if (Limiter.Attempt(c.Ip, key) is { } wait) return RateLimited("auth.reauth", c, wait, p.Name, p.Id);
            var a = Accounts.Find(p.Id)!;
            var step = a.TwoFactor && Base32.Decode(a.TotpSecret) is { } secret ? Totp.Verify(secret, code, Now, a.TotpLastStep) : null;
            if (step is null || !TryUse(a.Id, x => !x.Disabled && x.CredentialStamp == live.Session.CredentialStamp &&
                    x.TotpSecret == a.TotpSecret && x.TotpLastStep < step.Value ? x with { TotpLastStep = step.Value } : null))
            {
                Limiter.Fail(c.Ip, key);
                Audit.Record(AuditLog.Entry("auth.reauth", live, c.Ip, false, "invalid proof"));
                return new SignInResult.Refused(401, "invalid_credentials", "invalid credentials");
            }
            Limiter.Succeed(key);
        }
        var latest = Accounts.Find(p.Id);
        if (latest is null || latest.Disabled || !Sessions.MarkReauthenticated(live.Session, latest, Now + ReauthLifetime))
            return new SignInResult.Refused(401, "unauthenticated", "sign in again");
        Audit.Record(AuditLog.Entry("auth.reauth", live, c.Ip, true));
        return null;
    }

    /// <summary>Changes the user's own password: every other session ends, this one gets a new id.</summary>
    public async Task<SignInResult> ChangePasswordAsync(WebPrincipal p, string? current, string? next, ClientInfo c, CancellationToken ct)
    {
        if (await ConfirmPasswordAsync(p, current, c, ct).ConfigureAwait(false) is { } refused) return refused;
        if (PasswordProblem(p.Name, next) is { } problem) return new SignInResult.Refused(422, "weak_password", problem);
        if (current == next) return new SignInResult.Refused(422, "weak_password", "the new password must differ from the old one");
        var hash = await Hasher.HashAsync(next!, ct).ConfigureAwait(false);
        var a = Accounts.Update(p.Id, x => x with { PasswordHash = hash, MustChangePassword = false, CredentialStamp = x.CredentialStamp + 1 });
        Sessions.RevokeAccount(p.Id, p.Session.Handle);
        var (token, rec) = Sessions.Rotate(p.Session, a);
        Audit.Record(AuditLog.Entry("auth.password", p, c.Ip, true));
        return new SignInResult.Success(token, rec, a);
    }

    /// <summary>Starts two-factor setup: a new secret (pending until confirmed with a code). Needs the password.</summary>
    public async Task<(SignInResult.Refused? Refused, string? Secret, string? Uri)> BeginTwoFactorAsync(WebPrincipal p, string? password, ClientInfo c, CancellationToken ct)
    {
        if (p.Account.TwoFactor) return (new SignInResult.Refused(409, "2fa_on", "two-factor is on already (turn it off first to change the device)"), null, null);
        if (await ConfirmPasswordAsync(p, password, c, ct).ConfigureAwait(false) is { } refused) return (refused, null, null);
        var secret = Totp.NewSecret();
        var b32 = Base32.Encode(secret);
        Accounts.Update(p.Id, x => x with { PendingTotpSecret = b32, PendingTotpAt = _time.GetUtcNow() });
        return (null, b32, Totp.Uri(secret, Product.Name, p.Name));
    }

    /// <summary>Confirms setup with a code from the app: two-factor is on, the recovery codes are returned once, every other
    /// session ends and this one gets a new id (and counts as two-factor).</summary>
    public SignInResult EnableTwoFactor(WebPrincipal p, string? code, ClientInfo c, out IReadOnlyList<string>? recoveryCodes)
    {
        recoveryCodes = null;
        var a = Accounts.Find(p.Id)!;
        if (a.TwoFactor) return new SignInResult.Refused(409, "2fa_on", "two-factor is on already");
        if (a.PendingTotpSecret is null || a.PendingTotpAt is not { } at || _time.GetUtcNow() - at > PendingTotpLifetime || Base32.Decode(a.PendingTotpSecret) is not { } secret)
            return new SignInResult.Refused(409, "2fa_not_started", "start the two-factor setup again");
        var key = AccountKey(p.Id);
        if (Limiter.Attempt(c.Ip, key) is { } wait) return RateLimited("auth.2fa_enable", c, wait, p.Name, p.Id);
        if (Totp.Verify(secret, code, _time.GetUtcNow(), 0) is not { } step)
        {
            Limiter.Fail(c.Ip, key);
            return new SignInResult.Refused(401, "invalid_code", "wrong code: check the time on the phone");
        }
        var (codes, hashes) = RecoveryCodes.Generate();
        var pending = a.PendingTotpSecret;
        var updated = Accounts.Update(p.Id, x => x.PendingTotpSecret != pending ? throw new InvalidOperationException("the setup changed") : x with
        {
            TotpSecret = pending, TotpLastStep = step, PendingTotpSecret = null, PendingTotpAt = null, RecoveryCodes = hashes,
            CredentialStamp = x.CredentialStamp + 1,
        });
        Sessions.RevokeAccount(p.Id, p.Session.Handle);
        var (token, rec) = Sessions.Rotate(p.Session, updated, mfa: true);
        recoveryCodes = codes;
        Audit.Record(AuditLog.Entry("auth.2fa_enable", p, c.Ip, true));
        return new SignInResult.Success(token, rec, updated);
    }

    /// <summary>Turns two-factor off (password and a code or recovery code); refused while a role requires it.</summary>
    public async Task<SignInResult> DisableTwoFactorAsync(WebPrincipal p, string? password, string? code, ClientInfo c, CancellationToken ct)
    {
        if (!p.Account.TwoFactor) return new SignInResult.Refused(409, "2fa_off", "two-factor is off");
        if (p.Roles.Any(r => r.Require2fa)) return new SignInResult.Refused(403, "2fa_required", "one of your roles requires two-factor");
        if (await ConfirmPasswordAsync(p, password, c, ct).ConfigureAwait(false) is { } refused) return refused;
        var a = Accounts.Find(p.Id)!;
        var key = AccountKey(p.Id);
        var good = Base32.Decode(a.TotpSecret) is { } secret && Totp.Verify(secret, code, _time.GetUtcNow(), a.TotpLastStep) is not null
                   || RecoveryCodes.Match(a.RecoveryCodes, code) >= 0;
        if (!good)
        {
            Limiter.Fail(c.Ip, key);
            return new SignInResult.Refused(401, "invalid_code", "wrong code");
        }
        var updated = Accounts.Update(p.Id, x => x with { TotpSecret = null, TotpLastStep = 0, RecoveryCodes = [], CredentialStamp = x.CredentialStamp + 1 });
        Sessions.RevokeAccount(p.Id, p.Session.Handle);
        var (token, rec) = Sessions.Rotate(p.Session, updated, mfa: false);
        Audit.Record(AuditLog.Entry("auth.2fa_disable", p, c.Ip, true));
        return new SignInResult.Success(token, rec, updated);
    }

    /// <summary>New recovery codes (the old ones stop working). Needs the password.</summary>
    public async Task<(SignInResult.Refused? Refused, IReadOnlyList<string>? Codes)> NewRecoveryCodesAsync(WebPrincipal p, string? password, ClientInfo c, CancellationToken ct)
    {
        if (!p.Account.TwoFactor) return (new SignInResult.Refused(409, "2fa_off", "two-factor is off"), null);
        if (await ConfirmPasswordAsync(p, password, c, ct).ConfigureAwait(false) is { } refused) return (refused, null);
        var (codes, hashes) = RecoveryCodes.Generate();
        Accounts.Update(p.Id, x => x with { RecoveryCodes = hashes });
        Audit.Record(AuditLog.Entry("auth.recovery_codes", p, c.Ip, true));
        return (null, codes);
    }

    // ------------------------------------------------------------------------------------------------ rules, helpers

    /// <summary>Why a new password is not acceptable, or null.</summary>
    public string? PasswordProblem(string name, string? password)
    {
        var min = Settings.PasswordMinLength;
        if (password is null || password.Length < min) return $"a password needs at least {min} characters";
        if (password.Length > MaxPasswordLength) return $"a password has at most {MaxPasswordLength} characters";
        if (password.Any(char.IsControl)) return "a password cannot hold control characters";
        if (password.Distinct().Count() < 4) return "that password is too simple";
        if (password.Contains(name, StringComparison.OrdinalIgnoreCase)) return "a password cannot contain the account name";
        return null;
    }

    /// <summary>The lockout key of a typed name: the name's key when it could be an account name (whether or not one
    /// exists), else one shared key for malformed names.</summary>
    public static string NameKey(string name) => AccountNames.IsValid(name) ? "name:" + AccountNames.Key(name) : "name:?malformed";

    private SignInResult.Refused Wrong(ClientInfo c, string key, string name)
    {
        Limiter.Fail(c.Ip, key);
        var known = Accounts.FindByName(name);
        Audit.Record(AuditLog.Entry("auth.login", null, c.Ip, false, "wrong name or password", userName: known?.Name ?? "") with { User = known?.Id ?? "" });
        return new SignInResult.Refused(401, "invalid_credentials", "wrong name or password");
    }

    private SignInResult.Refused RateLimited(string op, ClientInfo c, TimeSpan wait, string? name, string? accountId = null)
    {
        var known = name is null ? null : Accounts.FindByName(name);
        Audit.Record(AuditLog.Entry(op, null, c.Ip, false, "rate limited", userName: known?.Name ?? "") with { User = accountId ?? known?.Id ?? "" });
        var s = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
        return new SignInResult.Refused(429, "rate_limited", $"too many attempts: try again in {s} s", TimeSpan.FromSeconds(s));
    }
}
