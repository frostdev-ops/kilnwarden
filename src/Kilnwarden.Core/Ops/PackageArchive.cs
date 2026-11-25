// SPDX-License-Identifier: MIT
using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Kilnwarden.Ops;

/// <summary>Size limits for one package (updates.json).</summary>
public sealed record ArchiveLimits(int MaxEntries, long MaxUnpackedBytes, long MaxEntryBytes, int MaxRatio = 200)
{
    public static ArchiveLimits Default { get; } = new(10_000, 512L << 20, 256L << 20);
}

/// <summary>A refused archive: the message says why (shown to the owner).</summary>
public sealed class ArchiveRefusedException(string message) : Exception(message);

/// <summary>What a checked package is.</summary>
/// <param name="Kind">mod (kiln.mod.json at its top) or kiln (kiln-runtime.json at its top: a Kiln runtime bundle).</param>
/// <param name="Prefix">The one wrapping folder that is removed ("" when none).</param>
/// <param name="Files">Each file's path after the prefix and its declared size.</param>
public sealed record ArchiveInfo(string Kind, string? Id, string? Name, string Version, string Prefix, IReadOnlyList<(string Path, long Size)> Files)
{
    public long Bytes => Files.Sum(f => f.Size);
}

/// <summary>Reads and unpacks mod and Kiln packages (zip) without trusting them (docs/step2-lane-b-ops.md "Archive rules";
/// the path rules are Kiln's staged-updates rules, so a package this accepts is one update_stage can take). Sizes are
/// counted on the bytes actually inflated, never on the headers.</summary>
public static partial class PackageArchive
{
    public const string ModManifest = "kiln.mod.json", KilnManifest = "kiln-runtime.json";
    public const int MaxManifestBytes = 64 << 10, MaxPathBytes = 240;

    private static readonly HashSet<string> Devices = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>Why <paramref name="path"/> (an entry name) is not acceptable, or null.</summary>
    public static string? PathProblem(string path)
    {
        if (path.Length == 0) return "an empty name";
        if (path.Contains('\\')) return "a backslash";
        if (path.StartsWith('/')) return "an absolute path";
        if (path.Length >= 2 && path[1] == ':') return "a drive letter";
        if (Encoding.UTF8.GetByteCount(path) > MaxPathBytes) return $"longer than {MaxPathBytes} bytes";
        foreach (var seg in path.Split('/'))
        {
            if (seg is "" or "." or "..") return "an empty, '.' or '..' part";
            foreach (var ch in seg)
                if (char.IsControl(ch) || ch is ':' or '*' or '?' or '"' or '<' or '>' or '|') return $"the character {(char.IsControl(ch) ? "U+" + ((int)ch).ToString("X4") : ch.ToString())}";
            if (seg.EndsWith('.') || seg.EndsWith(' ')) return "a part that ends in '.' or a space";
            var stem = seg.Split('.')[0];
            if (Devices.Contains(stem)) return $"the Windows device name {stem}";
            if (ShortName().IsMatch(seg)) return "a Windows 8.3 short name (like ABCDEF~1)";
        }
        return null;
    }

    [GeneratedRegex(@"^[^.~]{1,6}~[0-9]+(\.[^.]{0,3})?$")]
    private static partial Regex ShortName();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    internal static partial Regex IdRe();

    /// <summary>Counts the central directory's records straight from the file, before ZipArchive makes an object of each
    /// (security review S4: a small package with millions of records would otherwise fill the server's memory first).
    /// Reads the end-of-central-directory record (and its ZIP64 form), then walks the records with a 46-byte buffer.</summary>
    internal static void PrecheckDirectory(string zipPath, int maxEntries)
    {
        try
        {
            Precheck(zipPath, maxEntries);
        }
        catch (EndOfStreamException)
        {
            throw new ArchiveRefusedException("not a readable zip archive (cut short)");
        }
    }

