// SPDX-License-Identifier: MIT
using System.Text;

namespace Kilnwarden.Http;

/// <summary>The Cookie request header (RFC 6265 §4.2.1, read leniently in the way browsers send it but never past the
/// caps): name=value pairs separated by ';'. Names are case-sensitive; the first of a repeated name wins (the browser
/// sends the most specific path first). Values keep their bytes (no percent-decoding); a value in double quotes loses
/// them. Pairs with a bad name or a bad value byte are skipped, not fatal.</summary>
public static class Cookies
{
    public const int MaxCookies = 64;

    public static IReadOnlyDictionary<string, string> Parse(string? header)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(header)) return result;
        foreach (var part in header.Split(';'))
        {
            if (result.Count >= MaxCookies) break;
            var eq = part.IndexOf('=');
            if (eq <= 0) continue;
            var name = part[..eq].Trim(' ', '\t');
            var value = part[(eq + 1)..].Trim(' ', '\t');
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') value = value[1..^1];
            if (!IsToken(name) || !IsCookieValue(value)) continue;
            result.TryAdd(name, value);
        }
        return result;
    }

    /// <summary>RFC 9110 token: what a cookie name may be.</summary>
    public static bool IsToken(ReadOnlySpan<char> s)
    {
        if (s.IsEmpty) return false;
        foreach (var c in s)
            if (!HttpSyntax.IsTokenChar(c)) return false;
        return true;
    }

    /// <summary>RFC 6265 cookie-octet: printable ASCII except space, '"', ',', ';' and '\'.</summary>
    public static bool IsCookieValue(ReadOnlySpan<char> s)
    {
        foreach (var c in s)
            if (c < 0x21 || c > 0x7E || c is '"' or ',' or ';' or '\\') return false;
        return true;
    }
}

public enum SameSite
{
    Strict,
    Lax,
    None,
}

/// <summary>A Set-Cookie value. Defaults are the session cookie's: Secure, HttpOnly, SameSite=Strict, Path=/.
/// Prefer a "__Host-" name on HTTPS (it forces Secure, Path=/ and no Domain in the browser).</summary>
public sealed record SetCookie(string Name, string Value)
{
    public string Path { get; init; } = "/";
    public bool Secure { get; init; } = true;
    public bool HttpOnly { get; init; } = true;
    public SameSite SameSite { get; init; } = SameSite.Strict;

    /// <summary>Null: a session cookie (gone when the browser closes).</summary>
    public TimeSpan? MaxAge { get; init; }

    /// <summary>A cookie that deletes <paramref name="name"/> (Max-Age=0).</summary>
    public static SetCookie Delete(string name, bool secure = true) => new(name, "") { MaxAge = TimeSpan.Zero, Secure = secure };

    public override string ToString()
    {
        if (!Cookies.IsToken(Name)) throw new ArgumentException($"bad cookie name '{Name}'");
        if (!Cookies.IsCookieValue(Value)) throw new ArgumentException($"bad value for cookie '{Name}'");
        var sb = new StringBuilder(Name).Append('=').Append(Value).Append("; Path=").Append(Path);
        if (MaxAge is { } age) sb.Append("; Max-Age=").Append((long)Math.Max(0, age.TotalSeconds));
        if (Secure) sb.Append("; Secure");
        if (HttpOnly) sb.Append("; HttpOnly");
        sb.Append("; SameSite=").Append(SameSite);
        return sb.ToString();
    }
}
