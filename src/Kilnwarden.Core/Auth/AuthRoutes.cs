// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;
using Kilnwarden.Http;
using Kilnwarden.Modules;
using Kilnwarden.Routing;

namespace Kilnwarden.Auth;

/// <summary>/api/auth: sign-in, two-factor, first-run setup, the launcher's login, the account's own password,
/// two-factor and sessions. Bodies are small JSON objects (4 KiB cap). Answers never say whether a name exists.</summary>
internal static class AuthRoutes
{
    public const long BodyCap = 4096;

    internal sealed record LoginBody(string? Name, string? Password);
    internal sealed record CodeBody(string? Code, string? RecoveryCode);
    internal sealed record SetupBody(string? Token, string? Name, string? Password);
    internal sealed record SecretBody(string? Secret);
    internal sealed record PasswordBody(string? Current, string? Password);
    internal sealed record ConfirmBody(string? Password, string? Code);

    public static void Map(ModuleRegistration m, AuthService auth)
    {
        m.MapGet("/status", RouteAccess.Public, ctx => Ok(new JsonObject
        {
            ["ownerExists"] = auth.OwnerExists,
            ["signedIn"] = ctx.User is WebPrincipal,
            ["launcherLogin"] = auth.Settings.LauncherLogin,
            ["providers"] = new JsonArray([(JsonNode)"local", .. auth.Providers.All.Select(p => (JsonNode)new JsonObject { ["id"] = p.Id, ["title"] = p.Title })]),
        }));

        m.MapPost("/login", RouteAccess.Public, async ctx =>
        {
            var b = ctx.ReadJson<LoginBody>();
            var r = await auth.SignInAsync(b.Name, b.Password, Client(ctx), Current(ctx), ctx.Aborted).ConfigureAwait(false);
            return Answer(ctx, auth, r);
        }, BodyCap);

        m.MapPost("/login/2fa", RouteAccess.Public, ctx =>
        {
            var b = ctx.ReadJson<CodeBody>();
            var ticket = AuthCookies.Read(ctx, AuthCookies.TwoFactor);
            var r = auth.CompleteTwoFactor(ticket, b.Code, b.RecoveryCode, Client(ctx), Current(ctx));
            if (r is SignInResult.Success) AuthCookies.Clear(ctx, AuthCookies.TwoFactor);
            return ValueTask.FromResult(Answer(ctx, auth, r));
        }, BodyCap);

        m.MapPost("/setup", RouteAccess.Public, async ctx =>
        {
            var b = ctx.ReadJson<SetupBody>();
            var r = await auth.SetupOwnerAsync(b.Token, b.Name, b.Password, Client(ctx), ctx.Aborted).ConfigureAwait(false);
            return Answer(ctx, auth, r);
        }, BodyCap);

        m.MapPost("/launcher", RouteAccess.Public, async ctx =>
        {
            var b = ctx.ReadJson<SecretBody>();
            var r = await auth.LauncherAsync(b.Secret, Client(ctx), Current(ctx), ctx.Aborted).ConfigureAwait(false);
            return Answer(ctx, auth, r);
        }, BodyCap);

        m.MapPost("/logout", RouteAccess.SignedIn, ctx =>
        {
            var p = (WebPrincipal)ctx.User!;
            auth.Sessions.Revoke(p.Session.Handle);
            AuthCookies.Clear(ctx, AuthCookies.Session);
            auth.Audit.Record(AuditLog.Entry("auth.logout", p, ctx.ClientAddress, true));
            return ValueTask.FromResult(HttpResponse.Empty());
        });

        m.MapGet("/me", RouteAccess.SignedIn, ctx => Ok(Me((WebPrincipal)ctx.User!)));

        m.MapPost("/reauth", RouteAccess.SignedIn, async ctx =>
        {
            var b = ctx.ReadJson<ConfirmBody>();
            var p = (WebPrincipal)ctx.User!;
            if (await auth.ReauthenticateAsync(p, b.Password, b.Code, Client(ctx), ctx.Aborted).ConfigureAwait(false) is { } refusal)
                return Refusal(refusal);
            return HttpResponse.Json(new JsonObject { ["ok"] = true,
                ["reauthenticatedUntil"] = auth.Sessions.FindByHandle(p.Session.Handle)?.ReauthenticatedUntil?.ToString("O") });
        }, BodyCap);

        m.MapPost("/password", RouteAccess.SignedIn, async ctx =>
        {
            var b = ctx.ReadJson<PasswordBody>();
            var r = await auth.ChangePasswordAsync((WebPrincipal)ctx.User!, b.Current, b.Password, Client(ctx), ctx.Aborted).ConfigureAwait(false);
            return Answer(ctx, auth, r);
        }, BodyCap);

        m.MapPost("/2fa/setup", RouteAccess.SignedIn, async ctx =>
        {
            var b = ctx.ReadJson<ConfirmBody>();
            var (refused, secret, uri) = await auth.BeginTwoFactorAsync((WebPrincipal)ctx.User!, b.Password, Client(ctx), ctx.Aborted).ConfigureAwait(false);
            return refused is not null ? Refusal(refused) : HttpResponse.Json(new JsonObject { ["secret"] = secret, ["uri"] = uri });
        }, BodyCap);

        m.MapPost("/2fa/enable", RouteAccess.SignedIn, ctx =>
        {
            var b = ctx.ReadJson<CodeBody>();
            var r = auth.EnableTwoFactor((WebPrincipal)ctx.User!, b.Code, Client(ctx), out var codes);
            var res = Answer(ctx, auth, r);
            if (r is SignInResult.Success && codes is not null)
                res = HttpResponse.Json(new JsonObject { ["ok"] = true, ["recoveryCodes"] = new JsonArray([.. codes.Select(c => (JsonNode)c)]) });
            return ValueTask.FromResult(res);
        }, BodyCap);

        m.MapPost("/2fa/disable", RouteAccess.SignedIn, async ctx =>
        {
            var b = ctx.ReadJson<ConfirmBody>();
            var r = await auth.DisableTwoFactorAsync((WebPrincipal)ctx.User!, b.Password, b.Code, Client(ctx), ctx.Aborted).ConfigureAwait(false);
            return Answer(ctx, auth, r);
        }, BodyCap);

        m.MapPost("/2fa/recovery-codes", RouteAccess.SignedIn, async ctx =>
        {
            var b = ctx.ReadJson<ConfirmBody>();
            var (refused, codes) = await auth.NewRecoveryCodesAsync((WebPrincipal)ctx.User!, b.Password, Client(ctx), ctx.Aborted).ConfigureAwait(false);
            return refused is not null ? Refusal(refused) : HttpResponse.Json(new JsonObject { ["recoveryCodes"] = new JsonArray([.. codes!.Select(c => (JsonNode)c)]) });
        }, BodyCap);

        m.MapGet("/sessions", RouteAccess.SignedIn, ctx =>
        {
            var p = (WebPrincipal)ctx.User!;
            return Ok(Sessions(auth.Sessions.ForAccount(p.Id), p.Session.Handle));
        });

        m.MapDelete("/sessions/{handle}", RouteAccess.SignedIn, ctx =>
        {
            var p = (WebPrincipal)ctx.User!;
            var handle = ctx.RouteValue("handle");
            // only the user's own sessions; someone else's handle answers like an unknown one
            if (auth.Sessions.FindByHandle(handle) is not { } s || s.AccountId != p.Id) return ValueTask.FromResult(HttpResponse.NotFound());
            auth.Sessions.Revoke(handle);
            if (handle == p.Session.Handle) AuthCookies.Clear(ctx, AuthCookies.Session);
            auth.Audit.Record(AuditLog.Entry("auth.session_revoke", p, ctx.ClientAddress, true, args: new JsonObject { ["session"] = handle }));
            return ValueTask.FromResult(HttpResponse.Empty());
        });

        m.MapPost("/sessions/revoke-others", RouteAccess.SignedIn, ctx =>
        {
            var p = (WebPrincipal)ctx.User!;
            var n = auth.Sessions.RevokeAccount(p.Id, p.Session.Handle);
            auth.Audit.Record(AuditLog.Entry("auth.session_revoke", p, ctx.ClientAddress, true, args: new JsonObject { ["others"] = n }));
            return ValueTask.FromResult(HttpResponse.Json(new JsonObject { ["revoked"] = n }));
        });
    }

