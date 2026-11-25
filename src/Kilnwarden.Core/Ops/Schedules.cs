// SPDX-License-Identifier: MIT
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Kilnwarden.Hosting;
using Kilnwarden.Storage;

namespace Kilnwarden.Ops;

/// <summary>When a schedule's moments are (<see cref="ScheduleClock"/>).</summary>
public sealed class ScheduleWhen
{
    /// <summary>daily, interval or once.</summary>
    public string Type { get; set; } = "daily";

    /// <summary>daily: "HH:mm" wall-clock times in the schedule's zone.</summary>
    public List<string>? Times { get; set; }

    /// <summary>daily: sun..sat; null or empty: every day.</summary>
    public List<string>? Days { get; set; }

    public int? EveryMinutes { get; set; }
    public DateTimeOffset? Anchor { get; set; }
    public DateTimeOffset? At { get; set; }
}

/// <summary>One run (or skip, miss, refusal) as the history and the schedule's lastRun keep it.</summary>
public sealed record ScheduleRun(DateTimeOffset At, string Schedule, DateTimeOffset? Slot, string Kind, string Outcome, string? Code, string? Message, string? By);

/// <summary>A schedule (docs/step2-lane-b-ops.md "Schedules").</summary>
public sealed class Schedule
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;

    /// <summary>restart, backup, announce or command.</summary>
    public string Kind { get; set; } = "";

    public ScheduleWhen When { get; set; } = new();
    public string TimeZone { get; set; } = "UTC";
    public bool SkipWhenEmpty { get; set; }

    /// <summary>skip or catchUp.</summary>
    public string Missed { get; set; } = "skip";

    public int CatchUpMinutes { get; set; } = 60;
    public JsonObject Params { get; set; } = new();

    /// <summary>The account it runs for: whoever last saved or enabled it.</summary>
    public ActorRef Actor { get; set; } = new("", "");

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset Updated { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>The newest moment handled (run, skipped, missed or refused). Written before a run is dispatched.</summary>
    public DateTimeOffset? LastSlot { get; set; }

    /// <summary>A moment the owner asked to skip.</summary>
    public DateTimeOffset? SkipSlot { get; set; }

    public ScheduleRun? LastRun { get; set; }

    public static readonly string[] Kinds = ["restart", "backup", "announce", "command"];

    /// <summary>Seconds between the start of the run and the moment (a restart's countdown).</summary>
    [JsonIgnore]
    public int LeadSeconds => Kind == "restart" ? (int)(OpsHttp.Long(Params, "countdownSeconds") ?? 0) : 0;
}

/// <summary>schedules/schedules.json (atomic writes, one lock) and the run history schedules/runs.jsonl (rotated) with
/// an in-memory tail of the newest records.</summary>
public sealed class ScheduleStore : IDisposable
{
    public const string File = "schedules/schedules.json", RunsFile = "schedules/runs.jsonl";
    public const int Max = 100, Tail = 1000;

    private readonly DataStore _data;
    private readonly object _gate = new();
    private readonly JsonlWriter _runs;
    private readonly LinkedList<ScheduleRun> _tail = new();
    private List<Schedule> _all;

    public ScheduleStore(DataStore data, IWebAdminLog log)
    {
        _data = data;
        _all = SavedCommandStore.Load<ScheduleFile>(data, File, log)?.Schedules ?? [];
        _runs = data.OpenJsonl(RunsFile, 1 << 20, 3);
        foreach (var f in _runs.Files().Reverse())
            foreach (var el in JsonlWriter.ReadLines(f))
            {
                try
                {
                    if (el.Deserialize<ScheduleRun>(DataStore.Json) is { } r) Remember(r);
                }
                catch (JsonException)
                {
                }
            }
    }

    public IReadOnlyList<Schedule> All
    {
        get
        {
            lock (_gate) return [.. _all.Select(Clone)];
        }
    }

    public Schedule? Find(string? id)
    {
        lock (_gate) return _all.FirstOrDefault(s => s.Id == id) is { } s ? Clone(s) : null;
    }

    public Schedule Add(Schedule s)
    {
        lock (_gate)
        {
            if (_all.Count >= Max) throw new InvalidOperationException($"at most {Max} schedules");
            s.Id = "sch-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
            _all = [.. _all, Clone(s)];
            Save();
            return Clone(s);
        }
    }

