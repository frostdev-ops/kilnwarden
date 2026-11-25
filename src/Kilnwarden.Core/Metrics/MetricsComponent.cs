// SPDX-License-Identifier: MIT
using System.Text.Json;
using System.Text.Json.Nodes;
using Kilnwarden.Config;
using Kilnwarden.Events;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Kap;
using Kilnwarden.Modules;
using Kilnwarden.Routing;
using Kilnwarden.Security;

namespace Kilnwarden.Metrics;

/// <summary>Metrics (DESIGN §5, lane C): Kiln.Admin's metrics topic (plus mod faults and the last save's age from the server
/// watch) into &lt;see cref="MetricsHistory"/&gt;, alert
/// rules into the panel event "metrics.alert". Routes (module "metrics", docs/step2-lane-c.md C2): fields, live,
/// history, alerts (GET metrics.view; PUT metrics.alerts.edit, sensitive).</summary>
public sealed class MetricsComponent : IWebAdminComponent
{
    public const string ModuleId = "metrics", View = "metrics.view", AlertsEdit = "metrics.alerts.edit";
    public const string HistoryFile = "metrics/history.bin", AlertTopic = "metrics.alert";
    public static readonly TimeSpan SaveEvery = TimeSpan.FromMinutes(5);
    private static readonly int ModFaultsIndex = MetricsHistory.IndexOf("modFaults"), SaveAgeIndex = MetricsHistory.IndexOf("saveAgeS");

    private readonly Func<WebAdminServices, IDisposable?>? _feed;
    private MetricsHistory _history = new();
    private readonly AlertEvaluator _alerts = new();
    private SettingsStore<MetricsSettings>? _settings;
    private PanelEvents? _events;
    private WebAdminBuilder? _b;
    private IDisposable? _sub;
    private ITimer? _saver;
    private long _lastPushMs;

    public MetricsComponent() : this(null)
    {
    }

    /// <summary>Tests: &lt;paramref name="feed"/&gt; replaces the KAP subscription (null: none at all).</summary>
    internal MetricsComponent(Func<WebAdminServices, IDisposable?>? feed) => _feed = feed;

    public string Name => "metrics";
    public MetricsHistory History => _history;

    public void Configure(WebAdminBuilder b)
    {
        _b = b;
        _settings = b.SettingsFile<MetricsSettings>("metrics.json", MetricsRules.Validate);
        _events = PanelEvents.Of(b.Services, b.Log);
        _events.Declare(new EventKind(AlertTopic, "A metrics alert fired", ["rule", "metric", "op", "threshold", "value", "forS"], "metrics"));
        b.Services.Add(this);
        var m = b.Modules.Register(new ModuleSpec
        {
            Id = ModuleId, Title = "Metrics", Icon = "activity",
            Description = "Players, frame and tick time, memory, CPU: live and history, with alerts",
            Permissions =
            [
                new PermissionSpec(View, "See the server's metrics") { DefaultRole = BuiltinRole.Moderator },
                new PermissionSpec(AlertsEdit, "Change metric alerts") { DefaultRole = BuiltinRole.Admin, Sensitive = true },
            ],
            Pages = [new PageSpec("metrics", "Metrics", PageKind.Builtin, "metrics") { Icon = "activity", Permission = View, Order = 86 }],
            Widgets =
            [
                new WidgetSpec("players", "Players", WidgetSurface.Dashboard, "/api/metrics/live") { Permission = View, Kind = "metrics.players" },
                new WidgetSpec("performance", "Server performance", WidgetSurface.Dashboard, "/api/metrics/history?range=1h") { Permission = View, Kind = "metrics.chart" },
            ],
        }, Product.ModId);
        m.MapGet("/fields", RouteAccess.Requires(View), _ => ValueTask.FromResult(HttpResponse.Json(FieldsJson())));
        m.MapGet("/live", RouteAccess.Requires(View), _ => ValueTask.FromResult(HttpResponse.Json(LiveJson())));
        m.MapGet("/history", RouteAccess.Requires(View), ctx =>
        {
            var range = ctx.Request.QueryValues.GetValueOrDefault("range") ?? "1h";
            var s = _history.Series(range, b.Environment.Time.GetUtcNow().ToUnixTimeMilliseconds()) ?? throw new HttpError(400, "range: 10m, 1h, 6h, 24h, 7d or 30d");
            s["fields"] = FieldsJson()["fields"]!.DeepClone();
            return ValueTask.FromResult(HttpResponse.Json(s));
        });
        m.MapGet("/alerts", RouteAccess.Requires(View), _ => ValueTask.FromResult(HttpResponse.Json(new
        {
            maxAlertsPerHour = _settings.Current.MaxAlertsPerHour, alerts = _settings.Current.Alerts, history = _settings.Current.History,
        })));
        m.MapPut("/alerts", RouteAccess.Requires(AlertsEdit), ctx =>
        {
            var body = ctx.ReadJson<AlertsBody>();
            var cur = _settings.Current;
            var next = new MetricsSettings
            {
                Format = cur.Format, History = body.History ?? cur.History, MaxAlertsPerHour = body.MaxAlertsPerHour ?? cur.MaxAlertsPerHour,
                Alerts = body.Alerts ?? cur.Alerts,
            };
            try
            {
                _settings.Save(next);
            }
            catch (ArgumentException e)
            {
                throw new HttpError(422, e.Message);
            }
            ctx.Services.TryGet<IAuditSink>()?.Record(new AuditEntry(DateTimeOffset.UtcNow, ctx.User!.Id, ctx.User.Name, AlertsEdit,
                new JsonObject { ["rules"] = next.Alerts.Count, ["history"] = next.History }, true, null, 0, ctx.ClientAddress));
            return ValueTask.FromResult(HttpResponse.Json(new { ok = true }));
        });
    }

