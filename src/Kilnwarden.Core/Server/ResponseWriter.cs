// SPDX-License-Identifier: MIT
using System.Buffers;
using System.Globalization;
using System.Text;
using Kilnwarden.Http;

namespace Kilnwarden.Server;

/// <summary>Writes a response: status line, the handler's headers (checked: a CR or LF in a header is a bug, never sent),
/// Content-Length, Connection, Date, then the body. Small responses go out in one write (one TLS record).</summary>
internal static class ResponseWriter
{
    private const int Coalesce = 32 * 1024;

    public static async Task WriteAsync(Stream s, HttpResponse r, bool keepAlive, bool headRequest, CancellationToken ct)
    {
        var bodyless = r.Status is < 200 or 204 or 304;
        var length = r.BodyWriter is not null ? r.BodyLength : r.Body.Length;
        var sb = new StringBuilder(256);
        sb.Append("HTTP/1.1 ").Append(r.Status.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(HttpResponse.ReasonPhrase(r.Status)).Append("\r\n");
        foreach (var (name, value) in r.Headers)
        {
            if (name is "Content-Length" or "Connection" or "Date" or "Transfer-Encoding") continue;
            Check(name, value);
            sb.Append(name).Append(": ").Append(value).Append("\r\n");
        }
        if (!bodyless) sb.Append("Content-Length: ").Append(length.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        if (r.Status >= 200) sb.Append("Connection: ").Append(keepAlive ? "keep-alive" : "close").Append("\r\n");
        sb.Append("Date: ").Append(HttpDate.Now).Append("\r\n\r\n");
        var headLen = Encoding.Latin1.GetByteCount(sb.ToString());
        var withBody = !bodyless && !headRequest && r.BodyWriter is null && headLen + r.Body.Length <= Coalesce;
        var buf = ArrayPool<byte>.Shared.Rent(headLen + (withBody ? r.Body.Length : 0));
        try
        {
            Encoding.Latin1.GetBytes(sb.ToString(), buf);
            var total = headLen;
            if (withBody)
            {
                r.Body.Span.CopyTo(buf.AsSpan(headLen));
                total += r.Body.Length;
            }
            await s.WriteAsync(buf.AsMemory(0, total), ct).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }
        if (!bodyless && !headRequest && !withBody)
        {
            if (r.BodyWriter is { } w) await w(s, ct).ConfigureAwait(false);
            else if (!r.Body.IsEmpty) await s.WriteAsync(r.Body, ct).ConfigureAwait(false);
        }
        await s.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>A plain-text error written by the connection itself (before any handler): closes afterwards.</summary>
    public static Task WriteErrorAsync(Stream s, int status, string message, bool keepAlive, CancellationToken ct)
    {
        var r = HttpResponse.Text(status, message + "\n");
        SecurityHeaders.Apply(r, https: false, host: "", hsts: false);
        return WriteAsync(s, r, keepAlive, headRequest: false, ct);
    }

    private static void Check(string name, string value)
    {
        foreach (var c in name)
            if (!HttpSyntax.IsTokenChar(c)) throw new InvalidOperationException($"bad response header name '{name}'");
        foreach (var c in value)
            if (c is '\r' or '\n' || c != '\t' && c < 0x20 || c is '\x7f' || c > 0xFF)
                throw new InvalidOperationException($"bad value in response header '{name}'");
    }
}

/// <summary>The Date header (RFC 9110 IMF-fixdate), computed at most once a second.</summary>
internal static class HttpDate
{
    private static long _second = -1;
    private static string _value = "";

    public static string Now
    {
        get
        {
            var now = DateTimeOffset.UtcNow;
            var sec = now.ToUnixTimeSeconds();
            if (Interlocked.Read(ref _second) != sec)
            {
                Volatile.Write(ref _value, now.ToString("r", CultureInfo.InvariantCulture));
                Interlocked.Exchange(ref _second, sec);
            }
            return Volatile.Read(ref _value);
        }
    }
}
