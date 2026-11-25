// SPDX-License-Identifier: MIT
using Kilnwarden.Routing;

namespace Kilnwarden.Security;

/// <summary>The server-side check of every route (HTTP and WebSocket upgrade) before its handler runs. The auth
/// component replaces the default with its permission gate (audit, step-up, per-module rules). Actions, KAP ops and
/// WebSocket messages are checked by the code that runs them, through the same component.</summary>
public interface IAccessGate
{
    ValueTask<AccessDecision> CheckAsync(RequestContext ctx, RouteAccess access);
}

public readonly record struct AccessDecision(bool Allowed, int Status, string Code, string Message)
{
    public static AccessDecision Allow { get; } = new(true, 200, "", "");
    public static AccessDecision Unauthenticated { get; } = new(false, 401, "unauthenticated", "sign in first");
    public static AccessDecision Forbidden(string permission) => new(false, 403, "forbidden", $"needs permission {permission}");
}

/// <summary>The gate before an auth component is installed, and the minimum any replacement must keep: Public routes
/// pass, everything else needs <see cref="RequestContext.User"/>, permission routes need the user to have it. With no
/// session middleware there is never a user, so only Public routes answer (closed by default).</summary>
public sealed class DefaultAccessGate : IAccessGate
{
    public ValueTask<AccessDecision> CheckAsync(RequestContext ctx, RouteAccess access) => ValueTask.FromResult(Decide(ctx.User, access));

    public static AccessDecision Decide(IPrincipal? user, RouteAccess access) => access.Kind switch
    {
        AccessKind.Public => AccessDecision.Allow,
        _ when user is null => AccessDecision.Unauthenticated,
        AccessKind.SignedIn => AccessDecision.Allow,
        AccessKind.Permission when user.Has(access.Permission!) => AccessDecision.Allow,
        _ => AccessDecision.Forbidden(access.Permission ?? "?"),
    };
}
