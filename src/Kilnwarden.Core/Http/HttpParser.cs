// SPDX-License-Identifier: MIT
using System.Text;

namespace Kilnwarden.Http;

public enum ParseStatus
{
    Complete,
    Incomplete,
}

/// <summary>The parser's caps (a snapshot of settings.json limits).</summary>
public readonly record struct HttpLimits(int MaxHeaderBytes, int MaxHeaderCount, int MaxTargetBytes)
{
    public static HttpLimits Default { get; } = new(16 * 1024, 100, 8 * 1024);
}

/// <summary>The strict HTTP/1.1 request-head parser (RFC 9112), a pure function over bytes (fuzz target).
/// <list type="bullet">
/// <item>Lines end in CRLF only; a bare CR or LF is a 400. Blank lines before the request line are skipped.</item>
/// <item>Request line: token method (GET HEAD POST PUT PATCH DELETE OPTIONS; any other token 501), one space, an
/// origin-form target of visible ASCII without '#' (else 400; longer than MaxTargetBytes 414), one space, HTTP/1.1 or
/// HTTP/1.0 (another HTTP/d.d 505).</item>
/// <item>Fields: token name, ':' right after it, optional whitespace around a value of visible characters, spaces and
/// tabs (obs-text read as Latin-1). No obs-fold. At most MaxHeaderCount fields; the head (from the request line to the
/// blank line) at most MaxHeaderBytes, else 431, even before it is complete.</item>
/// <item>Host: exactly one on HTTP/1.1, at most one on 1.0, a plausible host[:port] (no '/', '@', spaces).</item>
/// <item>Framing (no request smuggling): Transfer-Encoding is not supported (501; with Content-Length 400); one
/// Content-Length of 1 to 18 digits; Expect other than 100-continue 417.</item>
/// </list>
/// Failures throw <see cref="HttpError"/> (the connection answers and closes); nothing else is thrown.</summary>
public static class HttpParser
{
    private static readonly string[] Methods = ["GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"];

    /// <summary>Parses one request head at the start of <paramref name="buffer"/>. Complete: <paramref name="request"/> is
    /// set and <paramref name="consumed"/> is the head's length (blank lines before it included); the body (if any) starts
    /// there. Incomplete: read more and call again with the longer buffer.</summary>
    public static ParseStatus TryParseHead(ReadOnlySpan<byte> buffer, HttpLimits limits, out HttpRequest? request, out int consumed)
    {
        request = null;
        consumed = 0;
        var skip = 0;
        while (skip < buffer.Length)
        {
            if (buffer[skip] == '\r')
            {
                if (skip + 1 >= buffer.Length) return ParseStatus.Incomplete;
                if (buffer[skip + 1] != '\n') throw new HttpError(400, "bare CR");
                skip += 2;
            }
            else if (buffer[skip] == '\n') throw new HttpError(400, "bare LF");
            else break;
            if (skip > limits.MaxHeaderBytes) throw new HttpError(431, "too many blank lines");
        }
        var head = buffer[skip..];
        var end = head.IndexOf("\r\n\r\n"u8);
        if (end < 0)
        {
            // still arriving: refuse early what can only end over a cap
            var firstLine = head.IndexOf("\r\n"u8);
            if (firstLine < 0 && head.Length > limits.MaxTargetBytes + 32) throw new HttpError(414, "request target too long");
            if (skip + head.Length > limits.MaxHeaderBytes) throw new HttpError(431, "request head too large");
            // a bare LF or CR can never end the head: refuse now instead of waiting for the deadline
            for (var i = 0; i < head.Length; i++)
            {
                if (head[i] == '\n' && (i == 0 || head[i - 1] != '\r')) throw new HttpError(400, "bare LF");
                if (head[i] == '\r' && i + 1 < head.Length && head[i + 1] != '\n') throw new HttpError(400, "bare CR");
            }
            return ParseStatus.Incomplete;
        }
        var headLen = end + 4;
        if (skip + headLen > limits.MaxHeaderBytes) throw new HttpError(431, "request head too large");
        head = head[..(end + 2)]; // the lines, each with its CRLF

        // the request line
        var eol = head.IndexOf("\r\n"u8);
        var line = head[..eol];
        CheckNoBareCrLf(line);
        var sp1 = line.IndexOf((byte)' ');
        if (sp1 <= 0) throw new HttpError(400, "bad request line");
        var sp2 = line[(sp1 + 1)..].IndexOf((byte)' ');
        if (sp2 < 0) throw new HttpError(400, "bad request line");
        sp2 += sp1 + 1;
        var methodBytes = line[..sp1];
        var target = line[(sp1 + 1)..sp2];
        var version = line[(sp2 + 1)..];
        if (methodBytes.Length > 16) throw new HttpError(501, "method not implemented");
        foreach (var b in methodBytes)
            if (!HttpSyntax.IsTokenChar(b)) throw new HttpError(400, "bad method");
        var method = Encoding.ASCII.GetString(methodBytes);
        if (target.Length > limits.MaxTargetBytes) throw new HttpError(414, "request target too long");
        if (target.Length == 0 || target[0] != '/') throw new HttpError(400, "the request target must be a path");
        foreach (var b in target)
            if (!HttpSyntax.IsTargetChar(b) || b == '#') throw new HttpError(400, "bad request target");
        var minor = ParseVersion(version);
        if (Array.IndexOf(Methods, method) < 0) throw new HttpError(501, "method not implemented");

        // the fields
        var headers = new HttpHeaders();
        var rest = head[(eol + 2)..];
        while (!rest.IsEmpty)
        {
            var e = rest.IndexOf("\r\n"u8);
            var field = rest[..e];
            rest = rest[(e + 2)..];
            CheckNoBareCrLf(field);
            if (headers.Count >= limits.MaxHeaderCount) throw new HttpError(431, "too many header fields");
            if (field.Length == 0 || field[0] is (byte)' ' or (byte)'\t') throw new HttpError(400, "folded or empty header line");
            var colon = field.IndexOf((byte)':');
            if (colon <= 0) throw new HttpError(400, "bad header field");
            var name = field[..colon];
            foreach (var b in name)
                if (!HttpSyntax.IsTokenChar(b)) throw new HttpError(400, "bad header name");
            var value = field[(colon + 1)..];
            foreach (var b in value)
                if (!HttpSyntax.IsFieldValueChar(b)) throw new HttpError(400, "bad header value");
            value = Trim(value);
            headers.Add(Encoding.ASCII.GetString(name), Encoding.Latin1.GetString(value));
        }

        // Host
        var hostCount = headers.CountOf("Host");
        if (hostCount > 1 || minor == 1 && hostCount == 0) throw new HttpError(400, "exactly one Host header is required");
        var host = headers.Get("Host") ?? "";
        if ((hostCount == 1 || minor == 1) && !IsHost(host)) throw new HttpError(400, "bad Host header");

        // framing
        var te = headers.Contains("Transfer-Encoding");
        var clCount = headers.CountOf("Content-Length");
        if (te && clCount > 0) throw new HttpError(400, "both Transfer-Encoding and Content-Length");
        if (te) throw new HttpError(501, "Transfer-Encoding is not supported: send Content-Length");
        if (clCount > 1) throw new HttpError(400, "more than one Content-Length");
        long length = 0;
        if (clCount == 1 && !TryParseLength(headers.Get("Content-Length")!, out length)) throw new HttpError(400, "bad Content-Length");
        if (headers.Get("Expect") is { } expect && !expect.Equals("100-continue", StringComparison.OrdinalIgnoreCase))
            throw new HttpError(417, "only Expect: 100-continue is supported");

        var targetText = Encoding.ASCII.GetString(target);
        var q = targetText.IndexOf('?');
        request = new HttpRequest
        {
            Method = method,
            Target = targetText,
            Path = q < 0 ? targetText : targetText[..q],
            Query = q < 0 ? "" : targetText[(q + 1)..],
            MinorVersion = minor,
            Headers = headers,
            Host = host,
            ContentLength = length,
        };
        consumed = skip + headLen;
        return ParseStatus.Complete;
    }

