// SPDX-License-Identifier: MIT
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kilnwarden.Storage;

/// <summary>The mod's files under kiln/data/kilnwarden (DESIGN §1: JSON and JSONL, no database). Paths are relative
/// to the root and may not leave it. Writes are atomic (a temp file in the same folder, flushed, then moved over the
/// old one), so a crash leaves either the old or the new file, never half of one.</summary>
public sealed class DataStore
{
    /// <summary>How the mod's own JSON files are written and read: camelCase, indented (people read and edit them),
    /// comments and trailing commas allowed on read (settings.json is hand-edited), enums as strings.</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    public DataStore(string root)
    {
        Root = Path.GetFullPath(root);
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    /// <summary>The full path of <paramref name="relative"/> ("settings.json", "tls/self-signed.key.pem"). Throws
    /// ArgumentException for a rooted path or one that leaves the root.</summary>
    public string PathOf(string relative)
    {
        if (string.IsNullOrEmpty(relative) || Path.IsPathRooted(relative)) throw new ArgumentException($"'{relative}' is not a relative path");
        var full = Path.GetFullPath(Path.Combine(Root, relative));
        if (!full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException($"'{relative}' leaves the data folder");
        return full;
    }

    public bool Exists(string relative) => File.Exists(PathOf(relative));

    /// <summary>Reads a JSON file; null when it does not exist. Throws JsonException when it is unreadable (the caller
    /// decides: settings keep the last good value, accounts refuse to start).</summary>
    public T? ReadJson<T>(string relative) where T : class
    {
        var path = PathOf(relative);
        if (!File.Exists(path)) return null;
        var bytes = File.ReadAllBytes(path);
        return JsonSerializer.Deserialize<T>(bytes, Json);
    }

    public void WriteJson<T>(string relative, T value, bool secret = false) =>
        AtomicFile.Write(PathOf(relative), JsonSerializer.SerializeToUtf8Bytes(value, Json), secret);

    public byte[]? ReadBytes(string relative)
    {
        var path = PathOf(relative);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public void WriteBytes(string relative, ReadOnlySpan<byte> data, bool secret = false) => AtomicFile.Write(PathOf(relative), data, secret);

    public void Delete(string relative) => File.Delete(PathOf(relative));

    /// <summary>The panel's secret files (keys, accounts, sessions, webhook URLs, extension settings), relative paths.</summary>
    public static readonly IReadOnlyList<string> SecretFiles =
    [
        "accounts.json", "sessions.json", "webhooks.json", "relay-key.pem", "tls/self-signed-ecdsa.key.pem", "tls/self-signed-rsa.key.pem",
        "tls/acme/account.json", "tls/acme/cert.json",
    ];

    /// <summary>Writes the secret files again as owner-only (a folder made by an older version, or by hand, may have them
    /// readable by everyone; under Wine the mode only follows the DACL a file is created with). Called at start.</summary>
    public int Resecure()
    {
        var n = 0;
        var files = SecretFiles.Select(PathOf).Concat(Directory.Exists(PathOf("ext")) ? Directory.EnumerateFiles(PathOf("ext"), "*.json") : []);
        foreach (var f in files)
        {
            try
            {
                if (!File.Exists(f) || new FileInfo(f).Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                AtomicFile.Write(f, File.ReadAllBytes(f), secret: true);
                n++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // the next write of that file secures it
            }
        }
        return n;
    }

    /// <summary>An append-only JSONL file with size-based rotation (audit, events).</summary>
    public JsonlWriter OpenJsonl(string relative, long rotateBytes = 8 << 20, int keep = 5) => new(PathOf(relative), rotateBytes, keep);
}