    public static JsonObject Me(WebPrincipal p) => new()
    {
        ["id"] = p.Id,
        ["name"] = p.Name,
        ["roles"] = new JsonArray([.. p.Roles.Select(r => (JsonNode)r.Id)]),
        ["owner"] = p.IsOwner,
        ["permissions"] = new JsonArray([.. p.Effective().Select(x => (JsonNode)x)]),
        ["commandLevel"] = Kap.CommandLevels.Name(p.CommandLevel),
        ["twoFactor"] = p.Account.TwoFactor,
        ["recoveryCodesLeft"] = p.Account.RecoveryCodes.Count,
        ["restriction"] = p.Restriction switch
        {
            Restriction.PasswordChange => "password_change_required",
            Restriction.TwoFactorSetup => "2fa_setup_required",
            Restriction.TwoFactorSignIn => "2fa_signin_required",
            _ => null,
        },
        ["session"] = new JsonObject { ["handle"] = p.Session.Handle, ["via"] = p.Session.Via, ["expires"] = p.Session.Expires.ToString("O"), ["reauthenticatedUntil"] = p.Session.ReauthenticatedUntil?.ToString("O") },
    };

    public static JsonArray Sessions(IEnumerable<SessionRecord> list, string? current) => new([.. list.Select(s => (JsonNode)new JsonObject
    {
        ["handle"] = s.Handle, ["created"] = s.Created.ToString("O"), ["lastSeen"] = s.LastSeen.ToString("O"), ["expires"] = s.Expires.ToString("O"),
        ["ip"] = s.Ip, ["userAgent"] = s.UserAgent, ["via"] = s.Via, ["twoFactor"] = s.Mfa, ["current"] = s.Handle == current,
    })]);

