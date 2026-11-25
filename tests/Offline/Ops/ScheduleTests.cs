// SPDX-License-Identifier: MIT
using System.Net;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Offline.Support;
using Kilnwarden.Ops;

namespace Kilnwarden.Offline.Ops;

public class ScheduleTests
{
    private static object Announce(string text = "Hello", string time = "06:00", string missed = "skip", bool skipEmpty = false) => new
    {
        name = "Morning hello", kind = "announce", when = new { type = "daily", times = new[] { time } }, timeZone = "UTC", missed, skipWhenEmpty = skipEmpty,
        @params = new { text },
    };

    private static async Task<string> CreateAsync(OpsHarness.Client c, object body)
    {
        var r = await c.PostAsync("/api/schedules", body);
        Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await OpsHarness.Client.JsonAsync(r))!["id"]!.GetValue<string>();
    }

    private static int Count(FakeKap k, string op) => k.Requests(op).Count();

    [Fact]
    public async Task An_announcement_runs_once_at_its_time_as_its_actor()
    {
        await using var h = await OpsHarness.StartAsync();
        using var admin = await h.UserAsync("alice", BuiltinRoles.Admin);
        var id = await CreateAsync(admin, Announce("Good morning"));
        Assert.Empty(await h.Ops.Scheduler.RunDueAsync(default)); // 05:00: not yet
        h.Time.Set(OpsHarness.Start.AddHours(1).AddSeconds(3));
        var runs = await h.Ops.Scheduler.RunDueAsync(default);
        var run = Assert.Single(runs);
        Assert.Equal("ok", run.Outcome);
        Assert.Equal("alice", run.By);
        var req = Assert.Single(h.Kap.Requests("broadcast"));
        Assert.Equal("Good morning", req["args"]!["text"]!.GetValue<string>());
        // the same moment never runs twice
        h.Time.Advance(TimeSpan.FromSeconds(30));
        Assert.Empty(await h.Ops.Scheduler.RunDueAsync(default));
        Assert.Equal(1, Count(h.Kap, "broadcast"));
        await Eventually.True(() => h.Audit.Read(50).Any(e => e.GetProperty("op").GetString() == "schedules.run"), 3000, "the audit entry");
        var history = await OpsHarness.Client.JsonAsync(await admin.GetAsync($"/api/schedules/{id}/runs"));
        Assert.Equal("ok", history![0]!["outcome"]!.GetValue<string>());
    }

    /// <summary>Runs reach webhooks as panel events: schedule.ran for a run that worked, schedule.failed for one that
    /// failed or was refused (skips are not news).</summary>
    [Fact]
    public async Task Runs_are_panel_events_for_webhooks()
    {
        await using var h = await OpsHarness.StartAsync();
        var seen = new List<Kilnwarden.Events.PanelEvent>();
        Kilnwarden.Events.PanelEvents.Of(h.Host.Services).Published += (e, _) => { lock (seen) seen.Add(e); };
        using var admin = await h.UserAsync("alice", BuiltinRoles.Admin);
        await CreateAsync(admin, Announce("Good morning"));
        h.Time.Set(OpsHarness.Start.AddHours(1).AddSeconds(3));
        await h.Ops.Scheduler.RunDueAsync(default);
        var ran = Assert.Single(seen, e => e.Topic == "schedule.ran");
        Assert.Equal("announce", ran.Payload["kind"]!.GetValue<string>());
        Assert.Equal("alice", ran.Payload["by"]!.GetValue<string>());
        // the actor loses the right: the next run is refused, which is news
        var alice = h.Auth.Accounts.FindByName("alice")!;
        h.Auth.Accounts.Update(alice.Id, a => a with { Roles = [BuiltinRoles.Viewer] });
        h.Time.Advance(TimeSpan.FromDays(1));
        await h.Ops.Scheduler.RunDueAsync(default);
        var failed = Assert.Single(seen, e => e.Topic == "schedule.failed");
        Assert.Equal("refused", failed.Payload["outcome"]!.GetValue<string>());
        Assert.Equal("actor_unauthorized", failed.Payload["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_reload_does_not_run_a_handled_moment_again()
    {
        var h1 = await OpsHarness.StartAsync();
        using (var admin = await h1.UserAsync("alice", BuiltinRoles.Admin))
            await CreateAsync(admin, Announce());
        h1.Time.Set(OpsHarness.Start.AddHours(1).AddSeconds(1));
        Assert.Single(await h1.Ops.Scheduler.RunDueAsync(default));
        h1.StopHost();
        await using var h2 = await OpsHarness.StartAsync(reuse: h1);
        h2.Time.Advance(TimeSpan.FromSeconds(20));
        Assert.Empty(await h2.Ops.Scheduler.RunDueAsync(default));
        Assert.Equal(1, Count(h2.Kap, "broadcast"));
    }

    [Fact]
    public async Task Missed_moments_are_skipped_or_caught_up_once()
    {
        await using var h = await OpsHarness.StartAsync();
        using var admin = await h.UserAsync("alice", BuiltinRoles.Admin);
        await CreateAsync(admin, Announce("skip me", "06:00", "skip"));
        await CreateAsync(admin, new
        {
            name = "Every 5", kind = "announce", when = new { type = "interval", everyMinutes = 5, anchor = OpsHarness.Start.AddMinutes(5).ToString("O") },
            missed = "catchUp", catchUpMinutes = 60, @params = new { text = "catch up" },
        });
        // the panel was off from 05:00 to 06:30: one catch-up run of the newest 5-minute moment, the 06:00 one missed
        h.Time.Set(OpsHarness.Start.AddMinutes(90).AddSeconds(200));
        var runs = await h.Ops.Scheduler.RunDueAsync(default);
        Assert.Equal(1, Count(h.Kap, "broadcast"));
        Assert.Equal("catch up", Assert.Single(h.Kap.Requests("broadcast"))["args"]!["text"]!.GetValue<string>());
        Assert.Contains(runs, r => r.Outcome == "missed" && r.Kind == "announce" && r.Slot == OpsHarness.Start.AddHours(1));
        Assert.Empty(await h.Ops.Scheduler.RunDueAsync(default));
    }

    [Fact]
    public async Task A_run_rechecks_the_actor_as_they_are_now()
    {
        await using var h = await OpsHarness.StartAsync();
        using var admin = await h.UserAsync("alice", BuiltinRoles.Admin);
        await CreateAsync(admin, Announce());
        var acc = h.Auth.Accounts.FindByName("alice")!;
        h.Auth.Accounts.Update(acc.Id, a => a with { Roles = [BuiltinRoles.Viewer], PrivilegeStamp = a.PrivilegeStamp + 1 });
        h.Time.Set(OpsHarness.Start.AddHours(1));
        var run = Assert.Single(await h.Ops.Scheduler.RunDueAsync(default));
        Assert.Equal("refused", run.Outcome);
        Assert.Equal("actor_unauthorized", run.Code);
        Assert.Equal(0, Count(h.Kap, "broadcast"));
        // gone entirely
        h.Auth.Accounts.Update(acc.Id, a => a with { Roles = [BuiltinRoles.Admin], Disabled = true });
        h.Time.Advance(TimeSpan.FromDays(1));
        Assert.Equal("actor_gone", Assert.Single(await h.Ops.Scheduler.RunDueAsync(default)).Code);
        Assert.Equal(0, Count(h.Kap, "broadcast"));
    }

    [Fact]
    public async Task A_restart_needs_recent_proof_the_restart_permission_and_starts_its_countdown_early()
    {
        await using var h = await OpsHarness.StartAsync();
        using var admin = await h.UserAsync("alice", BuiltinRoles.Admin);
        var body = new
        {
            name = "Nightly restart", kind = "restart", when = new { type = "daily", times = new[] { "06:00" } },
            @params = new { countdownSeconds = 600, message = "Back in a minute", allowStopOnly = false },
        };
        var r = await admin.PostAsync("/api/schedules", body);
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Equal("reauth_required", await OpsHarness.Client.CodeAsync(r));
        await admin.ReauthAsync();
        var id = await CreateAsync(admin, body);
        // a moderator may see but not add; a restart never catches up
        using var mod = await h.UserAsync("bob", BuiltinRoles.Moderator);
        Assert.Equal(HttpStatusCode.OK, (await mod.GetAsync("/api/schedules")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mod.PostAsync("/api/schedules", Announce())).StatusCode);
        var bad = await admin.PostAsync("/api/schedules", new { name = "x", kind = "restart", missed = "catchUp", when = new { type = "daily", times = new[] { "01:00" } } });
        Assert.Equal((HttpStatusCode)422, bad.StatusCode);
        // 05:50: the 10-minute countdown of 06:00 begins; the fake Kiln.Admin says restart support is "unknown"
        h.Time.Set(OpsHarness.Start.AddMinutes(50).AddSeconds(2));
        var run = Assert.Single(await h.Ops.Scheduler.RunDueAsync(default));
        Assert.Equal("restart_unsupported", run.Code);
        Assert.Equal(0, Count(h.Kap, "server_restart"));
        // allow a plain stop: the next day it is sent, as alice, 600 s ahead
        h.Time.Set(OpsHarness.Start.AddHours(10));
        var put = await admin.PutAsync($"/api/schedules/{id}", new { body.name, body.kind, body.when, @params = new { countdownSeconds = 600, allowStopOnly = true } });
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode); // the proof is older than ten minutes
        await admin.ReauthAsync();
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync($"/api/schedules/{id}", new { body.name, body.kind, body.when, @params = new { countdownSeconds = 600, allowStopOnly = true } })).StatusCode);
        h.Time.Set(OpsHarness.Start.AddDays(1).AddMinutes(50));
        Assert.Equal("ok", Assert.Single(await h.Ops.Scheduler.RunDueAsync(default)).Outcome);
        var req = Assert.Single(h.Kap.Requests("server_restart"));
        Assert.Equal(600, req["args"]!["seconds"]!.GetValue<long>());
        Assert.Equal("web:alice", req["args"]!["by"]!.GetValue<string>());
    }

    [Fact]
    public async Task Skip_when_empty_and_skip_next()
    {
        await using var h = await OpsHarness.StartAsync(k => k.Answer = req => new JsonObject
        {
            ["t"] = "res", ["ok"] = true, ["op"] = req["op"]!.GetValue<string>(), ["ms"] = 1,
            ["data"] = req["op"]!.GetValue<string>() == "players" ? new JsonArray() : new JsonObject(),
        });
        using var admin = await h.UserAsync("alice", BuiltinRoles.Admin);
        var id = await CreateAsync(admin, Announce("anyone?", skipEmpty: true));
        h.Time.Set(OpsHarness.Start.AddHours(1));
        var run = Assert.Single(await h.Ops.Scheduler.RunDueAsync(default));
        Assert.Equal(("skipped", "empty"), (run.Outcome, run.Code));
        Assert.Equal(0, Count(h.Kap, "broadcast"));
        // skip the next one (tomorrow 06:00)
        var s = await OpsHarness.Client.JsonAsync(await admin.PostAsync($"/api/schedules/{id}/skip-next", new { skip = true }));
        Assert.Equal(OpsHarness.Start.AddDays(1).AddHours(1), DateTimeOffset.Parse(s!["skipSlot"]!.GetValue<string>()));
        h.Time.Set(OpsHarness.Start.AddDays(1).AddHours(1));
        Assert.Equal("skip_next", Assert.Single(await h.Ops.Scheduler.RunDueAsync(default)).Code);
    }

    [Fact]
    public async Task Bad_input_and_a_switched_off_module()
    {
        await using var h = await OpsHarness.StartAsync();
        using var admin = await h.UserAsync("alice", BuiltinRoles.Admin);
        var zone = await admin.PostAsync("/api/schedules", new { name = "x", kind = "announce", timeZone = "Mars/Olympus_Mons", when = new { type = "daily", times = new[] { "06:00" } }, @params = new { text = "hi" } });
        Assert.Equal((HttpStatusCode)422, zone.StatusCode);
        Assert.Contains("timeZone", await zone.Content.ReadAsStringAsync());
        Assert.Equal((HttpStatusCode)422, (await admin.PostAsync("/api/schedules", new { name = "x", kind = "announce", when = new { type = "daily", times = new[] { "25:00" } }, @params = new { text = "hi" } })).StatusCode);
        Assert.Equal((HttpStatusCode)422, (await admin.PostAsync("/api/schedules", new { name = "x", kind = "announce", when = new { type = "daily", times = new[] { "06:00" } }, @params = new { text = "two\nlines" } })).StatusCode);
        await CreateAsync(admin, Announce());
        var reg = h.Auth.Registry;
        reg.SetEnabled(ScheduleRoutes.ModuleId, false);
        h.Time.Set(OpsHarness.Start.AddHours(1));
        Assert.Empty(await h.Ops.Scheduler.RunDueAsync(default));
        Assert.Equal(0, Count(h.Kap, "broadcast"));
    }

    [Fact]
    public async Task A_command_schedule_runs_its_pinned_line_and_stops_when_the_saved_command_changes()
    {
        await using var h = await OpsHarness.StartAsync();
        using var owner = await h.UserAsync("olivia", BuiltinRoles.Owner);
        var sc = await OpsHarness.Client.JsonAsync(await owner.PostAsync("/api/saved-commands", new { name = "Clear weather", line = "/weather clear", shared = true }));
        var scId = sc!["id"]!.GetValue<string>();
        var body = new { name = "Weather", kind = "command", when = new { type = "daily", times = new[] { "06:00" } }, @params = new { savedCommandId = scId } };
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsync("/api/schedules", body)).StatusCode);
        await owner.ReauthAsync();
        var id = await CreateAsync(owner, body);
        h.Time.Set(OpsHarness.Start.AddHours(1));
        Assert.Equal("ok", Assert.Single(await h.Ops.Scheduler.RunDueAsync(default)).Outcome);
        var req = Assert.Single(h.Kap.Requests("command_run"));
        Assert.Equal("/weather clear", req["args"]!["line"]!.GetValue<string>());
        Assert.Equal("web:olivia", req["args"]!["source"]!.GetValue<string>());
        Assert.Equal("console", req["args"]!["level"]!.GetValue<string>());
        // the saved command changes: the schedule refuses until it is saved again
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsync($"/api/saved-commands/{scId}", new { name = "Clear weather", line = "/weather rain", shared = true })).StatusCode);
        h.Time.Advance(TimeSpan.FromDays(1));
        Assert.Equal("saved_command_changed", Assert.Single(await h.Ops.Scheduler.RunDueAsync(default)).Code);
        Assert.Single(h.Kap.Requests("command_run"));
        // a day later the idle session is gone: sign in again
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync("/api/auth/login", new { name = "olivia", password = OpsHarness.Password })).StatusCode);
        var list = await OpsHarness.Client.JsonAsync(await owner.GetAsync("/api/schedules"));
        var s = list!["schedules"]!.AsArray().Single(x => x!["id"]!.GetValue<string>() == id)!;
        Assert.True(s["savedCommand"]!["changed"]!.GetValue<bool>());
        Assert.Null(s["params"]!["line"]);
    }
}
