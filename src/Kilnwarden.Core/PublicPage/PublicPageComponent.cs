// SPDX-License-Identifier: MIT
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kilnwarden.Branding;
using Kilnwarden.Config;
using Kilnwarden.Extensions;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Kap;
using Kilnwarden.Modules;
using Kilnwarden.Routing;
using Kilnwarden.Security;

namespace Kilnwarden.PublicPage;

/// <summary>public.json: the public page is off until the owner turns it on; each part has its own switch, the ones
/// that name players or show positions off.</summary>
public sealed class PublicSettings
{
    public int Format { get; set; } = 1;
    public bool Enabled { get; set; }
    public bool ShowStatus { get; set; } = true;
    public bool ShowPlayerCount { get; set; } = true;
    public bool ShowPlayerNames { get; set; }

    /// <summary>Map dots: where players are, rounded to <see cref="MapDotsGrid"/> metres, without names (off by default).</summary>
    public bool ShowMapDots { get; set; }

    /// <summary>The grid dots snap to, in metres (25 to 1000): a dot is the middle of the player's grid cell.</summary>
    public int MapDotsGrid { get; set; } = 100;

    public bool ShowMods { get; set; }
    public string Rules { get; set; } = "";
    public string JoinInfo { get; set; } = "";

    /// <summary>Extension public widgets shown ("module/widget"); none by default.</summary>
    public List<string> Widgets { get; set; } = [];
}

public static class PublicRules
{
    public const int MaxRules = 4000, MaxJoin = 500;

    public static IReadOnlyList<string> Validate(PublicSettings s)
    {
        var p = new List<string>();
        if ((s.Rules ?? "").Length > MaxRules) p.Add($"rules: at most {MaxRules} characters");
        if ((s.JoinInfo ?? "").Length > MaxJoin) p.Add($"joinInfo: at most {MaxJoin} characters");
        if (s.MapDotsGrid is < 25 or > 1000) p.Add("mapDotsGrid: 25 to 1000 metres");
        if (s.Widgets is null || s.Widgets.Count > 16 || s.Widgets.Any(w => w is null || w.Length > 130)) p.Add("widgets: at most 16 module/widget names");
        return p;
    }
}

/// <summary>The optional public page (DESIGN §5, lane C; contract docs/step2-lane-c.md C4): anonymous, read-only routes
/// that serve one cached snapshot made only of allow-listed fields. Nothing on them calls KAP for the visitor, reads a
/// store, or returns an admin answer: the snapshot is rebuilt by the panel at most every 15 s from the KAP connection
/// state, the state topic's player names, the mods op's id/name/version, the branding name and the owner's texts.</summary>
public sealed class PublicPageComponent : IWebAdminComponent
{
    public const string ModuleId = "public", Edit = "public.edit";
    public static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(15);

    private SettingsStore<PublicSettings>? _settings;
    private WebAdminBuilder? _b;
    private KapClient? _kap;
    private IDisposable? _stateSub;
    private readonly object _gate = new();
    private string[] _names = [];
    private int _count = -1;
    private (double X, double Z)[] _positions = [];

    /// <summary>The Enshrouded world is this many metres square (x and z from 0).</summary>
    public const int WorldMetres = 10240;
    private JsonArray? _mods;
    private long? _modsAtMs;
    private bool _modsFetching;
    private (long AtMs, JsonObject Snapshot)? _cache;
    private int _cacheGeneration;

    public string Name => "public";

