// SPDX-License-Identifier: MIT
using System.Text.RegularExpressions;

namespace Kilnwarden.Branding;

/// <summary>branding.json in the data folder (hand-edited for now; the Panel settings page edits it in step 2): what the
/// web app shows before and after sign-in. Every field is optional. The files are plain names inside branding/ (no
/// folders, no "..", an image extension); without one the fixed names logo.{png,webp,ico} and
/// favicon.{ico,png,webp} are looked for.</summary>
public sealed class BrandingSettings
{
    public int Format { get; set; } = 1;

    /// <summary>The server's name in the panel (else enshrouded_server.json's "name", else the product name).</summary>
    public string? Name { get; set; }

    /// <summary>The accent colour, "#rrggbb".</summary>
    public string? Accent { get; set; }

    /// <summary>A file name in branding/ for the logo.</summary>
    public string? Logo { get; set; }

    /// <summary>A file name in branding/ for the favicon.</summary>
    public string? Favicon { get; set; }
}

/// <summary>The checks of branding.json (a file that fails any of them is not loaded; the last good one stays).</summary>
public static partial class BrandingRules
{
    public const int MaxName = 64;

    public static IReadOnlyList<string> Validate(BrandingSettings s)
    {
        var p = new List<string>();
        if (s.Format != 1) p.Add($"format {s.Format} is not supported (1)");
        if (s.Name is not null && CleanName(s.Name) is null) p.Add($"name: 1 to {MaxName} characters, no control characters");
        if (s.Accent is not null && !IsAccent(s.Accent)) p.Add("accent: a colour like #ff6a1a (# and six hex digits)");
        if (s.Logo is not null && !BrandingFiles.LogoNames.Contains(s.Logo, StringComparer.Ordinal)) p.Add("logo: a fixed logo.png/logo.webp/logo.ico file in branding/");
        if (s.Favicon is not null && !BrandingFiles.FaviconNames.Contains(s.Favicon, StringComparer.Ordinal)) p.Add("favicon: a fixed favicon.ico/favicon.png/favicon.webp file in branding/");
        return p;
    }

    /// <summary>Exactly "#" and six hex digits.</summary>
    public static bool IsAccent(string? s) => s is not null && AccentRe().IsMatch(s);

    /// <summary>A plain file name in branding/: letters, digits, "_", "-" and dots between parts, ending in an allowed
    /// image extension; at most 64 characters.</summary>
    public static bool IsFileName(string? s) => s is not null && (BrandingFiles.LogoNames.Contains(s, StringComparer.Ordinal) || BrandingFiles.FaviconNames.Contains(s, StringComparer.Ordinal));

    /// <summary>The name trimmed, or null when it is empty, too long or has control characters.</summary>
    public static string? CleanName(string? s)
    {
        var t = s?.Trim();
        if (string.IsNullOrEmpty(t) || t.Length > MaxName) return null;
        foreach (var c in t)
            if (char.IsControl(c) || c is '\u2028' or '\u2029' or '\ufeff') return null;
        // lone surrogates make invalid UTF-8
        for (var i = 0; i < t.Length; i++)
        {
            if (char.IsHighSurrogate(t[i]))
            {
                if (i + 1 >= t.Length || !char.IsLowSurrogate(t[i + 1])) return null;
                i++;
            }
            else if (char.IsLowSurrogate(t[i])) return null;
        }
        return t;
    }

    [GeneratedRegex(@"\A#[0-9A-Fa-f]{6}\z", RegexOptions.CultureInvariant)]
    private static partial Regex AccentRe();

}
