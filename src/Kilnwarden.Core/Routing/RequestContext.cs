// SPDX-License-Identifier: MIT
using System.Net;
using System.Text.Json;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Security;

namespace Kilnwarden.Routing;

/// <summary>One request on its way through the pipeline (early checks, route match, middleware, gate, handler).</summary>
public sealed class RequestContext
{
    public required HttpRequest Request { get; init; }
    public required ConnectionInfo Connection { get; init; }
    public required WebAdminServices Services { get; init; }

    /// <summary>Cancelled when the connection drops or the server stops.</summary>
    public CancellationToken Aborted { get; init; }

    /// <summary>The client's address: the TCP peer, or with the reverse-proxy switch on and the peer a trusted proxy,
    /// the address in X-Forwarded-For.</summary>
    public required IPAddress ClientAddress { get; set; }

    /// <summary>"https" or "http", as the browser sees it (X-Forwarded-Proto behind a trusted proxy).</summary>
    public required string Scheme { get; set; }

    /// <summary>The host as the browser sees it (Host, or X-Forwarded-Host behind a trusted proxy), with the port when
    /// the browser sends one.</summary>
    public required string Host { get; set; }

    /// <summary>scheme://host: what the browser's Origin header must equal for a same-origin request.</summary>
    public string Origin => $"{Scheme}://{Host}";

    public bool IsHttps => Scheme == "https";

    /// <summary>The matched route; null when no route matched (static files, the app shell, 404).</summary>
    public RouteMatch? Route { get; internal set; }

    /// <summary>The signed-in user; set by the auth component's session middleware, null when anonymous.</summary>
    public IPrincipal? User { get; set; }

    /// <summary>Per-request state for middleware (the session record, a CSRF verdict...).</summary>
    public Dictionary<object, object?> Items { get; } = [];

    /// <summary>Headers every response to this request gets (middleware adds Set-Cookie here, for example).</summary>
    public HttpHeaders ResponseHeaders { get; } = new();

    /// <summary>A route value ("{id}" in the pattern), decoded; throws when the route has none of that name.</summary>
    public string RouteValue(string name) =>
        Route?.Values.TryGetValue(name, out var v) == true ? v : throw new KeyNotFoundException($"no route value '{name}'");

    /// <summary>The body as JSON (strict: UTF-8, no comments, depth ≤ 32, the media type must be application/json).
    /// Throws <see cref="HttpError"/> 415 or 400; handlers let it propagate and the pipeline answers with it.</summary>
    public T ReadJson<T>() => JsonBody.Read<T>(Request);
}
