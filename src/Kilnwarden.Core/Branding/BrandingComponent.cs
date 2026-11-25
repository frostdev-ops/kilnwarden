// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;
using Kilnwarden.Config;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Modules;
using Kilnwarden.Routing;

namespace Kilnwarden.Branding;

/// <summary>The panel's branding (DESIGN §4): public and read-only, because the sign-in page shows it.
/// <list type="bullet">
/// <item>GET /api/core/branding: {name, accent, logo, favicon}. name: branding.json's, else enshrouded_server.json's
/// "name", else the product name; accent "#rrggbb" or null; logo and favicon: their URLs, or null.</item>
/// <item>GET /api/core/branding/logo and /api/core/branding/favicon: the image (<see cref="BrandingFiles"/>), with its
/// exact raster content type, nosniff, an ETag and a sandbox policy. SVG is not served.</item>
/// </list>
/// The routes are the core module's (always on); branding.json is a hot-reloaded settings file nothing writes yet.</summary>
public sealed class BrandingComponent : IWebAdminComponent
{
    public const string SettingsFile = "branding.json", Folder = "branding";
    public const string LogoPath = "/api/core/branding/logo", FaviconPath = "/api/core/branding/favicon";

    public string Name => "branding";

    public void Configure(WebAdminBuilder b)
    {
        var settings = b.SettingsFile<BrandingSettings>(SettingsFile, BrandingRules.Validate);
        EditableSettings.Of(b.Services).Add(EditableFile.For("branding", "Name and colour", settings));
        b.Services.Add(settings);
        var files = new BrandingFiles(b.Data.PathOf(Folder), b.Log);
        var gameName = new GameServerName(b.Environment.ServerDirectory, b.Environment.Time);
        b.Services.Add(new ServerDisplayName(() => BrandingRules.CleanName(settings.Current.Name) ?? gameName.Current ?? Product.Name));
        // the core component registered the core module first (the host's order); branding adds its routes to it
        var core = new ModuleRegistration(b.Modules, CoreComponent.ModuleId);

        core.MapGet("/branding", RouteAccess.Public, _ =>
        {
            var s = settings.Current;
            var body = new JsonObject
            {
                ["name"] = BrandingRules.CleanName(s.Name) ?? gameName.Current ?? Product.Name,
                ["accent"] = BrandingRules.IsAccent(s.Accent) ? s.Accent!.ToLowerInvariant() : null,
                ["logo"] = files.Find(s.Logo, BrandingFiles.LogoNames) is null ? null : LogoPath,
                ["favicon"] = files.Find(s.Favicon, BrandingFiles.FaviconNames) is null ? null : FaviconPath,
            };
            var r = HttpResponse.Json(body);
            r.Headers.Set("Cache-Control", "no-cache");
            return ValueTask.FromResult(r);
        });
        // uploads from Panel settings: a PNG, WebP or ICO (sniffed; SVG never), kept under the fixed name for its kind
        foreach (var kind in new[] { "logo", "favicon" })
        {
            core.MapPost("/branding/" + kind, RouteAccess.Requires(CoreComponent.SettingsEdit), ctx =>
            {
                var ext = ctx.Request.Headers.Get("Content-Type")?.Split(';')[0].Trim().ToLowerInvariant() switch
                {
                    "image/png" => "png",
                    "image/webp" => "webp",
                    "image/x-icon" or "image/vnd.microsoft.icon" => "ico",
                    _ => throw new HttpError(422, "a PNG, WebP or ICO image (Content-Type image/png, image/webp or image/x-icon)"),
                };
                var bytes = ctx.Request.Body;
                if (bytes.Length is 0 or > BrandingFiles.MaxBytes) throw new HttpError(422, $"the image must be 1 byte to {BrandingFiles.MaxBytes / 1024} KiB");
                if (!BrandingFiles.Matches(ext, bytes.Span)) throw new HttpError(422, $"that file is not a {ext.ToUpperInvariant()} image");
                var name = $"{kind}.{ext}";
                Directory.CreateDirectory(files.Folder);
                foreach (var other in kind == "logo" ? BrandingFiles.LogoNames : BrandingFiles.FaviconNames)
                    if (other != name) File.Delete(Path.Combine(files.Folder, other));
                Storage.AtomicFile.Write(Path.Combine(files.Folder, name), bytes.Span, secret: false);
                var cur = settings.Current;
                settings.Save(kind == "logo"
                    ? new BrandingSettings { Format = cur.Format, Name = cur.Name, Accent = cur.Accent, Logo = name, Favicon = cur.Favicon }
                    : new BrandingSettings { Format = cur.Format, Name = cur.Name, Accent = cur.Accent, Logo = cur.Logo, Favicon = name });
                AuditBranding(ctx, $"core.branding.{kind}", new JsonObject { ["file"] = name, ["bytes"] = bytes.Length });
                return ValueTask.FromResult(HttpResponse.Json(new JsonObject { ["ok"] = true, ["file"] = name }));
            }, BrandingFiles.MaxBytes);
            core.MapDelete("/branding/" + kind, RouteAccess.Requires(CoreComponent.SettingsEdit), ctx =>
            {
                foreach (var n in kind == "logo" ? BrandingFiles.LogoNames : BrandingFiles.FaviconNames) File.Delete(Path.Combine(files.Folder, n));
                var cur = settings.Current;
                settings.Save(kind == "logo"
                    ? new BrandingSettings { Format = cur.Format, Name = cur.Name, Accent = cur.Accent, Logo = null, Favicon = cur.Favicon }
                    : new BrandingSettings { Format = cur.Format, Name = cur.Name, Accent = cur.Accent, Logo = cur.Logo, Favicon = null });
                AuditBranding(ctx, $"core.branding.{kind}", new JsonObject { ["removed"] = true });
                return ValueTask.FromResult(HttpResponse.Empty());
            });
        }
        core.MapGet("/branding/logo", RouteAccess.Public, ctx => ValueTask.FromResult(Serve(ctx, files.Find(settings.Current.Logo, BrandingFiles.LogoNames))));
        core.MapGet("/branding/favicon", RouteAccess.Public, ctx => ValueTask.FromResult(Serve(ctx, files.Find(settings.Current.Favicon, BrandingFiles.FaviconNames))));
    }

