// SPDX-License-Identifier: MIT
using System.Text.Json;
using System.Text.RegularExpressions;
using Kilnwarden.Hosting;
using Kilnwarden.Storage;

namespace Kilnwarden.Auth;

/// <summary>A panel account (DESIGN §3). Immutable: changes go through <see cref="AccountStore.Update"/> with a new copy.
/// The file keeps no secret in the clear except the TOTP seed (accounts.json is a secret file: mode 0600 where the OS
/// has modes).</summary>
public sealed record Account
{
    /// <summary>"u-" + 16 hex characters, random, never reused.</summary>
    public string Id { get; init; } = "";

    /// <summary>The sign-in name as typed at creation: 3 to 32 of A-Z a-z 0-9 . _ - (unique ignoring case). Also the
    /// KAP command source "web:&lt;name&gt;" and the audit log's name.</summary>
    public string Name { get; init; } = "";

    /// <summary>"pbkdf2-sha256$..." or "" for an account without a password (external sign-in only, step 2).</summary>
    public string PasswordHash { get; init; } = "";

    public IReadOnlyList<string> Roles { get; init; } = [];
    public bool Disabled { get; init; }

    /// <summary>The next sign-in may only change the password (accounts made or reset by someone else).</summary>
    public bool MustChangePassword { get; init; }

    /// <summary>The TOTP seed (base32) once two-factor is on; null when off.</summary>
    public string? TotpSecret { get; init; }

    /// <summary>The last TOTP step accepted (a code is never accepted twice).</summary>
    public long TotpLastStep { get; init; }

    /// <summary>A seed being set up (not yet confirmed with a code), and since when.</summary>
    public string? PendingTotpSecret { get; init; }

    public DateTimeOffset? PendingTotpAt { get; init; }

    /// <summary>SHA-256 hex of each unused recovery code.</summary>
    public IReadOnlyList<string> RecoveryCodes { get; init; } = [];

    /// <summary>Bumped when the password, two-factor or the enabled state changes: every session of the account ends.</summary>
    public long CredentialStamp { get; init; }

    /// <summary>Bumped when the roles change: every session gets a new id at its next request (rotation).</summary>
    public long PrivilegeStamp { get; init; }

    /// <summary>The owner made by the first-run setup (the launcher's one-time login signs in as this one).</summary>
    public bool Primary { get; init; }

    /// <summary>External sign-ins linked to the account (step 2: "steam" -> SteamID64, "discord" -> user id).</summary>
    public IReadOnlyDictionary<string, string> Links { get; init; } = new Dictionary<string, string>();

    /// <summary>Roles a provider's mapping gave at the last sign-in through it (identity.json roleMap: Discord roles,
    /// Kiln permission groups), per provider. Kept apart from <see cref="Roles"/> (what an admin set); they count only
    /// while the mapping still names them (AuthService.MappedRoles), and never include the owner role.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Synced { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    public DateTimeOffset Created { get; init; }
    public DateTimeOffset Updated { get; init; }
    public DateTimeOffset? LastSignIn { get; init; }
    public string? CreatedBy { get; init; }

    public bool IsOwner => Roles.Contains(BuiltinRoles.Owner);
    public bool TwoFactor => TotpSecret is not null;
}

public static partial class AccountNames
{
    /// <summary>3 to 32 characters of A-Z a-z 0-9 . _ -, starting with a letter or digit.</summary>
    public static bool IsValid(string? name) => name is not null && NameRe().IsMatch(name);

    /// <summary>The uniqueness key (names compare ignoring ASCII case).</summary>
    public static string Key(string name) => name.ToLowerInvariant();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{2,31}$")]
    private static partial Regex NameRe();
}

/// <summary>accounts.json: every account, loaded at start (an unreadable file stops the panel: it never runs without
/// its accounts, and never falls back to "no owner yet"), changed under a lock and written atomically. Also the
/// <see cref="ISetupState"/>: the owner exists once an enabled account has the owner role.</summary>
public sealed class AccountStore : ISetupState
{
    public const string File = "accounts.json";

    private readonly DataStore _data;
    private readonly TimeProvider _time;
    private readonly object _lock = new();
    private IReadOnlyList<Account> _all = [];

    public AccountStore(DataStore data, TimeProvider? time = null)
    {
        _data = data;
        _time = time ?? TimeProvider.System;
        AccountsFile? f;
        try
        {
            f = data.ReadJson<AccountsFile>(File);
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new InvalidDataException($"{File} is unreadable ({e.Message}): fix or restore it; the panel stays off until then", e);
        }
        if (f is null) return;
        if (f.Format != 1) throw new InvalidDataException($"{File}: format {f.Format} is not supported");
        var list = f.Accounts ?? [];
        var problems = Check(list);
        if (problems is not null) throw new InvalidDataException($"{File}: {problems}; the panel stays off until it is fixed");
        _all = list;
    }

    public IReadOnlyList<Account> All => Volatile.Read(ref _all);

    public Account? Find(string? id) => id is null ? null : All.FirstOrDefault(a => a.Id == id);

    public Account? FindByName(string? name) =>
        name is null || !AccountNames.IsValid(name) ? null : All.FirstOrDefault(a => AccountNames.Key(a.Name) == AccountNames.Key(name));

    public bool OwnerExists => All.Any(a => a.IsOwner && !a.Disabled);

