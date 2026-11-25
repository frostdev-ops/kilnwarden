// SPDX-License-Identifier: MIT
using System.Text.Json;
using System.Text.Json.Nodes;
using Kilnwarden.Events;
using Kilnwarden.Hosting;
using Kilnwarden.Kap;

namespace Kilnwarden.Watch;

/// <summary>Server events Kiln.Admin has no topic for, by polling as the panel (DESIGN §5 Webhooks: restart, crash; §5
/// Metrics: mod faults, save time): admin_info (restart requests, every 10 s), mods (fault counts, every 30 s),
/// crash_reports (new reports, every 60 s) and save_status (the last save, every 30 s). Publishes server.restart,
/// server.restart_canceled, server.restart_failed, mod.fault, crash.report and save.failed; the first answer after a
/// start only sets what is already there. Only ops the server lists in ready.ops are asked.</summary>
public sealed class ServerWatch(PanelEvents events)
{
    public const string Owner = "server-watch";

    private readonly object _gate = new();
    private string? _restartKey;
    private string? _restartState;
    private bool _restartSeen;
    private Dictionary<string, long>? _faults;
    private HashSet<string>? _crashes;
    private long? _failedSaves;
    private DateTimeOffset? _lastSaved;
    private long _modFaults = -1;

    /// <summary>The faults of every loaded mod added up (null before the first mods answer).</summary>
    public long? ModFaults
    {
        get
        {
            var v = Interlocked.Read(ref _modFaults);
            return v < 0 ? null : v;
        }
    }

    /// <summary>Seconds since the last world save (null when not known).</summary>
    public double? SaveAgeSeconds(DateTimeOffset now)
    {
        lock (_gate) return _lastSaved is { } t ? Math.Max(0, (now - t).TotalSeconds) : null;
    }

    public void Declare()
    {
        events.Declare(new EventKind("server.restart", "A server restart (or stop) was requested", ["by", "minutes", "message", "supported"], Owner));
        events.Declare(new EventKind("server.restart_canceled", "A restart countdown was canceled", ["by"], Owner));
        events.Declare(new EventKind("server.restart_failed", "A restart failed (the server keeps running)", ["by", "error"], Owner));
        events.Declare(new EventKind("mod.fault", "A mod faulted (an exception in its code)", ["mod", "name", "faults"], Owner));
        events.Declare(new EventKind("crash.report", "A new crash report", ["name"], Owner));
        events.Declare(new EventKind("save.failed", "A world save failed", ["failed", "reason"], Owner));
    }

    private static string? Str(JsonNode? n, string name) =>
        n?[name] is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null;

    private static long? Num(JsonNode? n, string name) =>
        Extensions.JsonSchemaLite.Num(n?[name]) is { } d && double.IsFinite(d) && Math.Abs(d) < 9e15 ? (long)d : null;

    private static string Cut(string? s, int max) => s is null ? "" : s.Length <= max ? s : s[..max];

    /// <summary>admin_info's data: a new request (by its "requested" time) while counting is news, and so is its end.</summary>
    public void OnAdminInfo(JsonNode? info)
    {
        var restart = info?["restart"];
        var req = restart?["request"] as JsonObject;
        var state = Str(req, "state");
        var key = req is null ? null : $"{Str(req, "requested")}|{Str(req, "by")}";
        JsonObject? payload = null;
        string? topic = null;
        lock (_gate)
        {
            var first = !_restartSeen;
            _restartSeen = true;
            var isNew = key != _restartKey;
            var changed = isNew || state != _restartState;
            _restartKey = key;
            _restartState = state;
            if (first || !changed || req is null) return;
            var by = Cut(Str(req, "by"), 120);
            switch (state)
            {
                case "counting" when isNew:
                    topic = "server.restart";
                    payload = new JsonObject
                    {
                        ["by"] = by, ["minutes"] = (int)Math.Round((Num(req, "secondsLeft") ?? 0) / 60.0), ["message"] = Cut(Str(req, "message"), 200),
                        ["supported"] = Str(restart, "supported") ?? Str(req["restart"], "supported") ?? "unknown",
                    };
                    break;
                case "canceled":
                    topic = "server.restart_canceled";
                    payload = new JsonObject { ["by"] = by };
                    break;
                case "failed":
                    topic = "server.restart_failed";
                    payload = new JsonObject { ["by"] = by, ["error"] = Cut(Str(req, "error"), 200) };
                    break;
            }
        }
        if (topic is not null) events.Publish(topic, payload);
    }