    public void Configure(WebAdminBuilder b)
    {
        _b = b;
        _settings = b.SettingsFile<PublicSettings>("public.json", PublicRules.Validate);
        _settings.Changed += (_, _) =>
        {
            lock (_gate) InvalidateCache();
            EnsureState();
            PrefetchMods();
        };
        var m = b.Modules.Register(new ModuleSpec
        {
            Id = ModuleId, Title = "Public page", Icon = "globe",
            Description = "An optional page anyone can open: online or not, player count, rules, join info",
            Permissions = [new PermissionSpec(Edit, "Turn the public page on and choose what it shows") { DefaultRole = BuiltinRole.Admin, Sensitive = true }],
            Pages = [new PageSpec("public", "Public page", PageKind.Builtin, "public-page") { Icon = "globe", Permission = Edit, Order = 940 }],
        }, Product.ModId);

        m.MapGet("/status", RouteAccess.Public, ctx => ValueTask.FromResult(_settings.Current.Enabled ? Cached(ctx, Json(Snapshot())) : HttpResponse.NotFound()));
        m.MapGet("/page", RouteAccess.Public, ctx => ValueTask.FromResult(_settings.Current.Enabled
            ? Cached(ctx, PublicHtml.Render(Snapshot(), b.Services.TryGet<SettingsStore<BrandingSettings>>()?.Current.Accent))
            : HttpResponse.NotFound()));
        m.MapGet("/settings", RouteAccess.Requires(Edit), _ => ValueTask.FromResult(HttpResponse.Json(new JsonObject
        {
            ["settings"] = JsonSerializer.SerializeToNode(_settings.Current, DataStore()),
            ["widgets"] = new JsonArray([.. Widgets().Select(w => (JsonNode)new JsonObject { ["key"] = w.Key, ["title"] = w.Title })]),
            ["mapDots"] = new JsonObject { ["available"] = true, ["worldMetres"] = WorldMetres },
        })));
        m.MapPut("/settings", RouteAccess.Requires(Edit), ctx =>
        {
            var next = ctx.ReadJson<PublicSettings>();
            try
            {
                _settings.Save(next);
            }
            catch (ArgumentException e)
            {
                throw new HttpError(422, e.Message);
            }
            ctx.Services.TryGet<IAuditSink>()?.Record(new AuditEntry(DateTimeOffset.UtcNow, ctx.User!.Id, ctx.User.Name, "public.edit",
                new JsonObject { ["enabled"] = next.Enabled, ["names"] = next.ShowPlayerNames, ["mods"] = next.ShowMods }, true, null, 0, ctx.ClientAddress));
            return ValueTask.FromResult(HttpResponse.Json(new { ok = true }));
        });
        m.MapGet("/preview", RouteAccess.Requires(Edit), _ => ValueTask.FromResult(HttpResponse.Json(Snapshot(force: true))));
    }

    private static JsonSerializerOptions DataStore() => Storage.DataStore.Json;

    private static HttpResponse Json(JsonObject o) => HttpResponse.Json(o);

    /// <summary>Anonymous answers may be cached by the browser and shared caches for &lt;see cref="CacheFor"/&gt;: they hold
    /// nothing per user. A response that carries a cookie change (a stale session cookie cleared) is never cacheable.</summary>
    private static HttpResponse Cached(RequestContext ctx, HttpResponse r)
    {
        r.Headers.Set("Cache-Control", ctx.ResponseHeaders.Get("Set-Cookie") is null ? $"public, max-age={(int)CacheFor.TotalSeconds}" : "no-store");
        return r;
    }

    private IReadOnlyList<(string Key, string Title, string Kind, Func<JsonNode?> Data)> Widgets() =>
        _b?.Services.TryGet<ExtensionHost>()?.PublicWidgets() ?? [];

    /// <summary>The snapshot (rebuilt at most every 15 s; &lt;paramref name="force"/&gt;: now, for the owner's preview).</summary>
    internal JsonObject Snapshot(bool force = false)
    {
        var now = _b!.Environment.Time.GetUtcNow().ToUnixTimeMilliseconds();
        int generation;
        lock (_gate)
        {
            if (!force && _cache is { } c && now - c.AtMs < CacheFor.TotalMilliseconds) return (JsonObject)c.Snapshot.DeepClone();
            generation = _cacheGeneration;
        }
        var s = _settings!.Current;
        var online = _kap?.IsReady ?? false;
        string[] names;
        int count;
        (double X, double Z)[] positions;
        lock (_gate)
        {
            names = _names;
            count = _count;
            positions = _positions;
        }
        var o = new JsonObject
        {
            ["name"] = _b.Services.TryGet<ServerDisplayName>()?.Current ?? Product.Name,
            ["at"] = DateTimeOffset.FromUnixTimeMilliseconds(now).ToString("O"),
        };
        if (s.ShowStatus) o["online"] = online;
        if (s.ShowPlayerCount) o["players"] = online && count >= 0 ? count : null;
        if (s.ShowPlayerNames) o["playerNames"] = online ? new JsonArray([.. names.Take(100).Select(x => (JsonNode)x)]) : new JsonArray();
        if (s.ShowMapDots)
        {
            o["dots"] = online ? Dots(positions, s.MapDotsGrid) : new JsonArray();
            o["dotsGrid"] = s.MapDotsGrid;
        }
        if (s.ShowMods) o["mods"] = Mods(now)?.DeepClone() ?? new JsonArray();
        if (s.Rules is { Length: > 0 } rules) o["rules"] = rules;
        if (s.JoinInfo is { Length: > 0 } join) o["joinInfo"] = join;
        var widgets = new JsonArray();
        foreach (var w in Widgets())
        {
            if (!s.Widgets.Contains(w.Key)) continue;
            JsonNode? data;
            try
            {
                data = w.Data()?.DeepClone();
            }
            catch (Exception)
            {
                data = null;
            }
            if (data is not null && JsonSerializer.SerializeToUtf8Bytes(data).Length > ExtensionModule.MaxPublicWidgetBytes) data = null;
            widgets.Add(new JsonObject { ["key"] = w.Key, ["title"] = w.Title, ["kind"] = w.Kind, ["data"] = data });
        }
        if (widgets.Count > 0) o["widgets"] = widgets;
        if (!force)
            lock (_gate)
                if (generation == _cacheGeneration) _cache = (now, (JsonObject)o.DeepClone());
        return o;
    }

