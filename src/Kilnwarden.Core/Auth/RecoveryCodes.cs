// SPDX-License-Identifier: MIT
using System.Security.Cryptography;
using System.Text;

namespace Kilnwarden.Auth;

/// <summary>Two-factor recovery codes: 10 single-use codes of 10 characters (50 random bits each, "ABCDE-FGHJK" from
/// an alphabet without look-alikes). Only their SHA-256 is stored: they are random enough that a slow hash adds
/// nothing.</summary>
public static class RecoveryCodes
{
    public const int Count = 10, Length = 10;
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // 32 symbols: no I, O, 0, 1

    /// <summary>New codes (shown once) and their stored hashes.</summary>
    public static (IReadOnlyList<string> Codes, List<string> Hashes) Generate()
    {
        var codes = new List<string>(Count);
        for (var i = 0; i < Count; i++)
        {
            var sb = new StringBuilder(Length + 1);
            for (var j = 0; j < Length; j++)
            {
                if (j == Length / 2) sb.Append('-');
                sb.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
            }
            codes.Add(sb.ToString());
        }
        return (codes, [.. codes.Select(c => Hash(Normalize(c)!))]);
    }

    /// <summary>Upper case without '-' and spaces; null when it cannot be a code (fuzz target).</summary>
    public static string? Normalize(string? code)
    {
        if (code is null || code.Length > 32) return null;
        var sb = new StringBuilder(Length);
        foreach (var c in code)
        {
            if (c is '-' or ' ') continue;
            var u = char.ToUpperInvariant(c);
            if (Alphabet.IndexOf(u) < 0 || sb.Length == Length) return null;
            sb.Append(u);
        }
        return sb.Length == Length ? sb.ToString() : null;
    }

    public static string Hash(string normalized) => Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(normalized)));

    /// <summary>The index of the stored hash <paramref name="code"/> matches (every one is compared in constant time), or -1.</summary>
    public static int Match(IReadOnlyList<string> hashes, string? code)
    {
        var n = Normalize(code);
        var given = Encoding.ASCII.GetBytes(n is null ? new string('-', 64) : Hash(n));
        var hit = -1;
        for (var i = 0; i < hashes.Count; i++)
            if (CryptographicOperations.FixedTimeEquals(given, Encoding.ASCII.GetBytes(hashes[i])) && hit < 0 && n is not null) hit = i;
        return hit;
    }
}

/// <summary>Opaque random tokens (session ids, setup and two-factor tickets): 32 bytes from the OS CSPRNG as base64url
/// (43 characters). The server keeps only their SHA-256, so a copy of its files holds no live token.</summary>
public static class SecretToken
{
    public const int Bytes = 32, TextLength = 43;

    public static string New() => System.Buffers.Text.Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(Bytes));

    /// <summary>Whether <paramref name="s"/> has a token's shape (fuzz target): exactly 43 base64url characters.</summary>
    public static bool IsWellFormed(string? s)
    {
        if (s is null || s.Length != TextLength) return false;
        foreach (var c in s)
            if (!(c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_')) return false;
        return true;
    }

    /// <summary>The key a token is stored under: SHA-256 hex. Call only with a well-formed token.</summary>
    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(token)));

    /// <summary>Constant-time equality of two hashes.</summary>
    public static bool HashEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));
}
