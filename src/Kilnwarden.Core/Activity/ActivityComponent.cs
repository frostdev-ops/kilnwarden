// SPDX-License-Identifier: MIT
using System.Text.Json;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Events;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Kap;
using Kilnwarden.Modules;
using Kilnwarden.Routing;
using Kilnwarden.Storage;

namespace Kilnwarden.Activity;

/// <summary>Player activity (DESIGN §5: join/leave events and history): who is on, recent joins, leaves and deaths,
/// and playtime per player, from Kiln.Admin's <c>state</c> topic. The one source of the panel events player.join,
/// player.leave and player.death (webhooks, the public page and the dashboard read it). Files under the data folder:
/// activity/log.jsonl (rotated at 1 MiB, 4 kept) and activity/online.json (who was on when the panel last looked, so a
/// hot reload announces no one twice and a restart closes the sessions at the right time).
/// <list type="bullet">
/// <item>GET /api/activity/online: who is on now ({connected, live, players: [{name, steamId, since, seconds, you}]}).</item>
/// <item>GET /api/activity/events?limit=&amp;before=&amp;player=: the log, newest first (at most the newest 5000 kept in memory).</item>
/// <item>GET /api/activity/players: per player: sessions, playtime in 7 and 30 days, deaths, first and last seen.</item>
/// <item>GET /api/activity/recent: the dashboard widget's data.</item>
/// </list>
/// All need activity.view (moderator). "you" marks the player whose SteamID the signed-in account linked.</summary>
public sealed class ActivityComponent : IWebAdminComponent
{
    public const string ModuleId = "activity", View = "activity.view";
    public const string LogFile = "activity/log.jsonl", OnlineFile = "activity/online.json";
    public const int Keep = 5000;
    public static readonly TimeSpan SaveOnlineEvery = TimeSpan.FromSeconds(60);

    private readonly Func<WebAdminServices, Action<JsonNode?>, IDisposable?>? _feed;
    private readonly PlayerTracker _tracker = new();
    private readonly object _gate = new();
    private readonly LinkedList<ActivityEntry> _recent = new();
    private WebAdminBuilder? _b;
    private PanelEvents? _events;
    private JsonlWriter? _log;
    private IDisposable? _sub;
    private ITimer? _saver;
    private KapClient? _kap;

    public ActivityComponent() : this(null)
    {
    }

    /// <summary>Tests: <paramref name="feed"/> replaces the KAP subscription and hands the test a way to push state data.</summary>
    internal ActivityComponent(Func<WebAdminServices, Action<JsonNode?>, IDisposable?>? feed) => _feed = feed;

    public string Name => "activity";
    public PlayerTracker Tracker => _tracker;

    /// <summary>The newest entries, oldest first (tests, the public page).</summary>
    public IReadOnlyList<ActivityEntry> Recent
    {
        get
        {
            lock (_gate) return [.. _recent];
        }
    }

    public void Configure(WebAdminBuilder b)
    {
        _b = b;
        b.Services.Add(this);
        _events = PanelEvents.Of(b.Services, b.Log);
        _events.Declare(new EventKind("player.join", "A player joined", ["player"], ModuleId));
        _events.Declare(new EventKind("player.leave", "A player left", ["player", "minutes"], ModuleId));
        _events.Declare(new EventKind("player.death", "A player died", ["player", "killer"], ModuleId));
        var m = b.Modules.Register(new ModuleSpec
        {
            Id = ModuleId, Title = "Player activity", Icon = "history",
            Description = "Who is on, joins, leaves and deaths, and playtime per player",
            Permissions = [new PermissionSpec(View, "See who is on, the join and leave history and playtime") { DefaultRole = BuiltinRole.Moderator }],
            Pages = [new PageSpec("activity", "Player activity", PageKind.Builtin, "activity") { Icon = "history", Permission = View, Order = 13 }],
            Widgets = [new WidgetSpec("recent", "Player activity", WidgetSurface.Dashboard, "/api/activity/recent") { Permission = View, Kind = "activity.recent" }],
        }, Product.ModId);
        m.MapGet("/online", RouteAccess.Requires(View), ctx => ValueTask.FromResult(HttpResponse.Json(OnlineJson(ctx))));
        m.MapGet("/events", RouteAccess.Requires(View), ctx =>
        {
            var q = ctx.Request.QueryValues;
            var limit = int.TryParse(q.GetValueOrDefault("limit"), out var l) ? Math.Clamp(l, 1, 500) : 100;
            DateTimeOffset? before = q.GetValueOrDefault("before") is { Length: > 0 } bs
                ? DateTimeOffset.TryParse(bs, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var bv)
                    ? bv : throw new HttpError(400, "before: an ISO 8601 time")
                : null;
            var player = q.GetValueOrDefault("player") is { Length: > 0 and <= 80 } p ? p : null;
            var type = q.GetValueOrDefault("type") is { } t && t is "join" or "leave" or "death" ? t : null;
            List<ActivityEntry> page;
            lock (_gate)
                page = [.. _recent.Reverse().Where(e => (before is null || e.At < before) && (type is null || e.Type == type) &&
                                                       (player is null || PlayerKey(e.Name, e.SteamId) == player))
                    .Take(limit + 1)];
            var more = page.Count > limit;
            if (more) page.RemoveAt(page.Count - 1);
            return ValueTask.FromResult(HttpResponse.Json(new JsonObject
            {
                ["events"] = new JsonArray([.. page.Select(e => (JsonNode)EntryJson(e))]),
                ["more"] = more,
            }));
        });
        m.MapGet("/players", RouteAccess.Requires(View), ctx => ValueTask.FromResult(HttpResponse.Json(PlayersJson(ctx))));
        m.MapGet("/recent", RouteAccess.Requires(View), ctx =>
        {
            List<ActivityEntry> last;
            lock (_gate) last = [.. _recent.Reverse().Take(8)];
            var online = OnlineJson(ctx);
            online["recent"] = new JsonArray([.. last.Select(e => (JsonNode)EntryJson(e))]);
            return ValueTask.FromResult(HttpResponse.Json(online));
        });
    }