    /// <summary>The mods op as the panel (id, name, version of each loaded mod), refreshed in the background at most every
    /// minute (every 5 s while there is no list yet); the last good list meanwhile. A new list drops the cached snapshot.
    /// (Step 2 lab: the first-attempt sentinel was long.MinValue, and now - long.MinValue overflows to a negative number,
    /// so the op was never called and the status said "mods": null.)</summary>
    private JsonArray? Mods(long now)
    {
        RefreshMods(now);
        lock (_gate) return _mods;
    }

    private void RefreshMods(long now)
    {
        if (_kap is not { IsReady: true } kap) return;
        lock (_gate)
        {
            if (_modsFetching) return;
            if (_modsAtMs is { } at && now - at < (_mods is null ? 5_000 : 60_000)) return;
            _modsAtMs = now;
            _modsFetching = true;
        }
        _ = Task.Run(async () =>
        {
            try
            {
                var r = await kap.CallAsync(KapCaller.Panel, "mods").ConfigureAwait(false);
                if (r.Ok)
                {
                    var list = ModList(r.Data);
                    lock (_gate)
                    {
                        if (_mods is null || !JsonNode.DeepEquals(_mods, list)) InvalidateCache();
                        _mods = list;
                    }
                }
            }
            catch (Exception)
            {
                // keep the last list
            }
            finally
            {
                lock (_gate) _modsFetching = false;
            }
        });
    }

    /// <summary>Positions as grid-cell centres inside the world; no names, order shuffled by sorting, at most 100.</summary>
    internal static JsonArray Dots(IEnumerable<(double X, double Z)> positions, int grid)
    {
        var cells = WorldMetres / grid;
        var arr = new JsonArray();
        foreach (var (x, z) in positions.Where(p => double.IsFinite(p.X) && double.IsFinite(p.Z) && p.X >= 0 && p.Z >= 0 && p.X <= WorldMetres && p.Z <= WorldMetres)
                     .Select(p => (Math.Min(cells - 1, (int)(p.X / grid)), Math.Min(cells - 1, (int)(p.Z / grid))))
                     .Order().Take(100))
            arr.Add(new JsonObject { ["x"] = x * grid + grid / 2, ["z"] = z * grid + grid / 2 });
        return arr;
    }

    // Called under _gate. A snapshot already being built must not put an older value back.
    private void InvalidateCache()
    {
        _cache = null;
        _cacheGeneration++;
    }

    /// <summary>Fetches the list ahead of the first visitor while the page is on and shows mods (start, KAP connected,
    /// settings changed).</summary>
    private void PrefetchMods()
    {
        if (_settings?.Current is { Enabled: true, ShowMods: true } && _b is not null)
            RefreshMods(_b.Environment.Time.GetUtcNow().ToUnixTimeMilliseconds());
    }

    private void OnKapConnected(KapSession _) => PrefetchMods();

    /// <summary>Only id, name and version of each mod (whatever else the op says stays out).</summary>
    internal static JsonArray ModList(JsonNode? data)
    {
        var src = data as JsonArray ?? data?["mods"] as JsonArray ?? [];
        var arr = new JsonArray();
        foreach (var m in src.Take(200))
        {
            string? S(string k) => m?[k] is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() is { Length: <= 80 } s ? s : null : null;
            if (S("id") is not { } id) continue;
            // Kiln.Admin names states "Loaded", "Failed"... (the case is not part of the contract)
            if (S("state") is { } st && !(st.Equals("loaded", StringComparison.OrdinalIgnoreCase) || st.Equals("running", StringComparison.OrdinalIgnoreCase) ||
                                          st.Equals("ok", StringComparison.OrdinalIgnoreCase))) continue;
            arr.Add(new JsonObject { ["id"] = id, ["name"] = S("name") ?? id, ["version"] = S("version") });
        }
        return arr;
    }