    /// <summary>Changes one schedule and saves at once; null when it is gone.</summary>
    public Schedule? Update(string id, Action<Schedule> change)
    {
        lock (_gate)
        {
            var i = _all.FindIndex(s => s.Id == id);
            if (i < 0) return null;
            var next = Clone(_all[i]);
            change(next);
            next.Id = id;
            _all = [.. _all.Select((s, j) => j == i ? next : s)];
            Save();
            return Clone(next);
        }
    }

    public bool Remove(string id)
    {
        lock (_gate)
        {
            if (!_all.Any(s => s.Id == id)) return false;
            _all = [.. _all.Where(s => s.Id != id)];
            Save();
            return true;
        }
    }

    /// <summary>Every run record (the ops component turns them into panel events for webhooks).</summary>
    public event Action<ScheduleRun>? Recorded;

    public void Record(ScheduleRun run)
    {
        lock (_gate) Remember(run);
        try
        {
            Recorded?.Invoke(run);
        }
        catch (Exception)
        {
            // a subscriber's failure never loses the record
        }
        try
        {
            _runs.Append(run);
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
            // the history is best effort; schedules.json holds lastRun
        }
    }

    public IReadOnlyList<ScheduleRun> Runs(string? scheduleId, int limit)
    {
        lock (_gate) return [.. _tail.Where(r => scheduleId is null || r.Schedule == scheduleId).Take(limit)];
    }

    private void Remember(ScheduleRun r)
    {
        _tail.AddFirst(r);
        while (_tail.Count > Tail) _tail.RemoveLast();
    }

    private void Save() => _data.WriteJson(File, new ScheduleFile { Schedules = _all });

    internal static Schedule Clone(Schedule s) =>
        JsonSerializer.Deserialize<Schedule>(JsonSerializer.SerializeToUtf8Bytes(s, DataStore.Json), DataStore.Json)!;

    public void Dispose() => _runs.Dispose();

    private sealed class ScheduleFile
    {
        public int Format { get; set; } = 1;
        public List<Schedule>? Schedules { get; set; } = [];
    }
}

/// <summary>Checks a schedule's editable fields (the body of POST/PUT) into a <see cref="Schedule"/>; the routes add the
/// actor and the saved-command pin.</summary>
public static partial class ScheduleInput
{
    public const int GraceSeconds = 120;

    public static Schedule? Parse(JsonObject body, TimeProvider time, out List<string> problems)
    {
        problems = [];
        var s = new Schedule();
        var name = OpsHttp.Str(body, "name")?.Trim();
        if (string.IsNullOrEmpty(name) || !OpsHttp.IsOneLine(name, 64)) problems.Add("name: 1 to 64 characters, one line");
        else s.Name = name;
        s.Enabled = OpsHttp.Bool(body, "enabled") ?? true;
        s.Kind = OpsHttp.Str(body, "kind") ?? "";
        if (!Schedule.Kinds.Contains(s.Kind)) problems.Add("kind: restart, backup, announce or command");
        s.TimeZone = OpsHttp.Str(body, "timeZone") ?? "UTC";
        if (ScheduleClock.Zone(s.TimeZone, time) is null)
            problems.Add($"timeZone: '{s.TimeZone}' is not a zone this server knows (use UTC, local, or one of its zone ids)");
        s.SkipWhenEmpty = OpsHttp.Bool(body, "skipWhenEmpty") ?? false;
        s.Missed = OpsHttp.Str(body, "missed") ?? "skip";
        if (s.Missed is not ("skip" or "catchUp")) problems.Add("missed: skip or catchUp");
        if (s.Missed == "catchUp" && s.Kind == "restart") problems.Add("missed: a restart never catches up (it could restart a server just started)");
        s.CatchUpMinutes = (int)Math.Clamp(OpsHttp.Long(body, "catchUpMinutes") ?? 60, int.MinValue, int.MaxValue);
        if (s.CatchUpMinutes is < 1 or > 1440) problems.Add("catchUpMinutes: 1 to 1440");
        var p = body["params"] as JsonObject ?? new JsonObject();
        s.Params = ParseParams(s.Kind, p, problems);
        s.When = ParseWhen(body["when"] as JsonObject, s.Kind, time, problems);
        return problems.Count == 0 ? s : null;
    }

