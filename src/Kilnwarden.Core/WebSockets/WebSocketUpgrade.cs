// SPDX-License-Identifier: MIT
using System.Security.Cryptography;
using System.Text;
using Kilnwarden.Http;

namespace Kilnwarden.WebSockets;

/// <summary>The WebSocket opening handshake's checks (RFC 6455 §4.2.1), a pure function (fuzz target): GET on HTTP/1.1
/// with Upgrade: websocket and Connection: upgrade, version 13 (else 426 naming 13), a key of 16 base64 bytes, and an
/// Origin equal to the panel's own (cross-site WebSocket hijacking: the browser sends cookies with any origin's
/// upgrade). No subprotocol and no extension is negotiated.</summary>
public static class WebSocketUpgrade
{
    public readonly record struct Result(bool Ok, int Status, string Message, string Accept);

    public static Result Check(HttpRequest req, string expectedOrigin)
    {
        if (req.Method != "GET" || req.MinorVersion != 1) return Fail(400, "a WebSocket upgrade is a GET on HTTP/1.1");
        if (!req.IsWebSocketUpgrade) return Fail(400, "not a WebSocket upgrade");
        if (req.Headers.CountOf("Sec-WebSocket-Version") != 1 || req.Headers.Get("Sec-WebSocket-Version")!.Trim() != "13")
            return Fail(426, "WebSocket version 13 is required");
        if (req.Headers.CountOf("Sec-WebSocket-Key") != 1) return Fail(400, "one Sec-WebSocket-Key is required");
        var key = req.Headers.Get("Sec-WebSocket-Key")!.Trim();
        Span<byte> raw = stackalloc byte[18];
        if (key.Length != 24 || !Convert.TryFromBase64String(key, raw, out var n) || n != 16) return Fail(400, "bad Sec-WebSocket-Key");
        if (req.Headers.CountOf("Origin") != 1 || !SameOrigin(req.Headers.Get("Origin")!, expectedOrigin))
            return Fail(403, "cross-origin WebSocket refused");
        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        return new Result(true, 101, "", accept);
    }

    /// <summary>Origin equality as browsers serialize it: scheme and host compare case-insensitively, the port exactly.</summary>
    public static bool SameOrigin(string origin, string expected) =>
        origin.Length == expected.Length && string.Equals(origin, expected, StringComparison.OrdinalIgnoreCase);

    private static Result Fail(int status, string message) => new(false, status, message, "");
}
