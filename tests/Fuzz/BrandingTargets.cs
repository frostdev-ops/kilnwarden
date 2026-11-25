// SPDX-License-Identifier: MIT
using System.Text;
using System.Text.Json;
using Kilnwarden.Branding;
using Kilnwarden.Http;

namespace Kilnwarden.Fuzz;

/// <summary>The branding parsers: the accent and file-name rules, the image sniffing, and branding.json.</summary>
public static class BrandingTargets
{
    private static string Utf8(byte[] b) => Encoding.UTF8.GetString(b);

    public static readonly Target[] All =
    [
        new("branding-accent", Accent, [], ["#ff6a1a", "#FFFFFF", "#12345", "red"], ["#", "0", "f", "F", "g", " ", "\n", ";", "url(", "%23", "\0"]),
        new("branding-file-name", FileName, [], ["logo.png", "crest.v2.webp", "../x.png", "a/b.svg"],
            [".", "..", "/", "\\", "png", "svg", "webp", "ico", "html", "\0", "%2e", ":", "C:"]),
        new("branding-sniff", Sniff, [],
            ["\x89PNG\r\n\x1a\nIHDR", "\0\0\x01\0\x01\0", "RIFF\x10\0\0\0WEBPVP8 ", "<?xml version=\"1.0\"?><svg xmlns=\"http://www.w3.org/2000/svg\"/>",
             "<!-- c --><svg viewBox=\"0 0 1 1\"><rect/></svg>"],
            ["<svg", "<script", " onload=", "on", "=", "javascript:", "<!--", "-->", "<?xml", "?>", "<!DOCTYPE", "\xef\xbb\xbf", "RIFF", "WEBP", "\x89PNG"]),
        new("branding-json", Settings, [typeof(HttpError)],
            ["{\"name\":\"Embervale\",\"accent\":\"#ff6a1a\",\"logo\":\"logo.png\",\"favicon\":null}", "{\"format\":1}"],
            ["{", "}", "\"", ":", ",", "null", "\"name\"", "\"accent\"", "\"logo\"", "\"favicon\"", "\\u0000", "\\ud800", "\\u2028", "#"]),
    ];

    private static void Check(bool ok, string what)
    {
        if (!ok) throw new FuzzFinding(what);
    }

    private static void Accent(byte[] b)
    {
        var s = Utf8(b);
        var ok = BrandingRules.IsAccent(s);
        // the invariant the CSS depends on: an accepted accent is exactly # and six hex digits
        Check(ok == (s.Length == 7 && s[0] == '#' && s.Skip(1).All(char.IsAsciiHexDigit)), "accent rule disagrees with #rrggbb: " + s);
    }

    private static void FileName(byte[] b)
    {
        var s = Utf8(b);
        if (!BrandingRules.IsFileName(s)) return;
        Check(s.Length <= 64, "long name accepted");
        Check(!s.Contains('/') && !s.Contains('\\') && !s.Contains("..") && !s.Contains(':') && !s.StartsWith('.') && s.All(c => c < 128 && !char.IsControl(c)),
            "accepted a name that is not plain: " + s);
        var ext = Path.GetExtension(s).TrimStart('.').ToLowerInvariant();
        Check(ext is "png" or "webp" or "ico", "accepted extension " + ext);
        Check(Path.GetFileName(s) == s && !Path.IsPathRooted(s), "accepted a path: " + s);
    }

    private static void Sniff(byte[] b)
    {
        var kinds = new[] { "png", "ico", "webp", "svg" }.Where(k => BrandingFiles.Matches(k, b)).ToList();
        Check(kinds.Count <= 1, "bytes match more than one kind: " + string.Join(",", kinds));
        Check(!kinds.Contains("svg"), "SVG must never be served");
    }

    private static void Settings(byte[] b)
    {
        BrandingSettings s;
        try
        {
            s = JsonSerializer.Deserialize<BrandingSettings>(b, Storage.DataStore.Json) ?? new BrandingSettings();
        }
        catch (JsonException)
        {
            return;
        }
        if (BrandingRules.Validate(s).Count > 0) return;
        // what a valid file can put into the page: a clean name, a strict accent, plain file names
        if (s.Name is not null) Check(BrandingRules.CleanName(s.Name) is { } n && n.Length <= BrandingRules.MaxName && !n.Any(char.IsControl), "name");
        if (s.Accent is not null) Check(BrandingRules.IsAccent(s.Accent), "accent");
        if (s.Logo is not null) Check(BrandingRules.IsFileName(s.Logo), "logo");
        if (s.Favicon is not null) Check(BrandingRules.IsFileName(s.Favicon), "favicon");
    }
}
