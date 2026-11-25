// SPDX-License-Identifier: MIT
using Kilnwarden.Http;
using Kilnwarden.Modules;
using Kilnwarden.Routing;
using Kilnwarden.Security;

namespace Kilnwarden.Hosting;

/// <summary>What runs for one request after the connection parsed it: the route match (before the body is read, for
/// the route's body cap), the middleware by stage, then the terminal: the gate and the handler, or the fallback (static
/// files, the app shell) when no route matched.</summary>
public sealed class RequestPipeline
{
    private readonly ModuleRegistry _registry;
    private readonly IAccessGate _gate;
    private readonly RequestDelegate _entry;
    private readonly RouteHandler _fallback;
    private readonly IWebAdminLog _log;

    public RequestPipeline(ModuleRegistry registry, IReadOnlyList<IHttpMiddleware> middleware, IAccessGate gate, RouteHandler fallback, IWebAdminLog log)
    {
        _registry = registry;
        _gate = gate;
        _fallback = fallback;
        _log = log;
        RequestDelegate next = TerminalAsync;
        for (var i = middleware.Count - 1; i >= 0; i--)
        {
            var m = middleware[i];
            var inner = next;
            next = ctx => m.InvokeAsync(ctx, inner);
        }
        _entry = next;
    }

    /// <summary>Matches the request's route (the connection calls this before reading the body). Null segments: the path
    /// was refused by <see cref="PathSegments"/> (400).</summary>
    public RouteLookup Match(RequestContext ctx, string[] segments)
    {
        var lookup = _registry.Routes.Lookup(ctx.Request.Method, segments, _registry.IsEnabled);
        ctx.Route = lookup.Match;
        ctx.Items[typeof(RouteLookup)] = lookup;
        return lookup;
    }

    /// <summary>Runs the middleware and the terminal. Never throws for a request's own faults: an <see cref="HttpError"/>
    /// becomes its status, anything else a 500 (logged, no details to the client).</summary>
    public async ValueTask<HttpResponse> RunAsync(RequestContext ctx)
    {
        try
        {
            return await _entry(ctx).ConfigureAwait(false);
        }
        catch (HttpError e)
        {
            return HttpResponse.Error(e.Status, ErrorCode(e.Status), e.Message);
        }
        catch (OperationCanceledException) when (ctx.Aborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            _log.Error($"{ctx.Request.Method} {ctx.Request.Path}: {(ctx.Route?.Route.ToString() ?? "no route")} failed", e);
            return HttpResponse.Error(500, "internal", "the server failed to handle this request (see kiln.log)");
        }
    }

    private async ValueTask<HttpResponse> TerminalAsync(RequestContext ctx)
    {
        if (ctx.Route is not { } match)
        {
            if (ctx.Items.GetValueOrDefault(typeof(RouteLookup)) is RouteLookup { Result: LookupResult.MethodNotAllowed } l)
            {
                var r = HttpResponse.Error(405, "method_not_allowed", $"{ctx.Request.Method} is not allowed here");
                r.Headers.Set("Allow", string.Join(", ", l.Allowed.Contains("GET") ? l.Allowed.Append("HEAD") : l.Allowed));
                return r;
            }
            return await _fallback(ctx).ConfigureAwait(false);
        }
        var route = match.Route;
        var decision = await _gate.CheckAsync(ctx, route.Access).ConfigureAwait(false);
        if (!decision.Allowed) return HttpResponse.Error(decision.Status, decision.Code, decision.Message);
        if (route.WebSocket is { } ws)
        {
            if (!ctx.Request.IsWebSocketUpgrade)
            {
                var r = HttpResponse.Error(426, "upgrade_required", "this is a WebSocket endpoint");
                r.Headers.Set("Upgrade", "websocket");
                return r;
            }
            return HttpResponse.WebSocketAccept(ws);
        }
        return await route.Handler!(ctx).ConfigureAwait(false);
    }

    public static string ErrorCode(int status) => status switch
    {
        400 => "bad_request",
        401 => "unauthenticated",
        403 => "forbidden",
        404 => "not_found",
        405 => "method_not_allowed",
        408 => "timeout",
        409 => "conflict",
        413 => "too_large",
        415 => "unsupported_media_type",
        422 => "invalid",
        429 => "rate_limited",
        431 => "headers_too_large",
        501 => "not_implemented",
        503 => "unavailable",
        _ => status >= 500 ? "internal" : "error",
    };
}
