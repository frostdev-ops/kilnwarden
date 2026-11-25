// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Kilnwarden.Metrics;

/// <summary>metrics.json: history on or off, the alert rules and the hourly alert budget.</summary>
public sealed class MetricsSettings
{
    public int Format { get; set; } = 1;

    /// <summary>Keep the 24 h and 30 d history (and its file). Live values work either way.</summary>
    public bool History { get; set; } = true;

    /// <summary>At most this many alerts fire per hour, all rules together.</summary>
    public int MaxAlertsPerHour { get; set; } = 6;

    public List<AlertRule> Alerts { get; set; } = [];
}

/// <summary>"tickP95Ms > 40 for 60 s": fires once the condition held for &lt;see cref="ForS"/&gt;, then not again before
/// &lt;see cref="CooldownS"/&gt;.</summary>
public sealed class AlertRule
{
    public string Id { get; set; } = "";
    public string Metric { get; set; } = "";
    public string Op { get; set; } = ">";
    public double Threshold { get; set; }
    public int ForS { get; set; } = 60;
    public int CooldownS { get; set; } = 900;
    public bool Enabled { get; set; } = true;
}

public static partial class MetricsRules
{
    public const int MaxRules = 32;

    public static IReadOnlyList<string> Validate(MetricsSettings s)
    {
        var p = new List<string>();
        if (s.MaxAlertsPerHour is < 1 or > 60) p.Add("maxAlertsPerHour: 1 to 60");
        if (s.Alerts is null) { p.Add("alerts: a list"); return p; }
        if (s.Alerts.Count > MaxRules) p.Add($"alerts: at most {MaxRules}");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in s.Alerts)
        {
            if (r is null) { p.Add("alerts: a rule is null"); continue; }
            var at = $"alerts[{r.Id}]";
            if (!IdRe().IsMatch(r.Id ?? "") || !ids.Add(r.Id!)) p.Add($"{at}: id must be unique, [a-z0-9-], 1 to 32");
            if (MetricsHistory.IndexOf(r.Metric ?? "") < 0) p.Add($"{at}: unknown metric '{r.Metric}'");
            if (r.Op is not (">" or "<")) p.Add($"{at}: op is > or <");
            if (!double.IsFinite(r.Threshold)) p.Add($"{at}: threshold must be a number");
            if (r.ForS is < 0 or > 3600) p.Add($"{at}: forS 0 to 3600");
            if (r.CooldownS is < 300 or > 86400) p.Add($"{at}: cooldownS 300 to 86400");
        }
        return p;
    }

    [GeneratedRegex("^[a-z0-9-]{1,32}$")]
    private static partial Regex IdRe();
}

/// <summary>Evaluates the rules on every sample (one thread: the metrics feed's). Returns the alerts that fire.</summary>
public sealed class AlertEvaluator
{
    private readonly Dictionary<string, (long? Since, long? Fired)> _state = new(StringComparer.Ordinal);
    private readonly Queue<long> _firedThisHour = new();

    public IReadOnlyList<JsonObject> Evaluate(MetricsSettings settings, long atMs, float[] values)
    {
        var fired = new List<JsonObject>();
        while (_firedThisHour.Count > 0 && atMs - _firedThisHour.Peek() >= 3_600_000) _firedThisHour.Dequeue();
        foreach (var r in settings.Alerts)
        {
            if (!r.Enabled) { _state.Remove(r.Id); continue; }
            var i = MetricsHistory.IndexOf(r.Metric);
            if (i < 0) continue;
            var (since, last) = _state.GetValueOrDefault(r.Id);
            var v = values[i];
            var holds = !float.IsNaN(v) && (r.Op == ">" ? v > r.Threshold : v < r.Threshold);
            if (!holds)
            {
                _state[r.Id] = (null, last);
                continue;
            }
            since ??= atMs;
            if (atMs - since >= r.ForS * 1000L && (last is null || atMs - last >= r.CooldownS * 1000L) && _firedThisHour.Count < settings.MaxAlertsPerHour)
            {
                last = atMs;
                _firedThisHour.Enqueue(atMs);
                fired.Add(new JsonObject
                {
                    ["rule"] = r.Id, ["metric"] = r.Metric, ["op"] = r.Op, ["threshold"] = r.Threshold, ["value"] = Math.Round(v, 3), ["forS"] = r.ForS,
                });
            }
            _state[r.Id] = (since, last);
        }
        foreach (var gone in _state.Keys.Where(k => settings.Alerts.All(r => r.Id != k)).ToList()) _state.Remove(gone);
        return fired;
    }
}
