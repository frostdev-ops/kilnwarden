// SPDX-License-Identifier: MIT
using System.Text.Json;
using System.Text.Json.Nodes;
using Kilnwarden.Hosting;

namespace Kilnwarden.Ops;

/// <summary>A switch the panel refuses: <see cref="Status"/> and <see cref="Code"/> go to the answer.</summary>
public sealed class ModSwitchException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

/// <summary>Persistent mod switches (DESIGN §5 Mods: enable/disable). Kiln's loader skips a <c>kiln/mods/&lt;folder&gt;</c>
/// that holds a <c>.disabled</c> file ("disabled by a .disabled file"), and staged updates keep it, so switching a mod
/// off for the next start is writing that file and switching it on is removing it. Kiln.Admin has no op for this; the
/// panel runs in the server process and writes it itself. Only direct, real folders of kiln/mods (no links); never the
/// panel's own folder or Kiln.Admin's, which the panel needs to work at all.</summary>
public static class ModSwitch
{
    public const string File = ".disabled";
    public const int MaxFolders = 500;

    public sealed record Result(string? Id, bool Changed);

    public static string ModsDirectory(WebAdminEnvironment env) => Path.Combine(Path.GetFullPath(env.ServerDirectory), "kiln", "mods");

    /// <summary>Every folder of kiln/mods with its manifest's id, name and version and whether it is switched off.</summary>
    public static JsonArray Folders(string modsDir, WebAdminEnvironment env)
    {
        var arr = new JsonArray();
        if (!Directory.Exists(modsDir)) return arr;
        IEnumerable<string> dirs;
        try
        {
            dirs = Directory.EnumerateDirectories(modsDir).Take(MaxFolders).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return arr;
        }
        foreach (var dir in dirs.Order(StringComparer.OrdinalIgnoreCase))
        {
            var folder = Path.GetFileName(dir);
            if (folder.StartsWith('.')) continue;
            var (id, name, version) = Manifest(dir);
            var flag = Path.Combine(dir, File);
            arr.Add(new JsonObject
            {
                ["folder"] = folder, ["id"] = id, ["name"] = name, ["version"] = version,
                ["disabled"] = System.IO.File.Exists(flag) || Directory.Exists(flag),
                ["locked"] = Needed(dir, id, env) ? "needed by the panel" : null,
            });
        }
        return arr;
    }

    private static (string? Id, string? Name, string? Version) Manifest(string dir)
    {
        try
        {
            var f = new FileInfo(Path.Combine(dir, "kiln.mod.json"));
            if (!f.Exists || f.Length > 64 << 10) return (null, null, null);
            using var doc = JsonDocument.Parse(System.IO.File.ReadAllBytes(f.FullName), new JsonDocumentOptions { MaxDepth = 32, CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            string? S(string k) => doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() is { Length: <= 120 } x ? x : null : null;
            return (S("id"), S("name"), S("version"));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return (null, null, null);
        }
    }

    // The panel's own folder (by path or id) and Kiln.Admin's (by id or folder name).
    private static bool Needed(string dir, string? id, WebAdminEnvironment env) =>
        string.Equals(Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(env.ModDirectory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) ||
        string.Equals(id, Product.ModId, StringComparison.OrdinalIgnoreCase) || string.Equals(id, "kiln-admin", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Path.GetFileName(dir), "kiln-admin", StringComparison.OrdinalIgnoreCase);

    /// <summary>Switches <paramref name="folder"/> off (<paramref name="disable"/>) or on for the next start.</summary>
    public static Result Set(string modsDir, WebAdminEnvironment env, string folder, bool disable, string by, DateTimeOffset at)
    {
        if (folder.Length == 0 || folder.StartsWith('.') || folder.Contains('/') || folder.Contains('\\') || folder.Contains(':'))
            throw new ModSwitchException(422, "invalid", "folder: a folder name under kiln/mods");
        var root = Path.GetFullPath(modsDir);
        var dir = Path.GetFullPath(Path.Combine(root, folder));
        if (!string.Equals(Path.GetDirectoryName(dir), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal))
            throw new ModSwitchException(422, "invalid", "folder: a folder name under kiln/mods");
        var info = new DirectoryInfo(dir);
        if (!info.Exists) throw new ModSwitchException(404, "not_found", $"there is no folder kiln/mods/{folder}");
        if (info.Attributes.HasFlag(FileAttributes.ReparsePoint) || new DirectoryInfo(root).Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new ModSwitchException(409, "link", "that folder is a link: switch it by hand");
        var (id, _, _) = Manifest(dir);
        if (Needed(dir, id, env)) throw new ModSwitchException(409, "needed_by_the_panel", "the panel needs this mod to work: it cannot switch it off");
        // Kiln Web Admin, this panel's old name: two copies would fight for the port and the data, and the panel stays off
        // while the old one is switched on (LegacyInstall). Switching it off stays possible.
        if (!disable && (string.Equals(id, LegacyInstall.OldModId, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(folder, LegacyInstall.OldModId, StringComparison.OrdinalIgnoreCase)))
            throw new ModSwitchException(409, "old_name", $"this is Kiln Web Admin, {Product.Name}'s old name: it cannot run next to {Product.Name}. Delete the folder instead");
        var flag = Path.Combine(dir, File);
        if (Directory.Exists(flag) || (System.IO.File.Exists(flag) && System.IO.File.GetAttributes(flag).HasFlag(FileAttributes.ReparsePoint)))
            throw new ModSwitchException(409, "not_a_file", $"kiln/mods/{folder}/{File} is not a plain file: fix it by hand");
        try
        {
            if (disable)
            {
                if (System.IO.File.Exists(flag)) return new Result(id, false);
                using var f = new FileStream(flag, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                f.Write(System.Text.Encoding.UTF8.GetBytes($"Switched off by {by} at {at.UtcDateTime:yyyy-MM-dd HH:mm} UTC. Kiln skips this folder while this file is here.\n"));
                return new Result(id, true);
            }
            if (!System.IO.File.Exists(flag)) return new Result(id, false);
            System.IO.File.Delete(flag);
            return new Result(id, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new ModSwitchException(500, "io", $"kiln/mods/{folder}/{File}: {e.Message}");
        }
    }
}