    private sealed class AlertsBody
    {
        public bool? History { get; set; }
        public int? MaxAlertsPerHour { get; set; }
        public List<AlertRule>? Alerts { get; set; }
    }

    private JsonObject FieldsJson()
    {
        var arr = new JsonArray();
        for (var i = 0; i < MetricsHistory.FieldCount; i++)
        {
            var f = MetricsHistory.Fields[i];
            var seen = _history.Seen(i);
            arr.Add(new JsonObject
            {
                ["key"] = f.Key, ["title"] = f.Title, ["unit"] = f.Unit, ["available"] = seen,
                ["reason"] = seen ? null : "no value from Kiln.Admin yet (null in its metrics, or not connected)",
            });
        }
        foreach (var (key, why) in MetricsHistory.Unavailable)
            arr.Add(new JsonObject { ["key"] = key, ["title"] = MetricsHistory.UnavailableTitles.GetValueOrDefault(key, key), ["unit"] = null, ["available"] = false, ["reason"] = why });
        return new JsonObject { ["fields"] = arr };
    }

    private JsonObject LiveJson()
    {
        var l = _history.Latest;
        var o = new JsonObject
        {
            ["connected"] = _b?.Services.TryGet<KapClient>()?.IsReady ?? false,
            ["at"] = l?.At,
        };
        var values = new JsonObject();
        for (var i = 0; i < MetricsHistory.FieldCount; i++)
            values[MetricsHistory.Fields[i].Key] = l is { } x && !float.IsNaN(x.Values[i]) ? JsonValue.Create(Math.Round(x.Values[i], 3)) : null;
        o["values"] = values;
        return o;
    }

    /// <summary>One sample in (the feed's thread): history, then alerts.</summary>
    public void Add(long atMs, float[] values)
    {
        // the fields Kiln.Admin's topic does not carry, from the server watch (mods and save_status polls)
        if (_b?.Services.TryGet<Watch.ServerWatch>() is { } watch)
        {
            if (watch.ModFaults is { } faults && float.IsNaN(values[ModFaultsIndex])) values[ModFaultsIndex] = faults;
            if (watch.SaveAgeSeconds(DateTimeOffset.FromUnixTimeMilliseconds(atMs)) is { } age && float.IsNaN(values[SaveAgeIndex])) values[SaveAgeIndex] = (float)age;
        }
        Volatile.Write(ref _lastPushMs, atMs);
        _history.Add(atMs, values);
        if (_settings is null) return;
        foreach (var a in _alerts.Evaluate(_settings.Current, atMs, values)) _events?.Publish(AlertTopic, a, DateTimeOffset.FromUnixTimeMilliseconds(atMs));
    }

    public Task StartAsync(WebAdminServices services, CancellationToken ct)
    {
        var b = _b!;
        if (_settings!.Current.History && b.Data.ReadBytes(HistoryFile) is { } bytes && !_history.Load(bytes))
            b.Log.Warn($"{HistoryFile}: not a history this version reads; starting a new one");
        if (_feed is not null) _sub = _feed(services);
        else if (services.TryGet<KapClient>() is { } kap)
            _sub = kap.Subscribe("metrics", push =>
            {
                try
                {
                    if (MetricsHistory.Parse(JsonNode.Parse(push.Payload.Span)?["data"]) is { } s) Add(s.At, s.Values);
                }
                catch (JsonException)
                {
                    // a malformed push is skipped
                }
            });
        _saver = b.Environment.Time.CreateTimer(_ => Save(), null, SaveEvery, SaveEvery);
        return Task.CompletedTask;
    }

    internal void Save()
    {
        var b = _b;
        if (b is null || _settings?.Current.History != true) return;
        try
        {
            b.Data.WriteBytes(HistoryFile, _history.Save());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            b.Log.Warn($"{HistoryFile}: cannot save: {e.Message}");
        }
    }

    public Task StopAsync(CancellationToken ct)
    {
        _sub?.Dispose();
        _saver?.Dispose();
        Save();
        return Task.CompletedTask;
    }
}
