// SPDX-License-Identifier: MIT
namespace Kilnwarden.Static;

/// <summary>Where the Kiln Launcher keeps the map art it makes from the player's own game (its data folder's
/// admin-assets: map/..., data/...), when the launcher runs on the server's own computer (0.1.3). Only the launcher's
/// fixed, documented places are looked at, never a path anyone types: <c>$KILN_DATA_DIR</c> (the launcher's own
/// override, when the server was started with it), Windows' <c>%LOCALAPPDATA%\Kiln</c>, and on Linux (under Wine, the
/// same folders through the Z: drive) <c>$XDG_DATA_HOME/kiln</c> or <c>~/.local/share/kiln</c>.</summary>
public static class LauncherArt
{
    public const string FolderName = "admin-assets";

    /// <summary>The places to look, in order (they may not exist).</summary>
    public static IReadOnlyList<string> Candidates(Func<string, string?>? env = null, bool? windows = null, string? localAppData = null)
    {
        env ??= Environment.GetEnvironmentVariable;
        var onWindows = windows ?? OperatingSystem.IsWindows();
        var list = new List<string>();
        // Windows paths are joined with '\' (also when this runs elsewhere, as in the tests)
        string Join(string a, params string[] parts) => onWindows ? string.Join('\\', [a.TrimEnd('\\', '/'), .. parts]) : Path.Combine([a, .. parts]);
        void Add(string? dataDir)
        {
            if (string.IsNullOrWhiteSpace(dataDir)) return;
            var p = Join(dataDir, FolderName);
            if (!list.Contains(p, StringComparer.Ordinal)) list.Add(p);
        }
        Add(Native(env("KILN_DATA_DIR"), onWindows));
        if (onWindows)
        {
            var local = localAppData ?? SafeLocalAppData();
            if (local.Length > 0) Add(Join(local, "Kiln"));
            // under Wine: the Linux side's folders. Wine names the Unix home in WINEHOMEDIR (\??\Z:\home\me); HOME and
            // XDG_DATA_HOME, when Wine passes them on, are Unix paths ("/home/me")
            if (env("XDG_DATA_HOME") is { Length: > 1 } xdg && xdg[0] == '/') Add(Join(OnZ(xdg), "kiln"));
            foreach (var home in new[] { WineHome(env("WINEHOMEDIR")), env("HOME") is { Length: > 1 } h && h[0] == '/' ? OnZ(h) : null })
                if (home is not null) Add(Join(home, ".local", "share", "kiln"));
        }
        else
        {
            if (env("XDG_DATA_HOME") is { Length: > 0 } xdg) Add(Join(xdg, "kiln"));
            if (env("HOME") is { Length: > 0 } home) Add(Join(home, ".local", "share", "kiln"));
        }
        return list;
    }

    /// <summary>The first candidate that holds the launcher's map art (a map/ or data/ folder, neither a link).</summary>
    public static string? Find(IEnumerable<string> candidates) => candidates.FirstOrDefault(Holds);

    public static bool Holds(string folder)
    {
        try
        {
            var root = new DirectoryInfo(folder);
            if (!root.Exists) return false;
            return new[] { "map", "data" }.Any(n => new DirectoryInfo(Path.Combine(folder, n)) is { Exists: true } d && !d.Attributes.HasFlag(FileAttributes.ReparsePoint));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            return false;
        }
    }

    private static string? Native(string? path, bool windows) =>
        path is not { Length: > 0 } ? null : windows && path[0] == '/' ? OnZ(path) : path;

    private static string OnZ(string unixPath) => "Z:" + unixPath.Replace('/', '\\');

    private static string? WineHome(string? v)
    {
        if (v is not { Length: > 3 }) return null;
        var s = v.StartsWith(@"\??\", StringComparison.Ordinal) ? v[4..] : v;
        return s.Length >= 3 && char.IsAsciiLetter(s[0]) && s[1] == ':' ? s : null;
    }

    private static string SafeLocalAppData()
    {
        try { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); }
        catch (PlatformNotSupportedException) { return ""; }
    }
}