    public static ClientInfo Client(RequestContext ctx) => new(ctx.ClientAddress, ctx.Request.Headers.Get("User-Agent") ?? "") { Relayed = ctx.Connection.Relayed };

    /// <summary>The handle of the session this request came with (a new sign-in replaces it).</summary>
    private static string? Current(RequestContext ctx) => (ctx.User as WebPrincipal)?.Session.Handle;

    private static ValueTask<HttpResponse> Ok(JsonNode body) => ValueTask.FromResult(HttpResponse.Json(body));

    /// <summary>A sign-in step's HTTP answer: cookies for success and the two-factor ticket, the refusal's status.</summary>
    public static HttpResponse Answer(RequestContext ctx, AuthService auth, SignInResult r)
    {
        switch (r)
        {
            case SignInResult.Success s:
                AuthCookies.Set(ctx, AuthCookies.Session, s.Token, s.Session.Expires - auth.Now);
                return HttpResponse.Json(new JsonObject { ["ok"] = true, ["user"] = auth.Resolve(s.Session) is { } p ? Me(p) : null });
            case SignInResult.TwoFactorNeeded t:
                AuthCookies.Set(ctx, AuthCookies.TwoFactor, t.Ticket, AuthService.TicketLifetime);
                return HttpResponse.Json(new JsonObject { ["ok"] = false, ["next"] = "2fa" });
            case SignInResult.SetupGranted g:
                return HttpResponse.Json(new JsonObject { ["ok"] = false, ["next"] = "setup", ["setupToken"] = g.SetupToken });
            case SignInResult.Refused f:
                return Refusal(f);
            default:
                throw new InvalidOperationException("unknown sign-in result");
        }
    }

    public static HttpResponse Refusal(SignInResult.Refused f)
    {
        var res = HttpResponse.Error(f.Status, f.Code, f.Message);
        if (f.RetryAfter is { } ra) res.Headers.Set("Retry-After", ((int)Math.Ceiling(ra.TotalSeconds)).ToString());
        return res;
    }
}
