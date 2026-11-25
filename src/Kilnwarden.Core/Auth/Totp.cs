// SPDX-License-Identifier: MIT
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Kilnwarden.Auth;

/// <summary>Time-based one-time passwords (RFC 6238 over RFC 4226): HMAC-SHA1, 6 digits, 30-second steps, one step of
/// clock drift either way. A step that was used once is refused after (the caller keeps the last one per account), so a
/// code seen over a shoulder cannot be replayed.</summary>
public static class Totp
{
    public const int Digits = 6, StepSeconds = 30, SecretBytes = 20, Window = 1;

    public static byte[] NewSecret() => RandomNumberGenerator.GetBytes(SecretBytes);

    public static long StepAt(DateTimeOffset t) => t.ToUnixTimeSeconds() / StepSeconds;

    /// <summary>The code for <paramref name="step"/> (RFC 4226 dynamic truncation).</summary>
    public static string Code(ReadOnlySpan<byte> secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        Span<byte> mac = stackalloc byte[20];
        HMACSHA1.HashData(secret, counter, mac);
        var off = mac[19] & 0x0F;
        var bin = (mac[off] & 0x7F) << 24 | mac[off + 1] << 16 | mac[off + 2] << 8 | mac[off + 3];
        return (bin % 1_000_000).ToString("D6");
    }

    /// <summary>The step <paramref name="code"/> belongs to (within the window, later than <paramref name="lastUsedStep"/>),
    /// or null. Every candidate is compared, in constant time each.</summary>
    public static long? Verify(ReadOnlySpan<byte> secret, string? code, DateTimeOffset now, long lastUsedStep)
    {
        if (!TryNormalize(code, out var digits)) return null;
        var given = Encoding.ASCII.GetBytes(digits);
        var current = StepAt(now);
        long? hit = null;
        for (var s = current - Window; s <= current + Window; s++)
        {
            var match = CryptographicOperations.FixedTimeEquals(given, Encoding.ASCII.GetBytes(Code(secret, s)));
            if (match && s > lastUsedStep && hit is null) hit = s;
        }
        return hit;
    }

    /// <summary>Six digits, spaces and dashes ignored (fuzz target).</summary>
    public static bool TryNormalize(string? code, out string digits)
    {
        digits = "";
        if (code is null || code.Length > 16) return false;
        var sb = new StringBuilder(Digits);
        foreach (var c in code)
        {
            if (c is ' ' or '-') continue;
            if (c is < '0' or > '9' || sb.Length == Digits) return false;
            sb.Append(c);
        }
        if (sb.Length != Digits) return false;
        digits = sb.ToString();
        return true;
    }

    /// <summary>The otpauth:// URI authenticator apps read (from a QR code the web app draws).</summary>
    public static string Uri(byte[] secret, string issuer, string account) =>
        $"otpauth://totp/{System.Uri.EscapeDataString(issuer)}:{System.Uri.EscapeDataString(account)}?secret={Base32.Encode(secret)}" +
        $"&issuer={System.Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";
}

/// <summary>RFC 4648 base32 (A-Z 2-7), no padding: how authenticator apps take TOTP secrets.</summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = buffer << 8 | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(Alphabet[buffer >> (bits - 5) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0) sb.Append(Alphabet[buffer << (5 - bits) & 31]);
        return sb.ToString();
    }

    /// <summary>Decodes (case-insensitive, '=' padding and spaces ignored); null for a bad character (fuzz target).</summary>
    public static byte[]? Decode(string? s)
    {
        if (s is null || s.Length > 1024) return null;
        var output = new List<byte>(s.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (var ch in s)
        {
            if (ch is '=' or ' ') continue;
            var v = Alphabet.IndexOf(char.ToUpperInvariant(ch));
            if (v < 0) return null;
            buffer = (buffer << 5 | v) & 0xFFFF;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }
        return [.. output];
    }
}
