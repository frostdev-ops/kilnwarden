// SPDX-License-Identifier: MIT
using System.Text;

namespace Kilnwarden.Http;

/// <summary>application/x-www-form-urlencoded (the URL query, or a form body): '&amp;'-separated name=value pairs, '+'
/// is a space, %XX bytes form UTF-8. The first value of a name wins; at most <see cref="MaxPairs"/> pairs are read.
/// Malformed escapes or invalid UTF-8 make the pair unreadable: it is skipped.</summary>
public static class QueryString
{
    public const int MaxPairs = 64;

    public static IReadOnlyDictionary<string, string> Parse(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(query)) return result;
        var pairs = 0;
        foreach (var part in query.Split('&'))
        {
            if (part.Length == 0) continue;
            if (++pairs > MaxPairs) break;
            var eq = part.IndexOf('=');
            var rawName = eq < 0 ? part : part[..eq];
            var rawValue = eq < 0 ? "" : part[(eq + 1)..];
            if (TryDecode(rawName, plusIsSpace: true, out var name) && name.Length > 0 &&
                TryDecode(rawValue, plusIsSpace: true, out var value))
                result.TryAdd(name, value);
        }
        return result;
    }

    /// <summary>Percent-decodes <paramref name="s"/> as UTF-8. False on a bad escape or invalid UTF-8.</summary>
    public static bool TryDecode(string s, bool plusIsSpace, out string decoded)
    {
        if (s.IndexOf('%') < 0 && (!plusIsSpace || s.IndexOf('+') < 0) && System.Text.Ascii.IsValid(s))
        {
            decoded = s;
            return true;
        }
        var bytes = new byte[s.Length];
        var n = 0;
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '%')
            {
                if (i + 2 >= s.Length || !IsHex(s[i + 1]) || !IsHex(s[i + 2]))
                {
                    decoded = "";
                    return false;
                }
                bytes[n++] = (byte)(Hex(s[i + 1]) << 4 | Hex(s[i + 2]));
                i += 2;
            }
            else if (c == '+' && plusIsSpace) bytes[n++] = (byte)' ';
            else if (c > 0x7F)
            {
                decoded = "";
                return false;
            }
            else bytes[n++] = (byte)c;
        }
        try
        {
            decoded = Strict.GetString(bytes, 0, n);
            return true;
        }
        catch (DecoderFallbackException)
        {
            decoded = "";
            return false;
        }
    }

    private static readonly UTF8Encoding Strict = new(false, true);
    private static bool IsHex(char c) => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
    private static int Hex(char c) => c <= '9' ? c - '0' : (c | 0x20) - 'a' + 10;
}
