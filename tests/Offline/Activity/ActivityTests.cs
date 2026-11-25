// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Kilnwarden.Activity;
using Kilnwarden.Auth;
using Lc = Kilnwarden.Offline.LaneC.LaneC;
using Kilnwarden.Offline.Support;

namespace Kilnwarden.Offline.Activity;

public sealed class PlayerTrackerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    private static JsonNode State(params (string Name, string? Steam, uint Entity)[] players) => new JsonObject
    {
        ["players"] = new JsonArray([.. players.Select(p => (JsonNode)new JsonObject { ["name"] = p.Name, ["steamId"] = p.Steam, ["entity"] = p.Entity })]),
    };

    [Fact]
    public void The_first_state_only_says_who_is_on_then_joins_and_leaves_are_announced()
    {
        var t = new PlayerTracker();
        var first = t.Observe(State(("Ann", "76561198000000001", 1)), T0);
        Assert.Single(first);
        Assert.False(first[0].Announce); // logged as a session start, not announced
        Assert.Equal("join", first[0].Entry.Type);

        var second = t.Observe(State(("Ann", "76561198000000001", 1), ("Bob", null, 2)), T0.AddMinutes(1));
        Assert.Equal(("join", "Bob", true), (second.Single().Entry.Type, second[0].Entry.Name, second[0].Announce));

        var third = t.Observe(State(("Bob", null, 2)), T0.AddMinutes(31));
        var leave = third.Single();
        Assert.Equal(("leave", "Ann", true), (leave.Entry.Type, leave.Entry.Name, leave.Announce));
        Assert.Equal(31 * 60, leave.Entry.Seconds);
        Assert.Equal("76561198000000001", leave.Entry.SteamId);
        Assert.Equal(["Bob"], t.Online.Select(p => p.Name));
    }

    [Fact]
    public void A_renamed_player_with_the_same_SteamID_is_the_same_session()
    {
        var t = new PlayerTracker();
        t.Observe(State(("Ann", "76561198000000001", 1)), T0);
        Assert.Empty(t.Observe(State(("Annie", "76561198000000001", 1)), T0.AddMinutes(1)));
        Assert.Equal("Annie", t.Online.Single().Name);
    }

    [Fact]
    public void Restored_sessions_continue_and_the_ones_gone_close_when_the_panel_last_saw_them_without_an_announcement()
    {
        var t = new PlayerTracker();
        t.Restore([new OnlinePlayer("Ann", "76561198000000001", T0), new OnlinePlayer("Bob", null, T0.AddMinutes(5))], T0.AddMinutes(10));
        var c = t.Observe(State(("Ann", "76561198000000001", 1), ("Cy", null, 3)), T0.AddHours(2));
        var bob = c.Single(x => x.Entry.Name == "Bob");
        Assert.Equal(("leave", false, "server stopped"), (bob.Entry.Type, bob.Announce, bob.Entry.Detail));
        Assert.Equal(T0.AddMinutes(10), bob.Entry.At);
        Assert.Equal(300, bob.Entry.Seconds);
        Assert.DoesNotContain(c, x => x.Entry.Name == "Ann"); // still on: no second join
        Assert.Equal(T0, t.Online.Single(p => p.Name == "Ann").Since);
        Assert.Contains(c, x => x.Entry is { Type: "join", Name: "Cy" } && !x.Announce);
    }

    [Fact]
    public void Deaths_come_from_combatDeaths_once_each_and_only_after_the_first_state()
    {
        var t = new PlayerTracker();
        JsonNode With(params (long Time, uint Entity, string? Killer)[] deaths)
        {
            var s = (JsonObject)State(("Ann", "76561198000000001", 7));
            s["combatDeaths"] = new JsonArray([.. deaths.Select(d => (JsonNode)new JsonObject
            {
                ["entity"] = d.Entity, ["time"] = d.Time, ["victimName"] = "Ann", ["killerName"] = d.Killer, ["killerKind"] = d.Killer is null ? "none" : "creature",
            })]);
            return s;
        }
        Assert.DoesNotContain(t.Observe(With((100, 7, "Wolf")), T0), x => x.Entry.Type == "death"); // already there before we looked
        var c = t.Observe(With((100, 7, "Wolf"), (200, 7, null)), T0.AddMinutes(1));
        var d = c.Single(x => x.Entry.Type == "death");
        Assert.Equal(("Ann", "", true), (d.Entry.Name, d.Entry.Detail, d.Announce));
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(200), d.Entry.At);
        Assert.Empty(t.Observe(With((100, 7, "Wolf"), (200, 7, null)), T0.AddMinutes(2)));
        Assert.Equal("Wolf", t.Observe(With((200, 7, null), (300, 7, "Wolf")), T0.AddMinutes(3)).Single().Entry.Detail);
    }

    [Fact]
    public void Older_deaths_fall_back_to_the_health_ring()
    {
        var t = new PlayerTracker();
        JsonNode With(params double[] clocks)
        {
            var s = (JsonObject)State(("Ann", null, 7));
            s["deaths"] = new JsonArray([.. clocks.Select(c => (JsonNode)new JsonObject { ["entity"] = 7u, ["clock"] = c })]);
            return s;
        }
        Assert.DoesNotContain(t.Observe(With(10.5), T0), x => x.Entry.Type == "death");
        Assert.Equal("Ann", t.Observe(With(10.5, 20.25), T0.AddMinutes(1)).Single(x => x.Entry.Type == "death").Entry.Name);
    }

    [Fact]
    public void Hostile_states_are_bounded_and_ignored()
    {
        var t = new PlayerTracker();
        Assert.Empty(t.Observe(null, T0));
        Assert.Empty(t.Observe(JsonNode.Parse("""{"players":"x"}"""), T0));
        var many = new JsonArray();
        for (var i = 0; i < 2000; i++) many.Add(new JsonObject { ["name"] = "p" + i });
        many.Add(new JsonObject { ["name"] = new string('x', 65) });
        many.Add(new JsonObject { ["name"] = "Eve\u0007\u2028", ["steamId"] = "not-a-steam-id" });
        t.Observe(new JsonObject { ["players"] = many }, T0);
        Assert.Equal(PlayerTracker.MaxPlayers, t.Online.Count);
        Assert.All(t.Online, p => Assert.DoesNotContain(p.Name, c => char.IsControl(c)));
    }
}