    private static void Precheck(string zipPath, int maxEntries)
    {
        using var f = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096);
        var len = f.Length;
        if (len < 22) throw new ArchiveRefusedException("not a readable zip archive (too short)");
        var tailLen = (int)Math.Min(len, 22 + 65535);
        var tail = new byte[tailLen];
        f.Seek(len - tailLen, SeekOrigin.Begin);
        f.ReadExactly(tail);
        var eocd = -1;
        for (var i = tailLen - 22; i >= 0; i--)
            if (BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(i)) == 0x06054b50) { eocd = i; break; }
        if (eocd < 0) throw new ArchiveRefusedException("not a readable zip archive (no end of central directory)");
        long cdOffset = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(eocd + 16));
        long declared = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(eocd + 10));
        if ((cdOffset == 0xFFFFFFFF || declared == 0xFFFF) && eocd >= 20 && BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(eocd - 20)) == 0x07064b50)
        {
            var z64 = (long)BinaryPrimitives.ReadUInt64LittleEndian(tail.AsSpan(eocd - 20 + 8));
            if (z64 < 0 || z64 > len - 56) throw new ArchiveRefusedException("not a readable zip archive (bad ZIP64 record)");
            var rec = new byte[56];
            f.Seek(z64, SeekOrigin.Begin);
            f.ReadExactly(rec);
            if (BinaryPrimitives.ReadUInt32LittleEndian(rec) != 0x06064b50) throw new ArchiveRefusedException("not a readable zip archive (bad ZIP64 record)");
            declared = (long)Math.Min(BinaryPrimitives.ReadUInt64LittleEndian(rec.AsSpan(32)), long.MaxValue);
            cdOffset = (long)Math.Min(BinaryPrimitives.ReadUInt64LittleEndian(rec.AsSpan(48)), long.MaxValue);
        }
        if (declared > maxEntries) throw new ArchiveRefusedException($"more than {maxEntries} entries");
        if (cdOffset < 0 || cdOffset > len) throw new ArchiveRefusedException("not a readable zip archive (bad central directory offset)");
        f.Seek(cdOffset, SeekOrigin.Begin);
        var head = new byte[46];
        var count = 0;
        while (f.Position + 46 <= len)
        {
            f.ReadExactly(head);
            if (BinaryPrimitives.ReadUInt32LittleEndian(head) != 0x02014b50) break;
            if (++count > maxEntries) throw new ArchiveRefusedException($"more than {maxEntries} entries");
            var skip = (long)BinaryPrimitives.ReadUInt16LittleEndian(head.AsSpan(28)) + BinaryPrimitives.ReadUInt16LittleEndian(head.AsSpan(30)) +
                       BinaryPrimitives.ReadUInt16LittleEndian(head.AsSpan(32));
            if (f.Position + skip > len) break;
            f.Seek(skip, SeekOrigin.Current);
        }
    }

    /// <summary>Checks the archive's directory and reads its manifest. Throws <see cref="ArchiveRefusedException"/>.</summary>
    public static ArchiveInfo Inspect(string zipPath, ArchiveLimits limits)
    {
        try
        {
            PrecheckDirectory(zipPath, limits.MaxEntries);
            using var zip = ZipFile.OpenRead(zipPath);
            var entries = Checked(zip, limits);
            var prefix = Prefix(entries.Select(e => e.Path).ToList());
            var files = entries.Where(e => !e.IsDir).Select(e => (Path: e.Path[prefix.Length..], e.Entry)).ToList();
            if (files.Count == 0) throw new ArchiveRefusedException("the archive has no files");
            var kiln = files.FirstOrDefault(f => f.Path.Equals(KilnManifest, StringComparison.OrdinalIgnoreCase));
            var mod = files.FirstOrDefault(f => f.Path.Equals(ModManifest, StringComparison.OrdinalIgnoreCase));
            var list = files.Select(f => (f.Path, f.Entry.Length)).OrderBy(f => f.Path, StringComparer.Ordinal).ToList();
            if (kiln.Entry is not null)
            {
                var m = Manifest(kiln.Entry);
                var version = Text(m, "version", 64) ?? throw new ArchiveRefusedException($"{KilnManifest} has no version");
                return new ArchiveInfo("kiln", null, "Kiln", version, prefix, list);
            }
            if (mod.Entry is not null)
            {
                var m = Manifest(mod.Entry);
                var id = Text(m, "id", 64);
                if (id is null || !IdRe().IsMatch(id)) throw new ArchiveRefusedException($"{ModManifest}: id is missing or not a mod id");
                var version = Text(m, "version", 64) ?? throw new ArchiveRefusedException($"{ModManifest} has no version");
                return new ArchiveInfo("mod", id, Text(m, "name", 128) ?? id, version, prefix, list);
            }
            throw new ArchiveRefusedException($"no {ModManifest} or {KilnManifest} at the top of the archive (or in its one top folder)");
        }
        catch (InvalidDataException e)
        {
            throw new ArchiveRefusedException($"not a readable zip archive ({e.Message})");
        }
    }

    /// <summary>Unpacks the archive's files (after the prefix) into <paramref name="target"/>, which must not exist yet,
    /// and returns each file's path and SHA-256 as written (sorted by path). Throws <see cref="ArchiveRefusedException"/>;
    /// the caller removes the target on failure.</summary>
    public static IReadOnlyList<(string Path, string Sha256, long Size)> Extract(string zipPath, string target, ArchiveLimits limits)
    {
        var root = Path.GetFullPath(target);
        if (Directory.Exists(root) || File.Exists(root)) throw new ArchiveRefusedException("the target folder exists already");
        Directory.CreateDirectory(root);
        var result = new List<(string, string, long)>();
        long total = 0;
        try
        {
            PrecheckDirectory(zipPath, limits.MaxEntries);
            using var zip = ZipFile.OpenRead(zipPath);
            var entries = Checked(zip, limits);
            var prefix = Prefix(entries.Select(e => e.Path).ToList());
            var buf = new byte[81920];
            foreach (var e in entries)
            {
                if (e.IsDir) continue;
                var rel = e.Path[prefix.Length..];
                var dest = Path.GetFullPath(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)));
                if (!dest.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new ArchiveRefusedException($"{rel}: leaves the target folder");
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                long size = 0;
                using (var src = e.Entry.Open())
                using (var dst = new FileStream(dest, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    int n;
                    while ((n = src.Read(buf, 0, buf.Length)) > 0)
                    {
                        size += n;
                        total += n;
                        if (size > e.Entry.Length) throw new ArchiveRefusedException($"{rel}: more bytes than its header says");
                        if (size > limits.MaxEntryBytes) throw new ArchiveRefusedException($"{rel}: larger than {limits.MaxEntryBytes} bytes");
                        if (total > limits.MaxUnpackedBytes) throw new ArchiveRefusedException($"more than {limits.MaxUnpackedBytes} bytes unpacked");
                        hash.AppendData(buf, 0, n);
                        dst.Write(buf, 0, n);
                    }
                }
                if (size != e.Entry.Length) throw new ArchiveRefusedException($"{rel}: fewer bytes than its header says");
                result.Add((rel, Convert.ToHexStringLower(hash.GetHashAndReset()), size));
            }
        }
        catch (InvalidDataException e)
        {
            throw new ArchiveRefusedException($"not a readable zip archive ({e.Message})");
        }
        return [.. result.OrderBy(r => r.Item1, StringComparer.Ordinal)];
    }

    public static string Sha256File(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexStringLower(SHA256.HashData(fs));
    }

    private sealed record Item(string Path, bool IsDir, ZipArchiveEntry Entry);

    /// <summary>Every entry checked against the rules and the declared limits.</summary>
    private static List<Item> Checked(ZipArchive zip, ArchiveLimits limits)
    {
        if (zip.Entries.Count > limits.MaxEntries) throw new ArchiveRefusedException($"more than {limits.MaxEntries} entries");
        var items = new List<Item>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var e in zip.Entries)
        {
            var name = e.FullName;
            var isDir = name.EndsWith('/');
            var path = isDir ? name[..^1] : name;
            if (PathProblem(path) is { } why) throw new ArchiveRefusedException($"'{Shown(name)}': {why}");
            var unixType = (e.ExternalAttributes >> 16) & 0xF000;
            if (unixType is not (0 or 0x8000 or 0x4000)) throw new ArchiveRefusedException($"'{Shown(name)}': a link or special file");
            if ((e.ExternalAttributes & 0x400) != 0) throw new ArchiveRefusedException($"'{Shown(name)}': a Windows reparse point (link)");
            if (isDir || unixType == 0x4000)
            {
                if (e.Length != 0) throw new ArchiveRefusedException($"'{Shown(name)}': a folder with content");
                if (!seen.Add(path) && !dirs.Contains(path)) throw new ArchiveRefusedException($"'{Shown(name)}': twice (letter case ignored)");
                dirs.Add(path);
                items.Add(new Item(path, true, e));
                continue;
            }
            if (!seen.Add(path)) throw new ArchiveRefusedException($"'{Shown(name)}': twice (letter case ignored)");
            if (e.Length > limits.MaxEntryBytes) throw new ArchiveRefusedException($"'{Shown(name)}': larger than {limits.MaxEntryBytes} bytes");
            if (e.Length > 1 << 20 && e.Length / Math.Max(1, e.CompressedLength) > limits.MaxRatio)
                throw new ArchiveRefusedException($"'{Shown(name)}': compressed more than {limits.MaxRatio}:1");
            total += e.Length;
            if (total > limits.MaxUnpackedBytes) throw new ArchiveRefusedException($"more than {limits.MaxUnpackedBytes} bytes unpacked");
            items.Add(new Item(path, false, e));
        }
        // a file and a folder of one name ("a" and "a/b")
        foreach (var f in items.Where(i => !i.IsDir))
        {
            var parts = f.Path.Split('/');
            for (var k = 1; k < parts.Length; k++)
            {
                var parent = string.Join('/', parts[..k]);
                if (items.Any(i => !i.IsDir && i.Path.Equals(parent, StringComparison.OrdinalIgnoreCase)))
                    throw new ArchiveRefusedException($"'{Shown(parent)}': both a file and a folder");
            }
        }
        return items;
    }

    /// <summary>The one top folder every entry is in ("name/"), when no manifest sits at the top; else "".</summary>
    private static string Prefix(List<string> paths)
    {
        if (paths.Any(p => p.Equals(ModManifest, StringComparison.OrdinalIgnoreCase) || p.Equals(KilnManifest, StringComparison.OrdinalIgnoreCase))) return "";
        var tops = paths.Select(p => p.Split('/')[0]).Distinct(StringComparer.Ordinal).ToList();
        if (tops.Count != 1) return "";
        var top = tops[0] + "/";
        return paths.All(p => p.StartsWith(top, StringComparison.Ordinal) || p == tops[0]) && paths.Any(p => p.StartsWith(top, StringComparison.Ordinal)) ? top : "";
    }

    private static JsonObject Manifest(ZipArchiveEntry e)
    {
        if (e.Length > MaxManifestBytes) throw new ArchiveRefusedException($"{e.Name}: larger than {MaxManifestBytes} bytes");
        using var s = e.Open();
        var buf = new byte[MaxManifestBytes + 1];
        var len = 0;
        int n;
        while (len < buf.Length && (n = s.Read(buf, len, buf.Length - len)) > 0) len += n;
        if (len > MaxManifestBytes) throw new ArchiveRefusedException($"{e.Name}: larger than {MaxManifestBytes} bytes");
        try
        {
            return JsonNode.Parse(buf.AsSpan(0, len), documentOptions: new JsonDocumentOptions { MaxDepth = 32, CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })
                as JsonObject ?? throw new ArchiveRefusedException($"{e.Name}: not a JSON object");
        }
        catch (JsonException x)
        {
            throw new ArchiveRefusedException($"{e.Name}: not valid JSON ({x.Message})");
        }
    }

    private static string? Text(JsonObject o, string name, int max) =>
        o[name] is JsonValue v && v.TryGetValue<string>(out var s) && s.Length is > 0 && s.Length <= max && !s.Any(char.IsControl) ? s : null;

    /// <summary>An entry name as it may be shown (control characters escaped, at most 120 characters).</summary>
    private static string Shown(string name)
    {
        var sb = new StringBuilder();
        foreach (var ch in name.Length > 120 ? name[..120] + "..." : name) sb.Append(char.IsControl(ch) ? $"\\u{(int)ch:x4}" : ch);
        return sb.ToString();
    }
}
