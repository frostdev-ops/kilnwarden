// SPDX-License-Identifier: MIT
using System.Net;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Events;
using Kilnwarden.Metrics;

namespace Kilnwarden.Offline.LaneC;

[Collection("lane-c")]
public sealed class MetricsTests
{
    private const long T0 = 1_763_913_600_000; // a multiple of 5 min

    private static float[] V(float players, float tickP95 = 1, float? cpu = 10)
    {
        var v = Enumerable.Repeat(float.NaN, MetricsHistory.FieldCount).ToArray();
        v[MetricsHistory.IndexOf("players")] = players;
        v[MetricsHistory.IndexOf("tickP95Ms")] = tickP95;
        v[MetricsHistory.IndexOf("frameMaxMs")] = players * 2;
        if (cpu is { } c) v[MetricsHistory.IndexOf("cpuPercent")] = c;
        return v;
    }

    [Fact]
    public void Parses_the_30_9_metrics_push()
    {
        var data = JsonNode.Parse("""
            {"at": 1763913600000, "windowS": 2.003, "steps": 120, "stepsPerS": 59.91,
             "frameMs": {"avg": 16.7, "p95": 18.1, "max": 20.3}, "tickMs": null,
             "memory": {"workingSet": 1060000000, "managedHeap": 21000000}, "cpu": {"percent": null, "cores": 16},
             "gcPauseMs": 4.0, "players": 3}
            """);
        var (at, v) = MetricsHistory.Parse(data)!.Value;
        Assert.Equal(1763913600000, at);
        Assert.Equal(3, v[MetricsHistory.IndexOf("players")]);
        Assert.Equal(18.1f, v[MetricsHistory.IndexOf("frameP95Ms")]);
        Assert.True(float.IsNaN(v[MetricsHistory.IndexOf("tickAvgMs")]));   // null: not reported
        Assert.True(float.IsNaN(v[MetricsHistory.IndexOf("cpuPercent")]));
        Assert.Null(MetricsHistory.Parse(JsonNode.Parse("""{"players": 3}""")));
        Assert.Null(MetricsHistory.Parse(JsonNode.Parse("[]")));
    }

    [Fact]
    public void Rings_are_bounded_and_averaged()
    {
        var h = new MetricsHistory();
        // 2 days of samples every 2 s would be 86400 live points; the live ring keeps 300, the 10 s tier 8640
        for (long i = 0; i < 100_000; i++) h.Add(T0 + i * 2000, V(i % 10, cpu: null));
        var live = h.Series("10m", T0 + 100_000L * 2000)!;
        Assert.Equal(300, live["t"]!.AsArray().Count);
        var day = h.Series("24h", T0 + 100_000L * 2000)!;
        Assert.True(day["t"]!.AsArray().Count <= MetricsHistory.MaxPoints);
        Assert.True(day["stepS"]!.GetValue<double>() >= 10);
        // averages of players 0..9 = 4.5; the max field keeps the max (9*2)
        var hour = h.Series("1h", T0 + 100_000L * 2000)!;
        Assert.Equal(360, hour["t"]!.AsArray().Count);
        Assert.All(hour["series"]!["frameMaxMs"]!.AsArray(), x => Assert.True(x!.GetValue<double>() >= 8));
        Assert.All(hour["series"]!["cpuPercent"]!.AsArray(), Assert.Null);
        Assert.False(h.Seen(MetricsHistory.IndexOf("cpuPercent")));
        Assert.Null(h.Series("1y", T0));
        // persisted: the averaged tiers only, a fixed size, refused when foreign
        var bytes = h.Save();
        Assert.True(bytes.Length <= 12 + 2 * (4 + MetricsHistory.TierCapacity * (8 + 4 * MetricsHistory.FieldCount)));
        var back = new MetricsHistory();
        Assert.True(back.Load(bytes));
        Assert.Equal(hour["t"]!.ToJsonString(), back.Series("1h", T0 + 100_000L * 2000)!["t"]!.ToJsonString());
        Assert.False(new MetricsHistory().Load("not a history"u8));
        Assert.False(new MetricsHistory().Load(bytes.AsSpan(0, 40)));
    }

