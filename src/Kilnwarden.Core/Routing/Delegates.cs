// SPDX-License-Identifier: MIT
using Kilnwarden.Http;

namespace Kilnwarden.Routing;

/// <summary>A route's handler. Runs on the server's I/O threads, never on the tick thread: game work goes through
/// Kiln.Admin (KAP) or <see cref="Hosting.IGameThread"/>. Throw <see cref="HttpError"/> to answer with an error.</summary>
public delegate ValueTask<HttpResponse> RouteHandler(RequestContext ctx);

/// <summary>The rest of the pipeline, as middleware sees it.</summary>
public delegate ValueTask<HttpResponse> RequestDelegate(RequestContext ctx);

/// <summary>Where a middleware runs. Before any middleware the connection has parsed the head, applied the IP allowlist,
/// rate limits, X-Forwarded-* and the Host check, matched the route (<see cref="RequestContext.Route"/>, so middleware
/// can read it) and read the body up to the route's cap. After the last middleware: the gate, then the handler (or the
/// static files when no route matched).</summary>
public enum PipelineStage
{
    /// <summary>First (request logging, timing).</summary>
    Early = 0,

    /// <summary>Auth: resolve the session cookie to <see cref="RequestContext.User"/>.</summary>
    Session = 100,

    /// <summary>Auth: CSRF (Origin check on unsafe methods, SameSite cookies).</summary>
    Csrf = 200,

    /// <summary>Anything else before the gate (audit context, per-account limits).</summary>
    BeforeGate = 300,
}

/// <summary>A pipeline step: inspect or change the context, answer early, or call <c>next</c>.</summary>
public interface IHttpMiddleware
{
    ValueTask<HttpResponse> InvokeAsync(RequestContext ctx, RequestDelegate next);
}

/// <summary>A middleware from a lambda.</summary>
public sealed class Middleware(Func<RequestContext, RequestDelegate, ValueTask<HttpResponse>> run) : IHttpMiddleware
{
    public ValueTask<HttpResponse> InvokeAsync(RequestContext ctx, RequestDelegate next) => run(ctx, next);
}
