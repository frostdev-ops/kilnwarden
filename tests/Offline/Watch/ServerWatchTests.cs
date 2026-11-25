// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;
using Kilnwarden.Events;
using Kilnwarden.Metrics;
using Kilnwarden.Watch;

namespace Kilnwarden.Offline.Watch;

public sealed class ServerWatchTests
{
    private static (ServerWatch W, List<PanelEvent> Seen) Make()
    {
        var events = new PanelEvents();
        var seen = new List<PanelEvent>();
        events.Published += (e, _) => seen.Add(e);
        var w = new ServerWatch(events);
        w.Declare();
        return (w, seen);
    }

    private static JsonNode Info(string? state, string requested = "2026-11-25T12:00:00Z", int seconds = 300, string? error = null) =>
        new JsonObject
        {
            ["restart"] = new JsonObject
            {
                ["supported"] = "supported",
                ["request"] = state is null ? null : new JsonObject
                {
                    ["state"] = state, ["by"] = "web:olive", ["requested"] = requested, ["secondsLeft"] = seconds, ["message"] = "patch", ["error"] = error,
                },
            },
        };

    [Fact]
    public void A_restart_countdown_is_announced_once_and_its_end_too()
    {
        var (w, seen) = Make();
        w.OnAdminInfo(Info(null));
        w.OnAdminInfo(Info("counting"));
        w.OnAdminInfo(Info("counting", seconds: 120));
        var e = Assert.Single(seen);
        Assert.Equal("server.restart", e.Topic);
        Assert.Equal("web:olive", e.Payload["by"]!.GetValue<string>());
        Assert.Equal(5, e.Payload["minutes"]!.GetValue<int>());
        w.OnAdminInfo(Info("canceled"));
        Assert.Equal("server.restart_canceled", seen[^1].Topic);
        w.OnAdminInfo(Info("failed", requested: "2026-11-25T13:00:00Z", error: "the save failed"));
        Assert.Equal("server.restart_failed", seen[^1].Topic);
        Assert.Equal("the save failed", seen[^1].Payload["error"]!.GetValue<string>());
        Assert.Equal(3, seen.Count);
    }

    [Fact]
    public void A_countdown_already_running_when_the_panel_starts_is_not_news()
    {
        var (w, seen) = Make();
        w.OnAdminInfo(Info("counting"));
        Assert.Empty(seen);
        w.OnAdminInfo(Info("saving"));
        Assert.Empty(seen);
    }

    [Fact]
    public void Mod_faults_add_up_and_a_growing_count_is_an_event()
    {
        var (w, seen) = Make();
        JsonNode Mods(int a, int b) => JsonNode.Parse($$"""
            [{"id":"a","name":"A","state":"Loaded","faults":{{a}},"lastFault":"NullReferenceException at C:\\srv"},
             {"id":"b","name":"B","state":"Loaded","faults":{{b}}}]
            """)!;
        w.OnMods(Mods(1, 0));
        Assert.Equal(1, w.ModFaults);
        Assert.Empty(seen);
        w.OnMods(Mods(3, 0));
        var e = Assert.Single(seen);
        Assert.Equal("mod.fault", e.Topic);
        Assert.Equal("a", e.Payload["mod"]!.GetValue<string>());
        Assert.Equal(3L, e.Payload["faults"]!.GetValue<long>());
        Assert.Equal(3, w.ModFaults);
        w.OnMods(Mods(3, 0));
        Assert.Single(seen);
    }

    [Fact]
    public void A_new_crash_report_is_an_event_old_ones_are_not()
    {
        var (w, seen) = Make();
        w.OnCrashReports(JsonNode.Parse("""[{"name":"crash-1.txt","bytes":10,"time":"2026-11-25T10:00:00Z"}]"""));
        Assert.Empty(seen);
        w.OnCrashReports(JsonNode.Parse("""[{"name":"crash-2.txt","bytes":10,"time":"2026-11-25T11:00:00Z"},{"name":"crash-1.txt","bytes":10}]"""));
        Assert.Equal("crash-2.txt", Assert.Single(seen).Payload["name"]!.GetValue<string>());
    }

    [Fact]
    public void The_save_age_and_failed_saves()
    {
        var (w, seen) = Make();
        var now = DateTimeOffset.UtcNow;
        w.OnSaveStatus(JsonNode.Parse($$"""{"state":"idle","lastSaved":"{{now.AddMinutes(-4):O}}","failed":0}"""));
        Assert.InRange(w.SaveAgeSeconds(now)!.Value, 239, 241);
        w.OnSaveStatus(JsonNode.Parse($$"""{"state":"idle","lastSaved":"{{now.AddMinutes(-4):O}}","failed":2,"lastReason":"Autosave"}"""));
        Assert.Equal("save.failed", Assert.Single(seen).Topic);
    }

    [Fact]
    public void Metrics_keep_old_history_files_and_report_the_new_fields()
    {
        // a history saved before modFaults and saveAgeS existed (11 fields) still loads
        var h = new MetricsHistory();
        Assert.Contains(MetricsHistory.Fields, f => f.Key == "modFaults");
        Assert.Contains(MetricsHistory.Fields, f => f.Key == "saveAgeS");
        var old = OldHistory(fields: 11, points: 3);
        Assert.True(h.Load(old));
        Assert.False(MetricsHistory.Unavailable.ContainsKey("modFaults"));
    }

    private static byte[] OldHistory(int fields, int points)
    {
        var rec = 8 + 4 * fields;
        var buf = new byte[12 + 4 + points * rec + 4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(buf, 0x4D41574B);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4), 1);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(8), fields);
        var o = 12;
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(o), points); o += 4;
        for (var i = 0; i < points; i++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(o), 1_764_000_000_000 + i * 10_000L); o += 8;
            for (var f = 0; f < fields; f++) { System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(buf.AsSpan(o), f); o += 4; }
        }
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(o), 0);
        return buf;
    }
}
