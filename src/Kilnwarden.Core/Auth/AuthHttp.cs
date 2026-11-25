// SPDX-License-Identifier: MIT
using Kilnwarden.Http;
using Kilnwarden.Hosting;
using Kilnwarden.Routing;
using Kilnwarden.Security;

namespace Kilnwarden.Auth;

/// <summary>The session and two-factor cookies. On HTTPS (directly or behind a trusted proxy) they are "__Host-" cookies:
/// Secure, HttpOnly, SameSite=Strict, Path=/, no Domain, so no other site or subdomain can set or read them. Plain HTTP
/// is only possible on loopback (or behind the proxy switch), where the cookie cannot be Secure and drops the prefix;
/// each scheme only accepts its own name. Names hold no product name (a rename changes nothing here).</summary>
public static class AuthCookies
{
    public const string Session = "wa-session", TwoFactor = "wa-2fa";

    public static string Name(RequestContext ctx, string baseName) => ctx.IsHttps ? "__Host-" + baseName : baseName;

    /// <summary>The cookie's value when present and shaped like a token (else null; a malformed one is ignored).</summary>
    public static string? Read(RequestContext ctx, string baseName)
    {
        var name = Name(ctx, baseName);
        foreach (var header in ctx.Request.Headers.GetAll("Cookie"))
            if (Cookies.Parse(header).TryGetValue(name, out var v)) return SecretToken.IsWellFormed(v) ? v : null;
        return null;
    }

    public static void Set(RequestContext ctx, string baseName, string token, TimeSpan maxAge) =>
        ctx.ResponseHeaders.Add("Set-Cookie", new SetCookie(Name(ctx, baseName), token) { Secure = ctx.IsHttps, MaxAge = maxAge }.ToString());

    public static void Clear(RequestContext ctx, string baseName) =>
        ctx.ResponseHeaders.Add("Set-Cookie", SetCookie.Delete(Name(ctx, baseName), ctx.IsHttps).ToString());
}

/// <summary>PipelineStage.Session: resolves the session cookie to <see cref="RequestContext.User"/> (a fresh
/// <see cref="WebPrincipal"/>), rotates the session id when the account's roles changed since (the new cookie goes with
/// this response), clears a cookie that names no live session, and notes the activity for the idle timeout.</summary>
public sealed class SessionMiddleware(AuthService auth) : IHttpMiddleware
{
    public ValueTask<HttpResponse> InvokeAsync(RequestContext ctx, RequestDelegate next)
    {
        var token = AuthCookies.Read(ctx, AuthCookies.Session);
        if (token is not null)
        {
            var rec = auth.Sessions.Find(token);
            var p = auth.Resolve(rec);
            if (p is null) AuthCookies.Clear(ctx, AuthCookies.Session);
            else
            {
                var session = p.Session;
                if (p.Account.PrivilegeStamp != session.PrivilegeStamp)
                {
                    (var fresh, session) = auth.Sessions.Rotate(session, p.Account);
                    AuthCookies.Set(ctx, AuthCookies.Session, fresh, session.Expires - auth.Now);
                    p = auth.Resolve(session) ?? p;
                }
                else session = auth.Sessions.Touch(session);
                ctx.User = p;
            }
        }
        return next(ctx);
    }
}

/// <summary>PipelineStage.Csrf (DESIGN §2): besides the SameSite=Strict cookies, every request that can change something
/// (any method but GET/HEAD/OPTIONS/TRACE, and every WebSocket upgrade) must carry exactly one Origin equal to the
/// panel's own, and a Sec-Fetch-Site, when the browser sends one, of same-origin. There is no CORS: other sites can
/// neither send state-changing requests nor read answers.</summary>
public sealed class CsrfMiddleware : IHttpMiddleware
{
    public ValueTask<HttpResponse> InvokeAsync(RequestContext ctx, RequestDelegate next) =>
        Check(ctx) is { } refusal ? ValueTask.FromResult(refusal) : next(ctx);

    public static HttpResponse? Check(RequestContext ctx)
    {
        var req = ctx.Request;
        if (req.IsSafeMethod && !req.IsWebSocketUpgrade) return null;
        if (req.Headers.CountOf("Origin") != 1 || !string.Equals(req.Headers.Get("Origin"), ctx.Origin, StringComparison.OrdinalIgnoreCase))
            return HttpResponse.Error(403, "csrf", "cross-site request refused (Origin)");
        if (req.Headers.CountOf("Sec-Fetch-Site") > 1 || req.Headers.Get("Sec-Fetch-Site") is { } site && site != "same-origin")
            return HttpResponse.Error(403, "csrf", "cross-site request refused (Sec-Fetch-Site)");
        return null;
    }
}

/// <summary>The panel's route gate (replaces the core's default, keeps its minimum): public routes pass; every other
/// route needs a signed-in user; permission routes need the permission. A restricted session (a password to change,
/// two-factor to set up) reaches only the routes that fix it. Refusals of signed-in users go to the audit log.</summary>
public sealed class PermissionGate(AuditLog audit, AuthService? auth = null) : IAccessGate
{
    /// <summary>Routes a restricted session may still call (full patterns).</summary>
    public static readonly IReadOnlySet<string> AllowedWhileRestricted = new HashSet<string>(StringComparer.Ordinal)
    {
        "/api/auth/me", "/api/auth/logout", "/api/auth/password", "/api/auth/2fa/setup", "/api/auth/2fa/enable", "/api/auth/status",
    };

    public ValueTask<AccessDecision> CheckAsync(RequestContext ctx, RouteAccess access)
    {
        var d = Decide(ctx, access);
        if (d.Allowed && !ctx.Request.IsSafeMethod && ctx.User is WebPrincipal actor &&
            (access.Permission is UserRoutes.Manage or UserRoutes.RolesPerm or CoreComponent.SettingsEdit or CoreComponent.ModulesEdit ||
             access.Permission is { } permission && auth?.Registry.FindPermission(permission)?.Sensitive == true) &&
            !(auth?.RecentlyAuthenticated(actor) ?? actor.Session.HasRecentAuthentication(DateTimeOffset.UtcNow)))
            d = new AccessDecision(false, 403, "reauth_required", "re-authenticate with your password or enrolled two-factor code, then retry");
        if (!d.Allowed && d.Status == 403 && ctx.User is WebPrincipal p)
            audit.Record(AuditLog.Entry("auth.denied", p, ctx.ClientAddress, false, d.Message,
                new System.Text.Json.Nodes.JsonObject { ["method"] = ctx.Request.Method, ["route"] = ctx.Route?.Route.Pattern ?? ctx.Request.Path }));
        return ValueTask.FromResult(d);
    }

    public static AccessDecision Decide(RequestContext ctx, RouteAccess access)
    {
        if (access.Kind == AccessKind.Public) return AccessDecision.Allow;
        if (ctx.User is WebPrincipal { Restriction: not Restriction.None } p && !AllowedWhileRestricted.Contains(ctx.Route?.Route.Pattern ?? ""))
            return RestrictedDecision(p.Restriction);
        // a principal that is not ours (none should exist) gets the core's minimum rules
        return DefaultAccessGate.Decide(ctx.User, access);
    }

    public static AccessDecision RestrictedDecision(Restriction r) => r switch
    {
        Restriction.PasswordChange => new(false, 403, "password_change_required", "change your password first"),
        Restriction.TwoFactorSetup => new(false, 403, "2fa_setup_required", "one of your roles requires two-factor: set it up first"),
        _ => new(false, 403, "2fa_signin_required", "one of your roles requires two-factor: sign out and sign in with your code"),
    };
}