    /// <summary>A player's key in the routes: "s:&lt;SteamID&gt;", else "n:&lt;name&gt;".</summary>
    public static string PlayerKey(string name, string? steamId) => steamId is not null ? "s:" + steamId : "n:" + name;

    private static string? LinkedSteam(RequestContext ctx) =>
        ctx.User is WebPrincipal p && p.Account.Links.TryGetValue("steam", out var s) ? s : null;

    private JsonObject OnlineJson(RequestContext ctx)
    {
        var me = LinkedSteam(ctx);
        var now = _b!.Environment.Time.GetUtcNow();
        return new JsonObject
        {
            ["connected"] = _kap?.IsReady ?? _feed is not null,
            ["live"] = _tracker.Live,
            ["players"] = new JsonArray([.. _tracker.Online.Select(p => (JsonNode)new JsonObject
            {
                ["key"] = PlayerKey(p.Name, p.SteamId), ["name"] = p.Name, ["steamId"] = p.SteamId, ["since"] = p.Since.ToString("O"),
                ["seconds"] = Math.Max(0, Math.Round((now - p.Since).TotalSeconds)), ["you"] = me is not null && p.SteamId == me,
            })]),
        };
    }

    private static JsonObject EntryJson(ActivityEntry e) => new()
    {
        ["type"] = e.Type, ["at"] = e.At.ToString("O"), ["name"] = e.Name, ["steamId"] = e.SteamId, ["key"] = PlayerKey(e.Name, e.SteamId),
        ["seconds"] = e.Seconds, ["detail"] = e.Detail,
    };

    private JsonObject PlayersJson(RequestContext ctx)
    {
        var me = LinkedSteam(ctx);
        var now = _b!.Environment.Time.GetUtcNow();
        var online = _tracker.Online.ToDictionary(p => PlayerKey(p.Name, p.SteamId), StringComparer.Ordinal);
        var rows = new Dictionary<string, Summary>(StringComparer.Ordinal);
        List<ActivityEntry> all;
        lock (_gate) all = [.. _recent];
        foreach (var e in all)
        {
            var key = PlayerKey(e.Name, e.SteamId);
            if (!rows.TryGetValue(key, out var r)) rows[key] = r = new Summary { Name = e.Name, SteamId = e.SteamId, First = e.At };
            r.Name = e.Name;
            r.Last = e.At > r.Last ? e.At : r.Last;
            switch (e.Type)
            {
                case "join":
                    r.Sessions++;
                    break;
                case "leave" when e.Seconds is { } secs:
                    if (e.At > now.AddDays(-30)) r.Seconds30 += secs;
                    if (e.At > now.AddDays(-7)) r.Seconds7 += secs;
                    break;
                case "death":
                    r.Deaths++;
                    break;
            }
        }
        foreach (var (key, p) in online)
        {
            if (!rows.TryGetValue(key, out var r)) rows[key] = r = new Summary { Name = p.Name, SteamId = p.SteamId, First = p.Since, Sessions = 1 };
            var secs = Math.Max(0, (now - p.Since).TotalSeconds);
            r.Seconds30 += secs;
            r.Seconds7 += secs;
            r.Last = now;
        }
        return new JsonObject
        {
            ["since"] = all.Count > 0 ? all[0].At.ToString("O") : null,
            ["players"] = new JsonArray([.. rows.OrderByDescending(kv => online.ContainsKey(kv.Key)).ThenByDescending(kv => kv.Value.Last).Select(kv => (JsonNode)new JsonObject
            {
                ["key"] = kv.Key, ["name"] = kv.Value.Name, ["steamId"] = kv.Value.SteamId, ["online"] = online.ContainsKey(kv.Key),
                ["you"] = me is not null && kv.Value.SteamId == me, ["sessions"] = kv.Value.Sessions,
                ["seconds7"] = Math.Round(kv.Value.Seconds7), ["seconds30"] = Math.Round(kv.Value.Seconds30), ["deaths"] = kv.Value.Deaths,
                ["firstSeen"] = kv.Value.First.ToString("O"), ["lastSeen"] = kv.Value.Last.ToString("O"),
            })]),
        };
    }

