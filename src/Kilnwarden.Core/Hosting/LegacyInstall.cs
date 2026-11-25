// SPDX-License-Identifier: MIT
namespace Kilnwarden.Hosting;

/// <summary>What the rename from Kiln Web Admin (mod id kiln-webadmin) leaves on a server that ran a 0.1.0 test build:
/// its mod folder and its data folder. Kiln names a mod's data folder after its id (kiln/data/&lt;id&gt;), so the
/// accounts, keys and settings stay behind under the old name unless they are moved.</summary>
public static class LegacyInstall
{
    /// <summary>The mod id before the rename: still the consumer name released Kiln launchers mint logins for, and the
    /// name of the old mod and data folders.</summary>
    public const string OldModId = "kiln-webadmin";

    public enum Migration { Nothing, Moved, BothKept, Failed }

    /// <summary>A copy of the old mod that runs, or will run, next to this one: its folder, or null when there is none.
    /// Kiln's own word first (<paramref name="kilnHasIt"/>: the folder of a mod with the old id that Kiln has loaded or
    /// is about to load, which may lie outside kiln/mods when a profile names it), then the folders next to this one:
    /// kiln/mods/kiln-webadmin, or any folder whose kiln.mod.json has the old id (Kiln loads a folder by the id in its
    /// manifest, whatever the folder is called), unless a .disabled file switches it off. Two copies would fight for the
    /// port and the data folder, and moving the data folder from under a running copy would lose what it writes.</summary>
    public static string? ActiveOldModFolder(string modDirectory, string? kilnHasIt = null)
    {
        if (kilnHasIt is not null) return kilnHasIt;
        var self = Path.TrimEndingDirectorySeparator(Path.GetFullPath(modDirectory));
        if (Path.GetDirectoryName(self) is not { } mods) return null;
        List<string> dirs;
        try
        {
            dirs = Directory.EnumerateDirectories(mods).Take(MaxFolders).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            dirs = [];
        }
        foreach (var dir in dirs.Prepend(Path.Combine(mods, OldModId)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(Path.TrimEndingDirectorySeparator(dir), self, StringComparison.OrdinalIgnoreCase)) continue;
            var manifest = Path.Combine(dir, "kiln.mod.json");
            if (!File.Exists(manifest) || File.Exists(Path.Combine(dir, ".disabled"))) continue;
            if (string.Equals(Path.GetFileName(dir), OldModId, StringComparison.OrdinalIgnoreCase) || ManifestId(manifest) == OldModId) return dir;
        }
        return null;
    }

    /// <summary>The folders of kiln/mods looked at for an old copy (as many as the panel's mod list shows).</summary>
    public const int MaxFolders = 500;

    private static string? ManifestId(string file)
    {
        try
        {
            var f = new FileInfo(file);
            if (f.Length > 64 << 10) return null;
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(file),
                new System.Text.Json.JsonDocumentOptions { MaxDepth = 32, CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true });
            return doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object && doc.RootElement.TryGetProperty("id", out var id) &&
                   id.ValueKind == System.Text.Json.JsonValueKind.String ? id.GetString() : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>Moves kiln/data/kiln-webadmin to this mod's data folder when the old folder exists and the new one is
    /// missing or empty: one rename on the same file system (atomic: a crash leaves one folder or the other, never half of
    /// each), so file modes and ACLs (the owner-only secrets) stay as they were. Nothing is copied: a move that the file
    /// system refuses (another volume, a file still open) fails, and the panel stays off. Runs before anything writes to
    /// the data folder.</summary>
    public static (Migration Result, string Message) MigrateDataFolder(string dataDirectory)
    {
        var target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataDirectory));
        if (Path.GetDirectoryName(target) is not { } parent) return (Migration.Nothing, "");
        var old = Path.Combine(parent, OldModId);
        if (string.Equals(old, target, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(old)) return (Migration.Nothing, "");
        var oldName = $"kiln/data/{OldModId}";
        var newName = $"kiln/data/{Path.GetFileName(target)}";
        try
        {
            if (Directory.Exists(target))
            {
                if (Directory.EnumerateFileSystemEntries(target).Any())
                    return (Migration.BothKept, $"{oldName} and {newName} both hold files: this panel uses {newName} and leaves " +
                                                $"{oldName} as it is (delete it once you no longer need it)");
                // an empty link or mount point (another disk, say): the move would put the old folder in its place, so the
                // owner moves the files into it
                if (new DirectoryInfo(target).Attributes.HasFlag(FileAttributes.ReparsePoint))
                    return (Migration.Failed, $"{newName} is a link or mount point, so {oldName} was not moved into its place. Stop the server, " +
                                              $"move the files of {oldName} into {newName} by hand and start it again; the panel stays off " +
                                              "until then (a fresh start would make a new relay key, which a claimed name would no longer match)");
                Directory.Delete(target);
            }
            Directory.Move(old, target);
            return (Migration.Moved, $"moved {oldName} to {newName} (accounts, settings, keys and history from Kiln Web Admin)");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return (Migration.Failed, $"could not move {oldName} to {newName} ({e.GetType().Name}: {e.Message}). Stop the server, " +
                                      $"rename the folder by hand and start it again; the panel stays off until then (a fresh start would " +
                                      "make a new relay key, which a claimed name would no longer match)");
        }
    }
}
