// SPDX-License-Identifier: MIT
using System.Text;
using System.Text.Json;
using Kilnwarden.WebSockets;

namespace Kilnwarden.Http;

/// <summary>A response a handler returns. The connection adds Content-Length, Connection, Date and the security headers
/// (<c>SecurityHeaders</c>) when it writes it; API responses default to Cache-Control: no-store.</summary>
public sealed class HttpResponse
{
    public int Status { get; set; } = 200;
    public HttpHeaders Headers { get; } = new();

    /// <summary>The body when it is in memory (most responses).</summary>
    public ReadOnlyMemory<byte> Body { get; set; }

    /// <summary>A streamed body of <see cref="BodyLength"/> bytes (large static files). Null: <see cref="Body"/>.</summary>
    public Func<Stream, CancellationToken, Task>? BodyWriter { get; set; }

    public long BodyLength { get; set; }

    /// <summary>Close the connection after this response.</summary>
    public bool Close { get; set; }

    /// <summary>Set by <see cref="WebSocketAccept"/>: the connection answers 101 and hands the socket to the handler.</summary>
    internal WebSocketHandler? WebSocket { get; private init; }

    // enums as the names the data files use ("self-signed", "acme"), so an answer can be sent back as a request body
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    public static HttpResponse Bytes(int status, string contentType, ReadOnlyMemory<byte> body)
    {
        var r = new HttpResponse { Status = status, Body = body };
        r.Headers.Set("Content-Type", contentType);
        return r;
    }

    public static HttpResponse Text(int status, string text) => Bytes(status, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(text));

    public static HttpResponse Html(int status, string html) => Bytes(status, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(html));

    /// <summary>JSON with the web defaults (camelCase).</summary>
    public static HttpResponse Json<T>(T value, int status = 200) =>
        Bytes(status, "application/json; charset=utf-8", JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions));

    /// <summary>The API's one error shape: {"error": {"code": "...", "message": "..."}}.</summary>
    public static HttpResponse Error(int status, string code, string message) =>
        Json(new ApiErrorBody(new ApiError(code, message)), status);

    public static HttpResponse Empty(int status = 204) => new() { Status = status };

    public static HttpResponse NotFound() => Error(404, "not_found", "no such resource");

    public static HttpResponse Redirect(string location, int status = 303)
    {
        var r = new HttpResponse { Status = status };
        r.Headers.Set("Location", location);
        return r;
    }

    /// <summary>What a WebSocket route's terminal returns once the gate allowed it: the connection checks the upgrade
    /// headers (version 13, key, Origin), answers 101 and runs <paramref name="handler"/>.</summary>
    internal static HttpResponse WebSocketAccept(WebSocketHandler handler) => new() { Status = 101, WebSocket = handler };

    /// <summary>Adds a Set-Cookie field.</summary>
    public HttpResponse SetCookie(SetCookie cookie)
    {
        Headers.Add("Set-Cookie", cookie.ToString());
        return this;
    }

    public static string ReasonPhrase(int status) => status switch
    {
        100 => "Continue",
        101 => "Switching Protocols",
        200 => "OK",
        201 => "Created",
        202 => "Accepted",
        204 => "No Content",
        206 => "Partial Content",
        301 => "Moved Permanently",
        302 => "Found",
        303 => "See Other",
        304 => "Not Modified",
        307 => "Temporary Redirect",
        308 => "Permanent Redirect",
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        405 => "Method Not Allowed",
        408 => "Request Timeout",
        409 => "Conflict",
        411 => "Length Required",
        412 => "Precondition Failed",
        413 => "Content Too Large",
        414 => "URI Too Long",
        415 => "Unsupported Media Type",
        417 => "Expectation Failed",
        421 => "Misdirected Request",
        422 => "Unprocessable Content",
        426 => "Upgrade Required",
        429 => "Too Many Requests",
        431 => "Request Header Fields Too Large",
        500 => "Internal Server Error",
        501 => "Not Implemented",
        502 => "Bad Gateway",
        503 => "Service Unavailable",
        504 => "Gateway Timeout",
        505 => "HTTP Version Not Supported",
        _ => "Status",
    };
}

public sealed record ApiError(string Code, string Message);

public sealed record ApiErrorBody(ApiError Error);