    private sealed class Summary
    {
        public string Name = "";
        public string? SteamId;
        public DateTimeOffset First, Last;
        public int Sessions, Deaths;
        public double Seconds7, Seconds30;
    }

    public Task StartAsync(WebAdminServices services, CancellationToken ct)
    {
        var b = _b!;
        _log = b.Data.OpenJsonl(LogFile, rotateBytes: 1 << 20, keep: 4);
        LoadLog();
        if (b.Data.ReadJson<OnlineState>(OnlineFile) is { Players: { } restored } st)
            _tracker.Restore(restored.Where(p => p is { Name.Length: > 0 and <= 64 } && (p.SteamId is null || PlayerTracker.IsSteamId(p.SteamId)))
                .Select(p => new OnlinePlayer(p.Name, p.SteamId, p.Since)), st.At);
        if (_feed is not null) _sub = _feed(services, data => OnState(data));
        else if (services.TryGet<KapClient>() is { } kap)
        {
            _kap = kap;
            _sub = kap.Subscribe("state", push =>
            {
                JsonNode? data;
                try
                {
                    data = JsonNode.Parse(push.Payload.Span, documentOptions: new JsonDocumentOptions { MaxDepth = 32 })?["data"];
                }
                catch (JsonException)
                {
                    return;
                }
                OnState(data);
            });
        }
        _saver = b.Environment.Time.CreateTimer(_ => SaveOnline(force: false), null, SaveOnlineEvery, SaveOnlineEvery);
        return Task.CompletedTask;
    }

    // The log back into memory at start: the newest Keep entries of the files, oldest first.
    private void LoadLog()
    {
        var files = _log!.Files();
        var list = new List<ActivityEntry>();
        foreach (var f in files.Reverse())
        {
            try
            {
                foreach (var line in JsonlWriter.ReadLines(f))
                    if (line.Deserialize<ActivityEntry>(DataStore.Json) is { Name.Length: > 0, Type: "join" or "leave" or "death" } e) list.Add(e);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                _b!.Log.Warn($"{LogFile}: {Path.GetFileName(f)} is unreadable ({e.Message}); its entries are skipped");
            }
        }
        lock (_gate)
            foreach (var e in list.OrderBy(e => e.At).TakeLast(Keep)) _recent.AddLast(e);
    }

    /// <summary>One state push (the KAP subscription's thread, or a test).</summary>
    internal void OnState(JsonNode? data)
    {
        var now = _b!.Environment.Time.GetUtcNow();
        var changes = _tracker.Observe(data, now);
        if (changes.Count == 0) return;
        foreach (var c in changes)
        {
            var e = c.Entry;
            lock (_gate)
            {
                _recent.AddLast(e);
                while (_recent.Count > Keep) _recent.RemoveFirst();
            }
            try
            {
                _log?.Append(e);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _b.Log.Warn($"{LogFile}: cannot write ({ex.Message})");
            }
            if (!c.Announce) continue;
            switch (e.Type)
            {
                case "join":
                    _events!.Publish("player.join", new JsonObject { ["player"] = e.Name }, e.At);
                    break;
                case "leave":
                    _events!.Publish("player.leave", new JsonObject { ["player"] = e.Name, ["minutes"] = Math.Round((e.Seconds ?? 0) / 60.0) }, e.At);
                    break;
                case "death":
                    _events!.Publish("player.death", new JsonObject { ["player"] = e.Name, ["killer"] = e.Detail ?? "" }, e.At);
                    break;
            }
        }
        if (changes.Any(c => c.Entry.Type is "join" or "leave")) SaveOnline(force: true);
    }

    private sealed class OnlineState
    {
        public int Format { get; set; } = 1;
        public DateTimeOffset At { get; set; }
        public List<OnlinePlayer>? Players { get; set; }
    }

    private void SaveOnline(bool force)
    {
        var b = _b;
        if (b is null) return;
        // the timer refreshes "last seen" only while someone is on (a join or leave always saves)
        if (!force && _tracker.Online.Count == 0) return;
        try
        {
            b.Data.WriteJson(OnlineFile, new OnlineState { At = b.Environment.Time.GetUtcNow(), Players = [.. _tracker.Online] });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            b.Log.Warn($"{OnlineFile}: cannot save ({e.Message})");
        }
    }

    public Task StopAsync(CancellationToken ct)
    {
        _sub?.Dispose();
        _sub = null;
        _saver?.Dispose();
        if (_tracker.Live) SaveOnline(force: true);
        _log?.Dispose();
        return Task.CompletedTask;
    }
}