    /// <summary>Whether the client waits for "100 Continue" before sending its body.</summary>
    public static bool ExpectsContinue(HttpRequest request) =>
        request.MinorVersion >= 1 && request.ContentLength > 0 &&
        string.Equals(request.Headers.Get("Expect"), "100-continue", StringComparison.OrdinalIgnoreCase);

    private static int ParseVersion(ReadOnlySpan<byte> v)
    {
        if (v.Length != 8 || !v.StartsWith("HTTP/"u8) || v[6] != '.' || !IsDigit(v[5]) || !IsDigit(v[7]))
            throw new HttpError(400, "bad HTTP version");
        if (v[5] != '1' || v[7] is not ((byte)'0' or (byte)'1')) throw new HttpError(505, "only HTTP/1.1 and HTTP/1.0 are supported");
        return v[7] - '0';
    }

    private static bool TryParseLength(string s, out long n)
    {
        n = 0;
        if (s.Length is 0 or > 18) return false;
        foreach (var c in s)
        {
            if (c is < '0' or > '9') return false;
            n = n * 10 + (c - '0');
        }
        return true;
    }

    /// <summary>host[:port]: a reg-name or IPv4 (unreserved, sub-delims, pct-encoded) or an [IPv6] literal; port digits.</summary>
    internal static bool IsHost(string h)
    {
        if (h.Length is 0 or > 255) return false;
        var hostPart = h;
        if (h[0] == '[')
        {
            var close = h.IndexOf(']');
            if (close < 2) return false;
            foreach (var c in h.AsSpan(1, close - 1))
                if (!(char.IsAsciiHexDigit(c) || c is ':' or '.')) return false;
            var after = h[(close + 1)..];
            return after.Length == 0 || after[0] == ':' && IsPort(after[1..]);
        }
        var colon = h.LastIndexOf(':');
        if (colon >= 0)
        {
            if (!IsPort(h[(colon + 1)..])) return false;
            hostPart = h[..colon];
        }
        if (hostPart.Length == 0) return false;
        foreach (var c in hostPart)
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~' or '%' or '!' or '$' or '&' or '\'' or '(' or ')' or '*' or '+' or ',' or ';' or '='))
                return false;
        return true;
    }

    private static bool IsPort(string p) => p.Length is >= 1 and <= 5 && p.All(char.IsAsciiDigit);

    private static void CheckNoBareCrLf(ReadOnlySpan<byte> line)
    {
        if (line.IndexOfAny((byte)'\r', (byte)'\n') >= 0) throw new HttpError(400, "bare CR or LF");
    }

    private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> v)
    {
        var a = 0;
        var b = v.Length;
        while (a < b && v[a] is (byte)' ' or (byte)'\t') a++;
        while (b > a && v[b - 1] is (byte)' ' or (byte)'\t') b--;
        return v[a..b];
    }

    private static bool IsDigit(byte b) => b is >= (byte)'0' and <= (byte)'9';
}
