// SPDX-License-Identifier: MIT
using System.Net;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Http;
using Kilnwarden.Kap;
using Kilnwarden.Routing;

namespace Kilnwarden.Ops;

/// <summary>Who a stored job acts for: an account id and the name it had when the job was saved (for display only; the
/// id is what is resolved at run time).</summary>
public sealed record ActorRef(string Id, string Name);

/// <summary>A principal for an account as it is right now, without any session: what a scheduled job is checked as.
/// Nothing of a sign-in is kept for it (no token, no password, no proof), so it never has recent proof; whether two-factor
/// counts as done follows the account (enrolled = satisfied), so a role that requires two-factor refuses an account that
/// has none.</summary>
internal static class Actors
{
    public static WebPrincipal? Resolve(AuthService auth, string? accountId)
    {
        var a = auth.Accounts.Find(accountId);
        if (a is null || a.Disabled) return null;
        var roles = a.Roles.Select(auth.Roles.Find).OfType<RoleDef>().ToList();
        var rec = new SessionRecord
        {
            AccountId = a.Id, Handle = "job", CredentialStamp = a.CredentialStamp, PrivilegeStamp = a.PrivilegeStamp, Mfa = a.TwoFactor,
            Via = "job", ReauthenticatedUntil = null,
        };
        return new WebPrincipal(a, roles, rec, auth.Registry);
    }
}

/// <summary>Route helpers shared by the lane's modules.</summary>
internal static class OpsHttp
{
    public const string ReauthMessage = "re-authenticate with your password or enrolled two-factor code, then retry";

    public static WebPrincipal User(RequestContext ctx) => ctx.User as WebPrincipal ?? throw new HttpError(401, "sign in first");

    public static HttpResponse Fail(int status, string code, string message) => HttpResponse.Error(status, code, message);

    public static HttpResponse Reauth() => Fail(403, "reauth_required", ReauthMessage);

    /// <summary>A gate refusal as an HTTP answer (403, its code: forbidden or reauth_required).</summary>
    public static HttpResponse Refused(KapDecision d) => Fail(403, d.Code is { Length: > 0 } c ? c : "forbidden", d.Message);

    /// <summary>A Kiln.Admin failure as an HTTP answer: 503 when it is not connected, else 502 with kap_&lt;code&gt;.</summary>
    public static HttpResponse KapFailed(KapResult r) => r.Code is "shutting_down"
        ? Fail(503, "kap_unavailable", "Kiln.Admin is not connected")
        : Fail(502, "kap_" + (KapOps.IsOpName(r.Code) ? r.Code : "failed"), r.Error ?? $"{r.Op} failed");

    public static JsonObject Body(RequestContext ctx) => ctx.Request.Body.IsEmpty ? new JsonObject() : ctx.ReadJson<JsonObject>();

    public static string? Str(JsonObject o, string name) => o[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    public static bool? Bool(JsonObject o, string name) => o[name] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    public static long? Long(JsonObject o, string name) => o[name] is JsonValue v && v.TryGetValue<long>(out var n) ? n
        : o[name] is JsonValue d && d.TryGetValue<double>(out var x) && x == Math.Floor(x) && Math.Abs(x) < 9e15 ? (long)x : null;

    /// <summary>One line of text: no control characters (tabs included), at most <paramref name="max"/> characters.</summary>
    public static bool IsOneLine(string? s, int max) => s is not null && s.Length <= max && !s.Any(char.IsControl);

    public static void Audit(AuditLog audit, string op, WebPrincipal? user, IPAddress? ip, bool ok, string? error, JsonObject? args, double ms = 0,
        string? userName = null) =>
        audit.Record(AuditLog.Entry(op, user, ip, ok, error, args, ms, userName));

    /// <summary>A URL as logs and the audit keep it: scheme, host, port and path; never user info, query or fragment
    /// (GitHub's signed download URLs carry a token in the query).</summary>
    public static string SafeUrl(Uri u) => u.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped);
}
