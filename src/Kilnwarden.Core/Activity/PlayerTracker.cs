// SPDX-License-Identifier: MIT
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Kilnwarden.Activity;

/// <summary>One line of the activity log: a player joined, left or died.</summary>
/// <param name="Type">"join", "leave" or "death".</param>
/// <param name="Seconds">leave: how long the session lasted.</param>
/// <param name="Detail">leave: "server stopped" when the panel found the player gone after a restart; death: the
/// killer's name ("" when none), when Kiln.Admin knows it.</param>
public sealed record ActivityEntry(string Type, DateTimeOffset At, string Name, string? SteamId, double? Seconds = null, string? Detail = null);

/// <summary>Someone in the world now.</summary>
public sealed record OnlinePlayer(string Name, string? SteamId, DateTimeOffset Since);

/// <summary>What one state push changed: the entries to log, and whether each is news (a webhook) or a correction
/// (players found gone after the panel itself was off: logged at the time the panel last saw them, never announced).</summary>
public sealed record TrackerChange(ActivityEntry Entry, bool Announce);

/// <summary>Who is on, from Kiln.Admin's <c>state</c> pushes (players, combatDeaths, deaths): joins, leaves and deaths.
/// Pure logic (no I/O, no clock of its own) so it is tested on its own; the component feeds it and keeps the log.
/// A player is keyed by SteamID when the state has one, else by name.</summary>
public sealed class PlayerTracker
{
    /// <summary>At most this many players are tracked (a hostile or broken state cannot grow it without bound).</summary>
    public const int MaxPlayers = 512;

    private readonly object _gate = new();
    private Dictionary<string, OnlinePlayer> _online = new(StringComparer.Ordinal);
    private Dictionary<string, OnlinePlayer>? _restored;
    private DateTimeOffset _restoredSeen;
    private bool _baseline;
    private long _lastDeathTime = long.MinValue;
    private double _lastDeathClock = double.MinValue;
    private HashSet<string> _deathKeys = new(StringComparer.Ordinal);

    /// <summary>The players online now.</summary>
    public IReadOnlyList<OnlinePlayer> Online
    {
        get
        {
            lock (_gate) return [.. _online.Values.OrderBy(p => p.Since).ThenBy(p => p.Name, StringComparer.Ordinal)];
        }
    }

    /// <summary>Whether the tracker has seen a state since it started (before that, Online is what it restored).</summary>
    public bool Live
    {
        get
        {
            lock (_gate) return _baseline;
        }
    }

    /// <summary>Sessions the panel had when it last stopped (online.json), and when it last saw them. The first state
    /// keeps the ones still on (no join) and closes the others at <paramref name="lastSeen"/> (logged, not announced).</summary>
    public void Restore(IEnumerable<OnlinePlayer> players, DateTimeOffset lastSeen)
    {
        lock (_gate)
        {
            _restored = new Dictionary<string, OnlinePlayer>(StringComparer.Ordinal);
            foreach (var p in players.Take(MaxPlayers)) _restored[Key(p.Name, p.SteamId)] = p;
            _restoredSeen = lastSeen;
            _online = new Dictionary<string, OnlinePlayer>(_restored, StringComparer.Ordinal);
        }
    }

    /// <summary>One <c>state</c> push's data. Returns the changes in order: leaves, joins, deaths.</summary>
    public IReadOnlyList<TrackerChange> Observe(JsonNode? state, DateTimeOffset now)
    {
        // a push that System.Text.Json cannot read in full (invalid UTF-8 in a string, a duplicate property: both throw
        // only when touched) is skipped as a whole, before anything here changes (found by the player-state fuzz target)
        try
        {
            Touch(state, 0);
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or FormatException)
        {
            return [];
        }
        if (state is not JsonObject s || s["players"] is not JsonArray arr) return [];
        var seen = new Dictionary<string, (string Name, string? SteamId, uint? Entity)>(StringComparer.Ordinal);
        foreach (var p in arr)
        {
            if (seen.Count >= MaxPlayers) break;
            if (p is not JsonObject o || Str(o, "name") is not { Length: > 0 and <= 64 } name) continue;
            var steam = Str(o, "steamId") is { } id && IsSteamId(id) ? id : null;
            uint? entity = o["entity"] is JsonValue ev && ev.TryGetValue<uint>(out var en) ? en : null;
            seen[Key(name, steam)] = (Clean(name), steam, entity);
        }
        var changes = new List<TrackerChange>();
        lock (_gate)
        {
            if (_restored is { } restored)
            {
                foreach (var (k, p) in restored)
                    if (!seen.ContainsKey(k))
                    {
                        _online.Remove(k);
                        changes.Add(new(new ActivityEntry("leave", _restoredSeen, p.Name, p.SteamId, Seconds(p.Since, _restoredSeen), "server stopped"), false));
                    }
                _restored = null;
            }
            foreach (var (k, p) in _online.ToList())
                if (!seen.ContainsKey(k))
                {
                    _online.Remove(k);
                    changes.Add(new(new ActivityEntry("leave", now, p.Name, p.SteamId, Seconds(p.Since, now)), true));
                }
            foreach (var (k, p) in seen)
            {
                if (_online.TryGetValue(k, out var had))
                {
                    if (had.Name != p.Name) _online[k] = had with { Name = p.Name };
                    continue;
                }
                _online[k] = new OnlinePlayer(p.Name, p.SteamId, now);
                // the very first state of a fresh panel (nothing restored) only says who is on: no joins announced
                changes.Add(new(new ActivityEntry("join", now, p.Name, p.SteamId), _baseline));
            }
            var names = seen.Values.Where(v => v.Entity is not null).GroupBy(v => v.Entity!.Value).ToDictionary(g => g.Key, g => g.First());
            changes.AddRange(Deaths(s, names, now));
            _baseline = true;
        }
        return changes;
    }