    private static JsonObject ParseParams(string kind, JsonObject p, List<string> problems)
    {
        var o = new JsonObject();
        switch (kind)
        {
            case "restart":
            {
                var cd = OpsHttp.Long(p, "countdownSeconds") ?? 300;
                if (cd is < 0 or > 3600) problems.Add("params.countdownSeconds: 0 to 3600");
                o["countdownSeconds"] = cd;
                var msg = OpsHttp.Str(p, "message");
                if (msg is not null && !OpsHttp.IsOneLine(msg, 200)) problems.Add("params.message: at most 200 characters, one line");
                if (!string.IsNullOrWhiteSpace(msg)) o["message"] = msg.Trim();
                o["allowStopOnly"] = OpsHttp.Bool(p, "allowStopOnly") ?? false;
                break;
            }
            case "backup":
            {
                var n = OpsHttp.Str(p, "name");
                if (n is not null && n.Length > 0 && !BackupName().IsMatch(n)) problems.Add("params.name: 1 to 40 of A-Z a-z 0-9 . _ -");
                if (!string.IsNullOrEmpty(n)) o["name"] = n;
                break;
            }
            case "announce":
            {
                var t = OpsHttp.Str(p, "text")?.Trim();
                if (string.IsNullOrEmpty(t) || !OpsHttp.IsOneLine(t, 200)) problems.Add("params.text: 1 to 200 characters, one line");
                o["text"] = t;
                break;
            }
            case "command":
            {
                var id = OpsHttp.Str(p, "savedCommandId");
                if (string.IsNullOrEmpty(id) || id.Length > 64) problems.Add("params.savedCommandId: a saved command's id");
                o["savedCommandId"] = id;
                break;
            }
        }
        return o;
    }

    private static ScheduleWhen ParseWhen(JsonObject? w, string kind, TimeProvider time, List<string> problems)
    {
        var when = new ScheduleWhen();
        if (w is null)
        {
            problems.Add("when: {type: daily|interval|once, ...}");
            return when;
        }
        when.Type = OpsHttp.Str(w, "type") ?? "";
        switch (when.Type)
        {
            case "daily":
            {
                var times = (w["times"] as JsonArray)?.Select(n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).ToList() ?? [];
                if (times.Count is < 1 or > 24 || times.Any(t => !ScheduleClock.TryParseTime(t, out _))) problems.Add("when.times: 1 to 24 times as HH:mm");
                when.Times = [.. times.OfType<string>().Distinct().Order(StringComparer.Ordinal)];
                if (w["days"] is JsonArray days)
                {
                    var list = days.Select(n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).ToList();
                    if (list.Any(d => d is null || !ScheduleClock.DayNames.Contains(d))) problems.Add("when.days: sun, mon, tue, wed, thu, fri, sat");
                    when.Days = [.. list.OfType<string>().Distinct()];
                    if (when.Days.Count == 0) when.Days = null;
                }
                break;
            }
            case "interval":
            {
                var every = OpsHttp.Long(w, "everyMinutes") ?? 0;
                var min = kind == "restart" ? 60 : 5;
                if (every < min || every > 10080) problems.Add($"when.everyMinutes: {min} to 10080");
                when.EveryMinutes = (int)Math.Clamp(every, 0, 10080);
                var anchor = OpsHttp.Str(w, "anchor");
                if (anchor is null)
                {
                    var now = time.GetUtcNow();
                    when.Anchor = new DateTimeOffset(now.Ticks - now.Ticks % TimeSpan.TicksPerMinute, TimeSpan.Zero) + TimeSpan.FromMinutes(Math.Max(1, every));
                }
                else if (DateTimeOffset.TryParse(anchor, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var a))
                    when.Anchor = a.ToUniversalTime();
                else problems.Add("when.anchor: an ISO 8601 instant");
                break;
            }
            case "once":
            {
                var at = OpsHttp.Str(w, "at");
                if (at is null || !DateTimeOffset.TryParse(at, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var a))
                    problems.Add("when.at: an ISO 8601 instant with its offset");
                else if (a <= time.GetUtcNow()) problems.Add("when.at: in the past");
                else when.At = a.ToUniversalTime();
                break;
            }
            default:
                problems.Add("when.type: daily, interval or once");
                break;
        }
        return when;
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,40}$")]
    private static partial Regex BackupName();
}
