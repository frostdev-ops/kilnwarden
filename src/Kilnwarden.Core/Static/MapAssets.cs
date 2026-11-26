// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Modules;
using Kilnwarden.Routing;

namespace Kilnwarden.Static;

/// <summary>The admin pages' map art and catalogs (DESIGN §4: map tiles local or served by the server). They are the
/// game's own art, which the launcher makes from the player's installed game and which nobody may ship, so the panel
/// serves only what the owner copied into the data folder's map-assets/ (the launcher's admin-assets folder: map/...,
/// data/...; the folder may also be a link to it). Signed-in users only; plain files under that folder only (no links
/// below it, no dot-names, no ".."). Without the
/// folder the map draws its live overlays on a plain world plane, as in the launcher.
/// <list type="bullet">
/// <item>GET /api/core/map-assets/{path}: a file (webp, png, json, bin, fonts), streamed, cached for a day.</item>
/// <item>GET /api/core/map-assets-status: {present, folder, launcher, copy} (0.1.3: <c>launcher</c> says whether the Kiln
/// Launcher's own copy is on this computer, <see cref="LauncherArt"/>; <c>copy</c> is the copy's progress).</item>
/// <item>POST /api/core/map-assets/copy (owner, recent proof; 0.1.3): copies that folder's map/ and data/ here, in the
/// background. Only the launcher's fixed places are read, and only the file types this route serves.</item>
/// </list></summary>
public sealed class MapAssetsComponent : IWebAdminComponent
{
    public const string Folder = "map-assets";
    public const long MaxFileBytes = 256L << 20;
    public const long MaxCopyBytes = 2L << 30;
    public const int MaxCopyFiles = 200_000;

    private readonly Func<IReadOnlyList<string>> _launcherFolders;
    private CancellationTokenSource? _life;
    private string? _root;
    private IWebAdminLog? _log;
    private volatile CopyState _copy = new("idle", 0, 0, null);
    private int _copying;

    public MapAssetsComponent() : this(null)
    {
    }

    /// <summary>Test and dev host hook: the places to look for the launcher's folder instead of the fixed ones.</summary>
    internal MapAssetsComponent(Func<IReadOnlyList<string>>? launcherFolders)
    {
        _launcherFolders = launcherFolders ?? (() => LauncherArt.Candidates());
    }

    public string Name => "map-assets";

    /// <summary>The copy's progress: idle, copying, done or failed (<see cref="Error"/> says why, in plain words).</summary>
    public sealed record CopyState(string State, int Files, long Bytes, string? Error);

    public void Configure(WebAdminBuilder b)
    {
        var root = _root = b.Data.PathOf(Folder);
        _log = b.Log;
        var core = new ModuleRegistration(b.Modules, CoreComponent.ModuleId);
        core.MapGet("/map-assets-status", RouteAccess.SignedIn, _ =>
        {
            var c = _copy;
            return ValueTask.FromResult(HttpResponse.Json(new JsonObject
            {
                ["present"] = Present(root),
                ["folder"] = Path.Combine("kiln", "data", Product.ModId, Folder),
                ["launcher"] = LauncherArt.Find(_launcherFolders()) is not null,
                ["copy"] = new JsonObject { ["state"] = c.State, ["files"] = c.Files, ["bytes"] = c.Bytes, ["error"] = c.Error },
            }));
        });
        core.MapGet("/map-assets/{*path}", RouteAccess.SignedIn, ctx => ValueTask.FromResult(Serve(root, ctx.RouteValue("path"))));
        core.MapPost("/map-assets/copy", RouteAccess.Requires(CoreComponent.SettingsEdit), ctx =>
        {
            if (ctx.User is not Auth.WebPrincipal { IsOwner: true } p) throw new HttpError(403, "only the owner copies the map art");
            if (Present(root)) throw new HttpError(409, "The map art is here already.");
            if (LauncherArt.Find(_launcherFolders()) is not { } source)
                throw new HttpError(404, "The Kiln Launcher's map art isn't on this computer. Make it in the launcher (Settings, Game art), or copy it by hand.");
            if (Interlocked.CompareExchange(ref _copying, 1, 0) != 0) throw new HttpError(409, "The map art is being copied already.");
            _copy = new CopyState("copying", 0, 0, null);
            var log = b.Log;
            var token = _life?.Token ?? CancellationToken.None;
            _ = Task.Run(() =>
            {
                try
                {
                    var (files, bytes) = CopyFrom(source, root, token, (f, n) => _copy = new CopyState("copying", f, n, null));
                    _copy = new CopyState("done", files, bytes, null);
                    log.Info($"{Product.Name}: copied the Kiln Launcher's map art ({files} files) into {Path.Combine("kiln", "data", Product.ModId, Folder)}");
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    // everyone signed in reads this (the Map page's notice): plain words, never the exception's text, which
                    // names folders of this computer (the user's home, the server's folder); the log has it
                    _copy = new CopyState("failed", 0, 0, e is CopyRefused r ? r.Message
                        : e is OperationCanceledException ? "The copy stopped when the panel stopped. Nothing was changed."
                        : "The copy stopped, and nothing was changed. The server's log says why.");
                    log.Warn($"{Product.Name}: copying the launcher's map art failed: {e.Message}");
                }
                finally
                {
                    Volatile.Write(ref _copying, 0);
                }
            }, CancellationToken.None);
            ctx.Services.TryGet<Security.IAuditSink>()?.Record(Auth.AuditLog.Entry("core.map_assets.copy", p, ctx.ClientAddress, true));
            return ValueTask.FromResult(HttpResponse.Json(new JsonObject { ["started"] = true }, 202));
        });
    }