    /// <summary>The launcher's one-time login signs in as this: the primary owner, else the oldest enabled owner.</summary>
    public Account? PrimaryOwner =>
        All.Where(a => a.IsOwner && !a.Disabled).OrderByDescending(a => a.Primary).ThenBy(a => a.Created).FirstOrDefault();

    /// <summary>ISetupState: fires when <see cref="OwnerExists"/> flips.</summary>
    public event Action? Changed;

    /// <summary>Any account changed (sessions re-check their stamps).</summary>
    public event Action<Account?, Account?>? AccountChanged;

    public static string NewId() => "u-" + Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8));

    /// <summary>Adds <paramref name="a"/>. Throws InvalidOperationException when the name is taken, ArgumentException for a
    /// bad account.</summary>
    public Account Add(Account a) => Mutate(list =>
    {
        if (list.Any(x => AccountNames.Key(x.Name) == AccountNames.Key(a.Name))) throw new InvalidOperationException($"the name '{a.Name}' is taken");
        var now = _time.GetUtcNow();
        var added = a with { Id = a.Id.Length > 0 ? a.Id : NewId(), Created = now, Updated = now };
        return ([.. list, added], null, added);
    });

    /// <summary>Changes one account. Throws KeyNotFoundException for an unknown id, InvalidOperationException when the
    /// change would leave no enabled owner or clash with another name.</summary>
    public Account Update(string id, Func<Account, Account> change) => Mutate(list =>
    {
        var i = list.FindIndex(x => x.Id == id);
        if (i < 0) throw new KeyNotFoundException($"no account {id}");
        var old = list[i];
        var next = change(old) with { Id = old.Id, Created = old.Created, Updated = _time.GetUtcNow() };
        var copy = new List<Account>(list) { [i] = next };
        return (copy, old, next);
    });

    /// <summary>Links an external identity to an account, atomically: InvalidOperationException when another account
    /// holds it already (security review S7: the check and the write happen under one lock).</summary>
    public Account LinkIfFree(string id, string provider, string subject) => Mutate(list =>
    {
        if (list.Any(x => x.Id != id && x.Links.TryGetValue(provider, out var s) && s == subject))
            throw new InvalidOperationException($"that {provider} account is linked to another panel account");
        var i = list.FindIndex(x => x.Id == id);
        if (i < 0) throw new KeyNotFoundException($"no account {id}");
        var old = list[i];
        var next = old with { Links = new Dictionary<string, string>(old.Links) { [provider] = subject }, Updated = _time.GetUtcNow() };
        return (new List<Account>(list) { [i] = next }, old, next);
    });

    /// <summary>Removes one account. Same refusals as <see cref="Update"/>.</summary>
    public Account Remove(string id) => Mutate(list =>
    {
        var old = list.FirstOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException($"no account {id}");
        return ([.. list.Where(x => x.Id != id)], old, null);
    });

    private Account Mutate(Func<List<Account>, (List<Account> Next, Account? Old, Account? New)> change)
    {
        Account? oldA, newA;
        bool ownerBefore, ownerAfter;
        lock (_lock)
        {
            var list = All.ToList();
            ownerBefore = list.Any(a => a.IsOwner && !a.Disabled);
            var (next, o, n) = change(list);
            if (Check(next) is { } problem) throw new ArgumentException(problem);
            ownerAfter = next.Any(a => a.IsOwner && !a.Disabled);
            if (ownerBefore && !ownerAfter) throw new InvalidOperationException("the panel needs at least one enabled owner");
            _data.WriteJson(File, new AccountsFile { Accounts = next }, secret: true);
            Volatile.Write(ref _all, next);
            (oldA, newA) = (o, n);
        }
        try
        {
            AccountChanged?.Invoke(oldA, newA);
            if (ownerBefore != ownerAfter) Changed?.Invoke();
        }
        catch (Exception)
        {
            // a listener's bug never undoes a saved change
        }
        return newA ?? oldA!;
    }

    /// <summary>What makes a list of accounts valid; null when it is.</summary>
    internal static string? Check(IReadOnlyList<Account> list)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in list)
        {
            if (a is null) return "an empty account entry";
            if (a.Id is null || !System.Text.RegularExpressions.Regex.IsMatch(a.Id, "^u-[0-9a-f]{16}$")) return $"bad account id '{a.Id}'";
            if (!ids.Add(a.Id)) return $"account id {a.Id} twice";
            if (!AccountNames.IsValid(a.Name)) return $"account {a.Id}: bad name";
            if (!names.Add(AccountNames.Key(a.Name))) return $"the name '{a.Name}' twice";
            if (a.PasswordHash is null) return $"account {a.Name}: no password field";
            if (a.PasswordHash.Length > 0 && !PasswordHasher.TryParse(a.PasswordHash, out _, out _, out _)) return $"account {a.Name}: unreadable password hash";
            if (a.Roles is null || a.Roles.Any(r => !RoleNames.IsValid(r))) return $"account {a.Name}: bad role list";
            if (a.TotpSecret is not null && Base32.Decode(a.TotpSecret) is not { Length: >= 10 }) return $"account {a.Name}: bad TOTP secret";
            if (a.RecoveryCodes is null || a.Links is null) return $"account {a.Name}: missing lists";
            if (a.Synced is null || a.Synced.Values.Any(l => l is null || l.Any(r => !RoleNames.IsValid(r) || r == BuiltinRoles.Owner)))
                return $"account {a.Name}: bad synced roles";
        }
        return null;
    }

    private sealed class AccountsFile
    {
        public int Format { get; set; } = 1;
        public List<Account>? Accounts { get; set; } = [];
    }
}
