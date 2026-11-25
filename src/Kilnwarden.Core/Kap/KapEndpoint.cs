// SPDX-License-Identifier: MIT
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Kilnwarden.Kap;

/// <summary>Where Kiln.Admin listens and the key to talk to it (admin-protocol.md §1): kiln/data/kiln-admin/endpoint.json
/// and admin-token, read fresh at every connect (the port changes with a restart, the token with admin_token_rotate).
/// The client only ever connects to loopback: an endpoint file naming another host is refused, so the token's MAC never
/// leaves the machine.</summary>
public static class KapEndpoint
{
    public const string EndpointFile = "endpoint.json";
    public const string TokenFile = "admin-token";

    /// <summary>The loopback endpoint from endpoint.json. Throws <see cref="KapUnavailableException"/> when it is missing,
    /// unreadable, not kap/1 or not on loopback.</summary>
    public static IPEndPoint Read(string adminDirectory)
    {
        var path = Path.Combine(adminDirectory, EndpointFile);
        byte[] bytes;
        try
        {
            var fi = new FileInfo(path);
            if (!fi.Exists) throw new KapUnavailableException("Kiln.Admin's endpoint.json is not there (is kiln-admin loaded with KAP on?)");
            if (fi.Length > 64 << 10) throw new KapUnavailableException("endpoint.json is too large");
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new KapUnavailableException($"endpoint.json: {e.Message}");
        }
        return Parse(bytes);
    }

    /// <summary>endpoint.json's content (fuzz target): throws <see cref="KapUnavailableException"/> only.</summary>
    public static IPEndPoint Parse(ReadOnlySpan<byte> json)
    {
        JsonObject o;
        try
        {
            o = KapFrames.ParseObject(json);
        }
        catch (KapProtocolException e)
        {
            throw new KapUnavailableException("endpoint.json is not a JSON object: " + e.Message);
        }
        if (KapFrames.Str(o, "protocol") is { } proto && proto != "kap/1") throw new KapUnavailableException($"endpoint.json says {proto}, this client speaks kap/1");
        var host = KapFrames.Str(o, "host") ?? "127.0.0.1";
        var port = KapFrames.Int(o, "port") ?? 0;
        if (port is < 1 or > 65535) throw new KapUnavailableException("endpoint.json has no valid port");
        IPAddress addr = host switch
        {
            "127.0.0.1" or "localhost" => IPAddress.Loopback,
            "::1" or "[::1]" => IPAddress.IPv6Loopback,
            _ => IPAddress.TryParse(host, out var a) && IPAddress.IsLoopback(a) ? a : throw new KapUnavailableException($"endpoint.json names '{host}': only loopback is used"),
        };
        return new IPEndPoint(addr, port);
    }

    /// <summary>The 32-byte token from admin-token (64 hex characters, an optional trailing newline). The caller zeroes it.
    /// Throws <see cref="KapUnavailableException"/>.</summary>
    public static byte[] ReadToken(string adminDirectory)
    {
        var path = Path.Combine(adminDirectory, TokenFile);
        try
        {
            var fi = new FileInfo(path);
            if (!fi.Exists) throw new KapUnavailableException("Kiln.Admin's admin-token is not there");
            if (fi.Length > 256) throw new KapUnavailableException("admin-token is malformed");
            var raw = File.ReadAllBytes(path);
            try
            {
                return ParseToken(raw);
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(raw);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new KapUnavailableException($"admin-token: {e.Message}");
        }
    }

    /// <summary>The token file's bytes to the key (fuzz target): exactly 64 hex characters, then nothing or one newline
    /// ("\n" or "\r\n").</summary>
    public static byte[] ParseToken(ReadOnlySpan<byte> raw)
    {
        var len = raw.Length;
        if (len >= 1 && raw[len - 1] == '\n') len--;
        if (len >= 1 && raw[len - 1] == '\r') len--;
        if (len != 64) throw new KapUnavailableException("admin-token is malformed");
        var key = new byte[32];
        for (var i = 0; i < 32; i++)
        {
            var hi = Hex(raw[2 * i]);
            var lo = Hex(raw[2 * i + 1]);
            if (hi < 0 || lo < 0)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
                throw new KapUnavailableException("admin-token is malformed");
            }
            key[i] = (byte)(hi << 4 | lo);
        }
        return key;
    }

    private static int Hex(byte c) => c switch
    {
        >= (byte)'0' and <= (byte)'9' => c - '0',
        >= (byte)'a' and <= (byte)'f' => c - 'a' + 10,
        >= (byte)'A' and <= (byte)'F' => c - 'A' + 10,
        _ => -1,
    };
}

/// <summary>Kiln.Admin cannot be reached (yet): no endpoint, no token, connection refused, handshake failed.</summary>
public sealed class KapUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
