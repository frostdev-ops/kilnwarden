// SPDX-License-Identifier: MIT
using System.Security.Cryptography;
using Kilnwarden.Hosting;

namespace Kilnwarden.Branding;

/// <summary>One branding image as served: its exact content type, bytes and ETag.</summary>
public sealed record BrandFile(string Name, string Extension, string ContentType, byte[] Bytes, string ETag);

/// <summary>Read-only, fixed-name raster branding assets, never active content. No request names a path.
/// Links, directories, devices, empty/special files and files over the size cap are refused.</summary>
public sealed class BrandingFiles(string folder, IWebAdminLog log)
{
    public const int MaxBytes = 512 * 1024;

    public static readonly IReadOnlyList<string> LogoNames = ["logo.png", "logo.webp", "logo.ico"];
    public static readonly IReadOnlyList<string> FaviconNames = ["favicon.ico", "favicon.png", "favicon.webp"];

    private readonly object _gate = new();
    private readonly HashSet<string> _warned = new(StringComparer.Ordinal);

    public string Folder { get; } = Path.GetFullPath(folder);

    /// <summary>The file for <paramref name="configured"/> (a name from branding.json, already validated) or the first of
    /// <paramref name="fixedNames"/> that is there and good; null when none is.</summary>
    public BrandFile? Find(string? configured, IReadOnlyList<string> fixedNames)
    {
        if (configured is not null) return fixedNames.Contains(configured, StringComparer.Ordinal) ? Load(configured) : null;
        foreach (var n in fixedNames)
            if (Load(n) is { } f) return f;
        return null;
    }

    private BrandFile? Load(string name)
    {
        var path = Path.Combine(Folder, name);
        // belt and braces: the name rules already keep it a plain name
        if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), Folder, StringComparison.Ordinal)) return null;
        FileInfo fi;
        try
        {
            var directory = new DirectoryInfo(Folder);
            if (!directory.Exists || directory.LinkTarget is not null || (directory.Attributes & FileAttributes.ReparsePoint) != 0) return null;
            fi = new FileInfo(path);
            if (!fi.Exists) return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
        return Read(fi, name);
    }

    private BrandFile? Read(FileInfo fi, string name)
    {
        var ext = Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
        if (fi.LinkTarget is not null || (fi.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory | FileAttributes.Device)) != 0) return Refuse(name, "it is a link");
        if (fi.Length < 6) return Refuse(name, "it is empty or not a regular image file");
        if (fi.Length > MaxBytes) return Refuse(name, $"it is larger than {MaxBytes / 1024} KiB");
        byte[] bytes;
        try
        {
            using var fs = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (fs.Length > MaxBytes) return Refuse(name, $"it is larger than {MaxBytes / 1024} KiB");
            bytes = new byte[fs.Length];
            fs.ReadExactly(bytes);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Refuse(name, e.Message);
        }
        if (!Matches(ext, bytes)) return Refuse(name, $"its content is not a {ext.ToUpperInvariant()} image");
        var etag = "\"" + Convert.ToHexStringLower(SHA256.HashData(bytes))[..16] + "\"";
        return new BrandFile(name, ext, ContentType(ext), bytes, etag);
    }

    private BrandFile? Refuse(string name, string why)
    {
        bool first;
        lock (_gate) first = _warned.Add(name + "|" + why);
        if (first) log.Warn($"branding: not serving branding/{name}: {why}");
        return null;
    }

    public static string ContentType(string ext) => ext switch
    {
        "png" => "image/png",
        "webp" => "image/webp",
        "ico" => "image/x-icon",
        _ => throw new ArgumentException("not a branding image type: " + ext),
    };

    /// <summary>Whether <paramref name="bytes"/> are the kind <paramref name="ext"/> names (fuzz target).</summary>
    public static bool Matches(string ext, ReadOnlySpan<byte> bytes) => ext switch
    {
        "png" => bytes.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) && bytes.Length > 8,
        "ico" => bytes.Length >= 6 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 1 && bytes[3] == 0 && (bytes[4] | bytes[5]) != 0,
        "webp" => bytes.Length >= 12 && bytes.StartsWith("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8),
        _ => false,
    };

}