    public Task StartAsync(WebAdminServices services, CancellationToken ct)
    {
        _kap = services.TryGet<KapClient>();
        if (_kap is not null) _kap.Connected += OnKapConnected;
        EnsureState();
        PrefetchMods();
        return Task.CompletedTask;
    }

    /// <summary>The state topic only while the page is on and shows players.</summary>
    private void EnsureState()
    {
        if (_kap is null || _settings is null) return;
        var s = _settings.Current;
        var want = s.Enabled && (s.ShowPlayerCount || s.ShowPlayerNames || s.ShowMapDots);
        lock (_gate)
        {
            if (want && _stateSub is null) _stateSub = _kap.Subscribe("state", OnState);
            else if (!want && _stateSub is not null)
            {
                _stateSub.Dispose();
                _stateSub = null;
                _names = [];
                _count = -1;
            }
        }
    }

    private void OnState(KapPush push)
    {
        try
        {
            if (JsonNode.Parse(push.Payload.Span)?["data"]?["players"] is not JsonArray players) return;
            SetPlayers([.. players.Select(p => p?["name"] is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null)
                .OfType<string>().Where(n => n.Length is > 0 and <= 64)], players.Count);
            SetPositions(_settings?.Current.ShowMapDots == true
                ? [.. players.Take(200).Select(p => (X: Extensions.JsonSchemaLite.Num(p?["pos"]?["x"]), Z: Extensions.JsonSchemaLite.Num(p?["pos"]?["z"])))
                    .Where(p => p.X is not null && p.Z is not null).Select(p => (p.X!.Value, p.Z!.Value))]
                : []);
        }
        catch (JsonException)
        {
        }
    }

    internal void SetPositions((double X, double Z)[] positions)
    {
        lock (_gate) _positions = positions;
    }

    internal void SetPlayers(string[] names, int count)
    {
        lock (_gate)
        {
            _names = names;
            _count = count;
        }
    }

    public Task StopAsync(CancellationToken ct)
    {
        if (_kap is not null) _kap.Connected -= OnKapConnected;
        lock (_gate)
        {
            _stateSub?.Dispose();
            _stateSub = null;
        }
        return Task.CompletedTask;
    }
}