    public Task StartAsync(WebAdminServices services, CancellationToken ct)
    {
        _life = new CancellationTokenSource();
        // a copy cut short (a crash, a kill) left its working folder beside map-assets: the ones there now (before any
        // copy of this run can start) are deleted in the background
        if (_root is { } root && StaleWork(root, _log) is { Count: > 0 } stale)
        {
            var log = _log;
            _ = Task.Run(() => DeleteStaleWork(stale, log), CancellationToken.None);
        }
        return Task.CompletedTask;
    }

    /// <summary>The working folders (<c>map-assets.copying-*</c>) of copies cut short, in the data folder: only real folders
    /// of that name (a link of that name is left alone).</summary>
    internal static List<DirectoryInfo> StaleWork(string root, IWebAdminLog? log)
    {
        try
        {
            var parent = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar))!);
            return parent.Exists ? [.. parent.EnumerateDirectories(Folder + ".copying-*").Where(d => !d.Attributes.HasFlag(FileAttributes.ReparsePoint))] : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            log?.Warn($"{Product.Name}: could not look for map art copies cut short: {e.Message}");
            return [];
        }
    }

    internal static void DeleteStaleWork(IEnumerable<DirectoryInfo> stale, IWebAdminLog? log)
    {
        foreach (var d in stale)
        {
            try
            {
                d.Delete(true);
                log?.Info($"{Product.Name}: deleted {d.Name}, left by a copy of the launcher's map art that was cut short");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                log?.Warn($"{Product.Name}: could not delete {d.Name} (a copy of the map art cut short): {e.Message}");
            }
        }
    }

    public Task StopAsync(CancellationToken ct)
    {
        _life?.Cancel();
        _life?.Dispose();
        _life = null;
        return Task.CompletedTask;
    }

    internal static bool Present(string root) => Directory.Exists(Path.Combine(root, "map")) || Directory.Exists(Path.Combine(root, "data"));

    /// <summary>A refusal of the copy, said plainly (the source holds something it must not).</summary>
    internal sealed class CopyRefused(string message) : Exception(message);

    /// <summary>Copies <paramref name="source"/>'s map/ and data/ into <paramref name="root"/>: only plain files of the
    /// served types (no links, no dot-names), at most <see cref="MaxCopyFiles"/> files and <see cref="MaxCopyBytes"/>
    /// bytes. Written to a folder beside the data folder's map-assets first, then moved in (one rename when there is no
    /// map-assets yet), so a stopped copy leaves no half map behind.</summary>
    internal static (int Files, long Bytes) CopyFrom(string source, string root, CancellationToken ct, Action<int, long>? progress = null)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar))!;
        var work = Path.Combine(parent, $"{Folder}.copying-{Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4))}");
        var files = 0;
        long bytes = 0;
        try
        {
            foreach (var top in new[] { "map", "data" })
            {
                var from = new DirectoryInfo(Path.Combine(source, top));
                if (!from.Exists || from.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                var stack = new Stack<(DirectoryInfo Dir, string Rel)>();
                stack.Push((from, top));
                while (stack.Count > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    var (dir, rel) = stack.Pop();
                    Directory.CreateDirectory(Path.Combine(work, rel));
                    foreach (var e in dir.EnumerateFileSystemInfos())
                    {
                        if (e.Name.StartsWith('.') || e.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                        if (e is DirectoryInfo d)
                        {
                            stack.Push((d, Path.Combine(rel, d.Name)));
                            continue;
                        }
                        if (e is not FileInfo f || TypeOf(f.Name) is null) continue;
                        if (f.Length > MaxFileBytes) continue;
                        if (++files > MaxCopyFiles) throw new CopyRefused($"The launcher's folder holds more than {MaxCopyFiles} files: copy it by hand.");
                        bytes += f.Length;
                        if (bytes > MaxCopyBytes) throw new CopyRefused($"The launcher's folder is larger than {MaxCopyBytes >> 30} GB: copy it by hand.");
                        f.CopyTo(Path.Combine(work, rel, f.Name));
                        if (files % 50 == 0) progress?.Invoke(files, bytes);
                    }
                }
            }
            if (files == 0) throw new CopyRefused("The launcher's folder has no map art in it. Make it again in the launcher (Settings, Game art).");
            if (!Directory.Exists(root) && !File.Exists(root))
                Directory.Move(work, root); // no map-assets yet (the usual case): one rename, all of it or nothing
            else
            {
                // map-assets is there (empty of art, or a link to elsewhere): map/ and data/ go in one at a time
                if (Present(root)) throw new CopyRefused("The map art arrived while copying. Nothing was changed.");
                foreach (var top in new[] { "map", "data" })
                    if (Directory.Exists(Path.Combine(work, top))) Directory.Move(Path.Combine(work, top), Path.Combine(root, top));
            }
            progress?.Invoke(files, bytes);
            return (files, bytes);
        }
        finally
        {
            try { if (Directory.Exists(work)) Directory.Delete(work, true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* the next start deletes it (StaleWork) */ }
        }
    }

    private static string? TypeOf(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".webp" => "image/webp",
        ".png" => "image/png",
        ".json" => "application/json",
        ".bin" => "application/octet-stream",
        ".woff2" => "font/woff2",
        ".otf" => "font/otf",
        ".ttf" => "font/ttf",
        _ => null,
    };

    internal static HttpResponse Serve(string root, string? rel)
    {
        if (string.IsNullOrEmpty(rel) || rel.Length > 512) return HttpResponse.NotFound();
        var segs = rel.Split('/');
        if (segs.Any(s => s.Length == 0 || s.StartsWith('.') || s.Contains('\\') || s.Contains(':') || s.Any(char.IsControl))) return HttpResponse.NotFound();
        var type = TypeOf(rel);
        if (type is null) return HttpResponse.NotFound();
        var full = Path.GetFullPath(Path.Combine(root, Path.Combine(segs)));
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootFull, StringComparison.Ordinal)) return HttpResponse.NotFound();
        var info = new FileInfo(full);
        if (!info.Exists || info.Length > MaxFileBytes) return HttpResponse.NotFound();
        // no link anywhere below the folder (map-assets itself may be a link the owner made to the launcher's folder)
        for (FileSystemInfo? f = info; f is not null && f.FullName.Length > rootFull.Length - 1; f = f is FileInfo fi ? fi.Directory : ((DirectoryInfo)f).Parent)
            if (f.Attributes.HasFlag(FileAttributes.ReparsePoint)) return HttpResponse.NotFound();
        var r = new HttpResponse { Status = 200, BodyLength = info.Length };
        r.Headers.Set("Content-Type", type);
        r.Headers.Set("Cache-Control", "private, max-age=86400");
        r.Headers.Set("X-Content-Type-Options", "nosniff");
        r.BodyWriter = async (stream, ct) =>
        {
            await using var f = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, useAsync: true);
            await f.CopyToAsync(stream, ct).ConfigureAwait(false);
        };
        return r;
    }
}
