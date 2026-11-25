// SPDX-License-Identifier: MIT
namespace Kilnwarden.Http;

/// <summary>One parsed request. Made by <c>HttpParser</c>: the method, target and headers have passed its strict
/// checks (RFC 9112 syntax, size caps, exactly one Host, no Transfer-Encoding). The body is read in full before the
/// pipeline runs, up to the matched route's cap.</summary>
public sealed class HttpRequest
{
    public required string Method { get; init; }

    /// <summary>The request target as sent (origin-form: path and query, still percent-encoded).</summary>
    public required string Target { get; init; }

    /// <summary>The percent-encoded path part of the target ("/api/players/7").</summary>
    public required string Path { get; init; }

    /// <summary>The query without '?', still encoded ("" when none). Parse with <see cref="QueryString.Parse"/>.</summary>
    public string Query { get; init; } = "";

    /// <summary>1 for HTTP/1.1, 0 for HTTP/1.0.</summary>
    public int MinorVersion { get; init; } = 1;

    public HttpHeaders Headers { get; init; } = new();

    /// <summary>The Host header (required on HTTP/1.1; "" on an HTTP/1.0 request without one).</summary>
    public string Host { get; init; } = "";

    /// <summary>The declared Content-Length (0 when none).</summary>
    public long ContentLength { get; init; }

    public ReadOnlyMemory<byte> Body { get; set; }

    public bool IsGetOrHead => Method is "GET" or "HEAD";

    /// <summary>GET, HEAD, OPTIONS and TRACE: methods that must not change anything (CSRF checks skip them).</summary>
    public bool IsSafeMethod => Method is "GET" or "HEAD" or "OPTIONS" or "TRACE";

    /// <summary>Whether the client allows the connection to stay open after this request.</summary>
    public bool KeepAlive =>
        MinorVersion >= 1 ? !Headers.HasToken("Connection", "close") : Headers.HasToken("Connection", "keep-alive");

    public bool IsWebSocketUpgrade =>
        Method == "GET" && Headers.HasToken("Upgrade", "websocket") && Headers.HasToken("Connection", "upgrade");

    private IReadOnlyDictionary<string, string>? _query;

    /// <summary>The query's first value per name, decoded (lazy).</summary>
    public IReadOnlyDictionary<string, string> QueryValues => _query ??= QueryString.Parse(Query);
}