    /// <summary>The mods op's data: per mod its fault count; a count that grows is a mod.fault event.</summary>
    public void OnMods(JsonNode? mods)
    {
        if (mods is not JsonArray arr) return;
        var now = new Dictionary<string, long>(StringComparer.Ordinal);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var m in arr.Take(1000))
        {
            if (Str(m, "id") is not { Length: > 0 and <= 80 } id) continue;
            now[id] = Math.Max(0, Num(m, "faults") ?? 0);
            names[id] = Cut(Str(m, "name") ?? id, 80);
        }
        List<JsonObject> news = [];
        lock (_gate)
        {
            if (_faults is not null)
                foreach (var (id, n) in now)
                    if (n > _faults.GetValueOrDefault(id)) news.Add(new JsonObject { ["mod"] = id, ["name"] = names[id], ["faults"] = n });
            _faults = now;
        }
        Interlocked.Exchange(ref _modFaults, now.Values.Sum());
        foreach (var p in news) events.Publish("mod.fault", p);
    }

    /// <summary>crash_reports' list: a name not seen before is a crash.report event.</summary>
    public void OnCrashReports(JsonNode? list)
    {
        if (list is not JsonArray arr) return;
        var names = arr.Take(200).Select(r => Str(r, "name")).OfType<string>().Where(n => n.Length is > 0 and <= 120).ToHashSet(StringComparer.Ordinal);
        List<string> news = [];
        lock (_gate)
        {
            if (_crashes is not null) news = [.. names.Where(n => !_crashes.Contains(n))];
            _crashes = names;
        }
        foreach (var n in news) events.Publish("crash.report", new JsonObject { ["name"] = n });
    }

    /// <summary>save_status' data: the last save's time, and failed saves (a growing count is a save.failed event).</summary>
    public void OnSaveStatus(JsonNode? status)
    {
        if (status is not JsonObject) return;
        DateTimeOffset? saved = Str(status, "lastSaved") is { } s && DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out var t) ? t : null;
        var failed = Num(status, "failed");
        JsonObject? news = null;
        lock (_gate)
        {
            if (saved is not null) _lastSaved = saved;
            if (failed is { } f)
            {
                if (_failedSaves is { } before && f > before) news = new JsonObject { ["failed"] = f, ["reason"] = Cut(Str(status, "lastReason"), 64) };
                _failedSaves = f;
            }
        }
        if (news is not null) events.Publish("save.failed", news);
    }

    /// <summary>One round of polls that are due (the component's timer; every call checks ready.ops).</summary>
    internal async Task PollAsync(KapClient kap, long tick, CancellationToken ct)
    {
        if (kap.Session is not { } session) return;
        async Task Ask(string op, Action<JsonNode?> into)
        {
            if (!session.Ops.Contains(op)) return;
            try
            {
                var r = await kap.CallAsync(KapCaller.Panel, op, null, ct).ConfigureAwait(false);
                if (r.Ok) into(r.Data);
            }
            catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // the next round asks again
            }
        }
        await Ask("admin_info", OnAdminInfo).ConfigureAwait(false);
        if (tick % 3 == 0)
        {
            await Ask("mods", OnMods).ConfigureAwait(false);
            await Ask("save_status", OnSaveStatus).ConfigureAwait(false);
        }
        if (tick % 6 == 0) await Ask("crash_reports", OnCrashReports).ConfigureAwait(false);
    }
}

/// <summary>Runs <see cref="ServerWatch"/> every 10 s while Kiln.Admin is connected.</summary>
public sealed class ServerWatchComponent : IWebAdminComponent
{
    public static readonly TimeSpan Period = TimeSpan.FromSeconds(10);
    private ServerWatch? _watch;
    private WebAdminBuilder? _b;
    private ITimer? _timer;
    private readonly CancellationTokenSource _life = new();
    private int _busy;
    private long _tick;

    public string Name => "server-watch";
    public ServerWatch? Watch => _watch;

    public void Configure(WebAdminBuilder b)
    {
        _b = b;
        _watch = new ServerWatch(PanelEvents.Of(b.Services, b.Log));
        _watch.Declare();
        b.Services.Add(_watch);
    }

    public Task StartAsync(WebAdminServices services, CancellationToken ct)
    {
        if (services.TryGet<KapClient>() is not { } kap) return Task.CompletedTask;
        _timer = _b!.Environment.Time.CreateTimer(_ =>
        {
            if (Interlocked.Exchange(ref _busy, 1) != 0) return;
            var tick = _tick++;
            _ = Task.Run(async () =>
            {
                try
                {
                    await _watch!.PollAsync(kap, tick, _life.Token).ConfigureAwait(false);
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    if (!_life.IsCancellationRequested) _b.Log.Warn($"server watch: {e.GetType().Name}");
                }
                finally
                {
                    Volatile.Write(ref _busy, 0);
                }
            });
        }, null, TimeSpan.FromSeconds(3), Period);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _timer?.Dispose();
        _life.Cancel();
        return Task.CompletedTask;
    }
}
