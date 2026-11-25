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
/// <item>GET /api/core/map-assets-status: {present, folder}.</item>
/// </list></summary>
public sealed class MapAssetsComponent : IWebAdminComponent
{
    public const string Folder = "map-assets";
    public const long MaxFileBytes = 256L << 20;

    public string Name => "map-assets";

    public void Configure(WebAdminBuilder b)
    {
        var root = b.Data.PathOf(Folder);
        var core = new ModuleRegistration(b.Modules, CoreComponent.ModuleId);
        core.MapGet("/map-assets-status", RouteAccess.SignedIn, _ => ValueTask.FromResult(HttpResponse.Json(new JsonObject
        {
            ["present"] = Directory.Exists(Path.Combine(root, "map")) || Directory.Exists(Path.Combine(root, "data")),
            ["folder"] = Path.Combine("kiln", "data", Product.ModId, Folder),
        })));
        core.MapGet("/map-assets/{*path}", RouteAccess.SignedIn, ctx => ValueTask.FromResult(Serve(root, ctx.RouteValue("path"))));
    }

    internal static HttpResponse Serve(string root, string? rel)
    {
        if (string.IsNullOrEmpty(rel) || rel.Length > 512) return HttpResponse.NotFound();
        var segs = rel.Split('/');
        if (segs.Any(s => s.Length == 0 || s.StartsWith('.') || s.Contains('\\') || s.Contains(':') || s.Any(char.IsControl))) return HttpResponse.NotFound();
        var type = Path.GetExtension(rel).ToLowerInvariant() switch
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