    private static void AuditBranding(RequestContext ctx, string op, JsonObject args) =>
        ctx.Services.TryGet<Security.IAuditSink>()?.Record(new Security.AuditEntry(DateTimeOffset.UtcNow, ctx.User?.Id ?? "", ctx.User?.Name ?? "", op, args, true, null, 0,
            ctx.ClientAddress));

    private static HttpResponse Serve(RequestContext ctx, BrandFile? f)
    {
        if (f is null) return HttpResponse.NotFound();
        var r = new HttpResponse { Status = 200 };
        r.Headers.Set("Content-Type", f.ContentType);
        r.Headers.Set("X-Content-Type-Options", "nosniff");
        r.Headers.Set("Cache-Control", "no-cache");
        r.Headers.Set("ETag", f.ETag);
        r.Headers.Set("Content-Security-Policy", "default-src 'none'; sandbox");
        if (ctx.Request.Headers.Get("If-None-Match") is { } inm && inm.Split(',').Any(t => t.Trim().TrimStart('W', '/') == f.ETag))
        {
            r.Status = 304;
            return r;
        }
        r.Body = f.Bytes;
        return r;
    }
}

/// <summary>The name the panel shows for this server everywhere (sign-in page, public page, webhooks): branding.json's,
/// else enshrouded_server.json's "name", else the product name.</summary>
public sealed class ServerDisplayName(Func<string> current)
{
    public string Current => current();
}

/// <summary>enshrouded_server.json's "name" (re-read at most every 30 s), or null.</summary>
internal sealed class GameServerName(string serverDirectory, TimeProvider time)
{
    private readonly object _gate = new();
    private DateTimeOffset _readAt = DateTimeOffset.MinValue;
    private string? _name;

    public string? Current
    {
        get
        {
            lock (_gate)
            {
                var now = time.GetUtcNow();
                if (now - _readAt < TimeSpan.FromSeconds(30)) return _name;
                _readAt = now;
                _name = Read();
                return _name;
            }
        }
    }

    private string? Read()
    {
        try
        {
            var cfg = Path.Combine(serverDirectory, "enshrouded_server.json");
            var fi = new FileInfo(cfg);
            if (!fi.Exists || fi.Length > 1 << 20) return null;
            return JsonNode.Parse(File.ReadAllBytes(cfg))?["name"] is JsonValue v && v.TryGetValue<string>(out var n) ? BrandingRules.CleanName(n) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}