    [Fact]
    public void Alerts_hold_cool_down_and_keep_the_hourly_budget()
    {
        var s = new MetricsSettings
        {
            MaxAlertsPerHour = 2,
            Alerts = [new AlertRule { Id = "lag", Metric = "tickP95Ms", Op = ">", Threshold = 40, ForS = 10, CooldownS = 300 }],
        };
        Assert.Empty(MetricsRules.Validate(s));
        var e = new AlertEvaluator();
        var fired = new List<long>();
        for (long t = 0; t < 3_600_000; t += 2000)
            if (e.Evaluate(s, T0 + t, V(1, tickP95: 50)).Count > 0) fired.Add(t);
        Assert.Equal([10_000L, 310_000L], fired); // held 10 s, then once per 5 min, then the hourly budget of 2
        // a dip resets the hold
        var e2 = new AlertEvaluator();
        Assert.Empty(e2.Evaluate(s, T0, V(1, 50)));
        Assert.Empty(e2.Evaluate(s, T0 + 8000, V(1, 5)));
        Assert.Empty(e2.Evaluate(s, T0 + 16000, V(1, 50)));
        Assert.Single(e2.Evaluate(s, T0 + 26000, V(1, 50)));
    }

    [Theory]
    [InlineData("""{"id":"x","metric":"nope","op":">","threshold":1,"forS":0,"cooldownS":300}""")]
    [InlineData("""{"id":"x","metric":"players","op":"=","threshold":1,"forS":0,"cooldownS":300}""")]
    [InlineData("""{"id":"x","metric":"players","op":">","threshold":1,"forS":0,"cooldownS":10}""")]
    [InlineData("""{"id":"Bad id","metric":"players","op":">","threshold":1,"forS":0,"cooldownS":300}""")]
    public void Bad_rules_are_refused(string rule)
    {
        var r = System.Text.Json.JsonSerializer.Deserialize<AlertRule>(rule, Kilnwarden.Storage.DataStore.Json)!;
        Assert.NotEmpty(MetricsRules.Validate(new MetricsSettings { Alerts = [r] }));
    }

    [Fact]
    public async Task Routes_and_alert_events()
    {
        await using var c = await LaneC.StartAsync();
        var events = c.H.Host.Services.Get<PanelEvents>();
        var alerts = new List<PanelEvent>();
        events.Published += (e, _) => { if (e.Topic == MetricsComponent.AlertTopic) lock (alerts) alerts.Add(e); };
        using var viewer = await c.UserAsync("vic", BuiltinRoles.Viewer);
        using var mod = await c.UserAsync("mona", BuiltinRoles.Moderator);
        using var admin = await c.UserAsync("adam", BuiltinRoles.Admin);

        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/metrics/live")).StatusCode);
        var fields = await LaneC.JsonAsync(await mod.GetAsync("/api/metrics/fields"));
        Assert.Contains(fields!["fields"]!.AsArray(), f => f!["key"]!.GetValue<string>() == "saveMs" && !f["available"]!.GetValue<bool>());

        var rule = new { id = "busy", metric = "players", op = ">", threshold = 5, forS = 0, cooldownS = 300, enabled = true };
        var put = await LaneC.PutAsync(admin, "/api/metrics/alerts", new { alerts = new[] { rule } });
        Assert.Equal("reauth_required", await LaneC.CodeAsync(put));
        await LaneC.ReauthAsync(admin);
        Assert.Equal(HttpStatusCode.UnprocessableContent, (await LaneC.PutAsync(admin, "/api/metrics/alerts", new { alerts = new[] { rule with { op = "~" } } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LaneC.PutAsync(admin, "/api/metrics/alerts", new { alerts = new[] { rule } })).StatusCode);

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        c.Metrics.Add(now - 4000, V(2));
        c.Metrics.Add(now - 2000, V(9));
        var live = await LaneC.JsonAsync(await mod.GetAsync("/api/metrics/live"));
        Assert.Equal(9, live!["values"]!["players"]!.GetValue<double>());
        var hist = await LaneC.JsonAsync(await mod.GetAsync("/api/metrics/history?range=10m"));
        Assert.Equal(2, hist!["t"]!.AsArray().Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await mod.GetAsync("/api/metrics/history?range=forever")).StatusCode);
        lock (alerts) Assert.Single(alerts);
        Assert.Equal("busy", alerts[0].Payload["rule"]!.GetValue<string>());
    }
}