[Collection("lane-c")]
public sealed class ActivityComponentTests
{
    [Fact]
    public async Task Joins_leaves_and_deaths_reach_the_routes_the_log_and_webhooks()
    {
        await using var c = await Lc.StartAsync();
        var push = (Action<JsonNode?>)(n => c.PushState(n!.ToJsonString()));
        using var owner = await c.UserAsync("olive", BuiltinRoles.Owner);
        using var viewer = await c.UserAsync("vic", BuiltinRoles.Viewer);
        using var mod = await c.UserAsync("moe", BuiltinRoles.Moderator);
        await Lc.ReauthAsync(owner);
        var hook = new { name = "Discord", kind = "discord", url = "https://discord.com/api/webhooks/123456789012345678/abcdefghijklmnopqrstuvwxyz0123456789_-", events = new[] { "player.*" } };
        Assert.Equal(HttpStatusCode.Created, (await owner.PostAsync("/api/webhooks/hooks", hook)).StatusCode);

        push(JsonNode.Parse("""{"players":[{"name":"Ann","steamId":"76561198000000001","entity":7}]}"""));
        push(JsonNode.Parse("""{"players":[{"name":"Ann","steamId":"76561198000000001","entity":7},{"name":"Bob","entity":8}]}"""));
        push(JsonNode.Parse("""{"players":[{"name":"Bob","entity":8}],"combatDeaths":[]}"""));
        push(JsonNode.Parse("""{"players":[{"name":"Bob","entity":8}],"combatDeaths":[{"entity":8,"time":1764072000,"victimName":"Bob","killerName":"Wolf"}]}"""));

        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/activity/online")).StatusCode);
        var online = await Lc.JsonAsync(await mod.GetAsync("/api/activity/online"));
        Assert.Equal("Bob", online!["players"]!.AsArray().Single()!["name"]!.GetValue<string>());
        var events = (await Lc.JsonAsync(await mod.GetAsync("/api/activity/events")))!["events"]!.AsArray();
        Assert.Equal(["death", "leave", "join", "join"], events.Select(e => e!["type"]!.GetValue<string>()));
        var only = (await Lc.JsonAsync(await mod.GetAsync("/api/activity/events?type=join&limit=1")))!;
        Assert.Single(only["events"]!.AsArray());
        Assert.True(only["more"]!.GetValue<bool>());
        var players = (await Lc.JsonAsync(await mod.GetAsync("/api/activity/players")))!["players"]!.AsArray();
        var ann = players.Single(p => p!["name"]!.GetValue<string>() == "Ann")!;
        Assert.False(ann["online"]!.GetValue<bool>());
        Assert.Equal(1, ann["sessions"]!.GetValue<int>());
        Assert.Equal(1, players.Single(p => p!["name"]!.GetValue<string>() == "Bob")!["deaths"]!.GetValue<int>());
        // the writer keeps the log open (Windows: read it shared, as the panel's own readers do)
        using (var log = new FileStream(Path.Combine(c.H.DataDir, ActivityComponent.LogFile), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            Assert.Contains("\"Wolf\"", new StreamReader(log).ReadToEnd());

        // webhooks: Bob's join, Ann's leave and Bob's death (Ann's first sighting is not news)
        await Eventually.True(() => c.Transport.Count >= 3, 5000, "three player events");
        string[] sent;
        lock (c.Transport.Sent) sent = [.. c.Transport.Sent.Select(s => Encoding.UTF8.GetString(s.Body))];
        Assert.Contains(sent, s => s.Contains("player.join") && s.Contains("Bob"));
        Assert.Contains(sent, s => s.Contains("player.leave") && s.Contains("Ann"));
        Assert.Contains(sent, s => s.Contains("player.death") && s.Contains("Wolf"));
        Assert.DoesNotContain(sent, s => s.Contains("player.join") && s.Contains("Ann"));
        Assert.DoesNotContain(sent, s => s.Contains("76561198000000001")); // SteamIDs never leave the panel
    }

    [Fact]
    public async Task A_restart_restores_who_was_on_and_closes_the_others_quietly()
    {
        using var data = new TempDir();
        string dataDir;
        await using (var c = await Lc.StartAsync(root: data))
        {
            dataDir = c.H.DataDir;
            c.PushState("""{"players":[{"name":"Ann","entity":1},{"name":"Bob","entity":2}]}""");
        }
        Assert.True(File.Exists(Path.Combine(dataDir, ActivityComponent.OnlineFile)));
        await using (var c = await Lc.StartAsync(root: data))
        {
            Assert.Equal(2, c.Activity.Tracker.Online.Count); // restored before any state
            c.PushState("""{"players":[{"name":"Ann","entity":1}]}""");
            var leave = c.Activity.Recent.Last();
            Assert.Equal(("leave", "Bob", "server stopped"), (leave.Type, leave.Name, leave.Detail));
            Assert.Equal(2, c.Activity.Recent.Count(e => e.Type == "join")); // the log's two, no second join for Ann
            Assert.Equal(0, c.Transport.Count);
        }
    }

    [Fact]
    public async Task You_marks_the_player_whose_SteamID_your_account_linked()
    {
        await using var c = await Lc.StartAsync();
        using var mod = await c.UserAsync("moe", BuiltinRoles.Moderator);
        var accounts = c.H.Host.Services.Get<AccountStore>();
        var moe = accounts.FindByName("moe")!;
        accounts.Update(moe.Id, a => a with { Links = new Dictionary<string, string> { ["steam"] = "76561198000000042" } });
        c.PushState("""{"players":[{"name":"Moe","steamId":"76561198000000042","entity":1},{"name":"Ann","entity":2}]}""");
        var players = (await Lc.JsonAsync(await mod.GetAsync("/api/activity/online")))!["players"]!.AsArray();
        Assert.True(players.Single(p => p!["name"]!.GetValue<string>() == "Moe")!["you"]!.GetValue<bool>());
        Assert.False(players.Single(p => p!["name"]!.GetValue<string>() == "Ann")!["you"]!.GetValue<bool>());
    }
}