/// <summary>The public page as HTML: no script at all, one inline style with a nonce, every text HTML-encoded (rules
/// keep only paragraphs and "- " list lines; no links, no markup).</summary>
public static class PublicHtml
{
    public static HttpResponse Render(JsonObject s, string? accent = null)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        string E(string? x) => WebUtility.HtmlEncode(x ?? "");
        // the panel's Hearth colours (warm dark, ember accent; the owner's branding accent when set); system fonts only
        var ember = accent is { Length: 7 } a && a[0] == '#' && a.Skip(1).All(char.IsAsciiHexDigit) ? a : "#ff8f45";
        var h = new StringBuilder();
        h.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        h.Append("<meta name=\"robots\" content=\"noindex\"><meta name=\"color-scheme\" content=\"dark\"><title>").Append(E(s["name"]?.ToString())).Append("</title><style nonce=\"").Append(nonce).Append("\">");
        h.Append(":root{--bg:#080504;--panel:#110c09;--line:#ffbe8c21;--text:#f5e9db;--muted:#ab9682;--ember:").Append(ember).Append("}");
        h.Append("body{margin:0;font:16px/1.55 \"Alegreya Sans\",\"Segoe UI\",system-ui,sans-serif;background:var(--bg);color:var(--text)}");
        h.Append("main{max-width:680px;margin:0 auto;padding:28px 16px 40px}h1{font:600 2rem/1.15 \"Grenze Gotisch\",Georgia,serif;margin:0 0 12px;color:var(--text)}");
        h.Append(".pill{display:inline-block;padding:2px 12px;border-radius:999px;font-weight:700;font-size:.9rem}.on{background:#1f5a3f;color:#d9f5e7}.off{background:#5a2620;color:#ffd9d2}");
        h.Append(".count{color:var(--muted);margin-left:8px}section{margin-top:18px;padding:14px 18px;background:var(--panel);border:1px solid var(--line);border-radius:10px}");
        h.Append("h2{font:600 .8rem/1.2 system-ui,sans-serif;letter-spacing:.12em;text-transform:uppercase;margin:0 0 10px;color:var(--ember)}h3{font-size:1.1rem;margin:8px 0}h4{font-size:1rem;margin:8px 0}");
        h.Append("ul,ol{margin:0 0 8px;padding-left:22px}p{margin:0 0 8px}small{color:var(--muted)}a{color:var(--ember)}code{font-family:ui-monospace,Consolas,monospace;background:#0a0705;padding:1px 5px;border-radius:4px}");
        h.Append(".map{display:block;width:100%;height:auto;border-radius:8px;background:#0a0705}.map .grid{stroke:var(--line);stroke-width:12}.map .dot{fill:var(--ember);stroke:#170803;stroke-width:24}");
        h.Append("</style></head><body><main>");
        h.Append("<h1>").Append(E(s["name"]?.ToString())).Append("</h1>");
        if (s["online"] is JsonValue on)
            h.Append(on.GetValue<bool>() ? "<span class=\"pill on\">Online</span>" : "<span class=\"pill off\">Offline</span>");
        if (s.ContainsKey("players"))
            h.Append("<span class=\"count\">").Append(s["players"] is JsonValue pv ? E(pv.ToString()) + (pv.ToString() == "1" ? " player" : " players") : "Player count not known").Append("</span>");
        if (s["playerNames"] is JsonArray names && names.Count > 0)
        {
            h.Append("<section><h2>Players online</h2><ul>");
            foreach (var n in names) h.Append("<li>").Append(E(n?.ToString())).Append("</li>");
            h.Append("</ul></section>");
        }
        if (s["dots"] is JsonArray dots)
        {
            var grid = s["dotsGrid"] is JsonValue gv && gv.TryGetValue<int>(out var g) ? g : 100;
            var w = PublicPageComponent.WorldMetres;
            h.Append("<section><h2>Map</h2><svg class=\"map\" viewBox=\"0 0 ").Append(w).Append(' ').Append(w).Append("\" role=\"img\" aria-label=\"")
                .Append(dots.Count == 0 ? "Nobody is on the map" : dots.Count == 1 ? "One player on the map" : $"{dots.Count} players on the map").Append("\">");
            for (var i = 1024; i < w; i += 1024)
                h.Append("<line class=\"grid\" x1=\"").Append(i).Append("\" y1=\"0\" x2=\"").Append(i).Append("\" y2=\"").Append(w).Append("\"/><line class=\"grid\" x1=\"0\" y1=\"")
                    .Append(i).Append("\" x2=\"").Append(w).Append("\" y2=\"").Append(i).Append("\"/>");
            foreach (var d in dots)
                if (d?["x"] is JsonValue x && d["z"] is JsonValue z && x.TryGetValue<int>(out var xi) && z.TryGetValue<int>(out var zi))
                    h.Append("<circle class=\"dot\" cx=\"").Append(xi).Append("\" cy=\"").Append(w - zi).Append("\" r=\"").Append(Math.Max(70, grid / 2)).Append("\"/>");
            h.Append("</svg><p><small>Each dot is a player, placed in a ").Append(grid).Append(" m square. North is up.</small></p></section>");
        }
        if (s["joinInfo"]?.ToString() is { } join) h.Append("<section><h2>How to join</h2>").Append(PublicMarkdown.Render(join)).Append("</section>");
        if (s["rules"]?.ToString() is { } rules) h.Append("<section><h2>Rules</h2>").Append(PublicMarkdown.Render(rules)).Append("</section>");
        if (s["mods"] is JsonArray mods && mods.Count > 0)
        {
            h.Append("<section><h2>Mods</h2><ul>");
            foreach (var m in mods)
                h.Append("<li>").Append(E(m?["name"]?.ToString())).Append(m?["version"] is JsonNode v ? " <small>" + E(v.ToString()) + "</small>" : "").Append("</li>");
            h.Append("</ul></section>");
        }
        if (s["widgets"] is JsonArray ws)
            foreach (var w in ws)
            {
                h.Append("<section><h2>").Append(E(w?["title"]?.ToString())).Append("</h2>");
                var d = w?["data"];
                h.Append(d is JsonValue dv ? "<p>" + E(dv.ToString()) + "</p>" : d is null ? "<p><small>Nothing to show yet.</small></p>" : "<pre>" + E(d.ToJsonString()) + "</pre>");
                h.Append("</section>");
            }
        var at = DateTimeOffset.TryParse(s["at"]?.ToString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var t)
            ? t.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture) : s["at"]?.ToString();
        h.Append("<p><small>Updated ").Append(E(at)).Append(". This page refreshes its data every 15 seconds; reload to see it.</small></p></main></body></html>");
        var r = HttpResponse.Html(200, h.ToString());
        r.Headers.Set("Content-Security-Policy", $"default-src 'none'; style-src 'nonce-{nonce}'; img-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'");
        return r;
    }
}
