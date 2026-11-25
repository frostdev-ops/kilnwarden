// SPDX-License-Identifier: MIT
namespace Kilnwarden.Http;

/// <summary>Character classes of RFC 9110/9112.</summary>
public static class HttpSyntax
{
    /// <summary>tchar: "!#$%&amp;'*+-.^_`|~", digits, letters.</summary>
    public static bool IsTokenChar(int c) =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or
            '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~';

    /// <summary>field-vchar, SP or HTAB (obs-text 0x80-0xFF allowed, read as Latin-1); no other control bytes.</summary>
    public static bool IsFieldValueChar(int c) => c == '\t' || c >= 0x20 && c != 0x7F && c <= 0xFF;

    /// <summary>What a request target may hold: visible ASCII (0x21-0x7E).</summary>
    public static bool IsTargetChar(int c) => c is >= 0x21 and <= 0x7E;
}