    // Called under _gate. combatDeaths (Kiln.Admin with Kiln.Combat: victim and killer names, a Unix time) is preferred;
    // deaths (health reached 0: entity and a process clock) is the fallback. The first state only sets what was seen.
    private List<TrackerChange> Deaths(JsonObject s, Dictionary<uint, (string Name, string? SteamId, uint? Entity)> players, DateTimeOffset now)
    {
        var list = new List<TrackerChange>();
        if (s["combatDeaths"] is JsonArray combat)
        {
            var newest = _lastDeathTime;
            var ring = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in combat.OfType<JsonObject>().Take(256))
            {
                if (d["time"] is not JsonValue tv || !tv.TryGetValue<long>(out var t)) continue;
                var entity = d["entity"] is JsonValue ev && ev.TryGetValue<uint>(out var e) ? e : 0u;
                var key = $"c:{t}:{entity}";
                ring.Add(key);
                if (t < _lastDeathTime || _deathKeys.Contains(key)) continue;
                newest = Math.Max(newest, t);
                if (!_baseline) continue;
                var name = Str(d, "victimName") is { Length: > 0 and <= 64 } v ? Clean(v) : players.TryGetValue(entity, out var p) ? p.Name : null;
                if (name is null) continue;
                var steam = players.TryGetValue(entity, out var pp) ? pp.SteamId : null;
                var killer = Str(d, "killerName") is { Length: > 0 } k ? Clean(k.Length > 64 ? k[..64] : k) : "";
                var at = t > 0 && t < 4_102_444_800 ? DateTimeOffset.FromUnixTimeSeconds(t) : now;
                list.Add(new(new ActivityEntry("death", at, name, steam, Detail: killer), true));
            }
            _lastDeathTime = newest;
            _deathKeys = ring;
        }
        else if (s["deaths"] is JsonArray plain)
        {
            var newest = _lastDeathClock;
            foreach (var d in plain.OfType<JsonObject>().Take(256))
            {
                if (d["clock"] is not JsonValue cv || !cv.TryGetValue<double>(out var clock) || !double.IsFinite(clock)) continue;
                var entity = d["entity"] is JsonValue ev && ev.TryGetValue<uint>(out var e) ? e : 0u;
                if (clock <= _lastDeathClock) continue;
                newest = Math.Max(newest, clock);
                if (!_baseline || !players.TryGetValue(entity, out var p)) continue;
                list.Add(new(new ActivityEntry("death", now, p.Name, p.SteamId, Detail: ""), true));
            }
            _lastDeathClock = newest;
        }
        return list;
    }

    // Reads every property and string once: a lazily parsed node throws for duplicates and bad UTF-8 only when touched.
    private static void Touch(JsonNode? n, int depth)
    {
        if (depth > 64) throw new FormatException("too deep");
        switch (n)
        {
            case JsonObject o:
                foreach (var kv in o) Touch(kv.Value, depth + 1);
                break;
            case JsonArray a:
                foreach (var item in a) Touch(item, depth + 1);
                break;
            case JsonValue v when v.GetValueKind() == JsonValueKind.String:
                _ = v.GetValue<string>();
                break;
        }
    }

    private static double Seconds(DateTimeOffset since, DateTimeOffset until) => Math.Max(0, Math.Round((until - since).TotalSeconds));

    private static string Key(string name, string? steamId) => steamId is not null ? "s:" + steamId : "n:" + name;

    private static string? Str(JsonObject o, string name) =>
        o[name] is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null;

    /// <summary>A SteamID64: 17 digits starting 7656119.</summary>
    public static bool IsSteamId(string? s) => s is { Length: 17 } && s.StartsWith("7656119", StringComparison.Ordinal) && s.All(char.IsAsciiDigit);

    /// <summary>A name without control characters (a log line, a webhook, a page).</summary>
    private static string Clean(string s) => string.Concat(s.Where(c => !char.IsControl(c) && c is not ('\u2028' or '\u2029')));
}
