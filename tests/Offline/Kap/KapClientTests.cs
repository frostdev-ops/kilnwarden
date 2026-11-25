// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;
using Kilnwarden.Kap;
using Kilnwarden.Offline.Support;

namespace Kilnwarden.Offline.Kap;

public class KapClientTests
{
    private static KapClient Client(string dir, TestLog log, Func<string, TimeSpan>? timeout = null) => new(new KapClientOptions
    {
        AdminDirectory = dir, Log = log, Backoff = [TimeSpan.FromMilliseconds(30), TimeSpan.FromMilliseconds(60)],
        KeepaliveTick = TimeSpan.FromMilliseconds(50), OpTimeout = timeout ?? (_ => TimeSpan.FromSeconds(5)),
    });

    private static string Op(JsonObject f) => f["op"]!.GetValue<string>();

    [Fact]
    public async Task Handshake_proves_the_token_both_ways_and_reads_ready()
    {
        using var tmp = new TempDir();
        using var kap = new FakeKap(tmp.Path);
        var log = new TestLog();
        await using var c = Client(tmp.Path, log);
        c.Start();
        var s = await c.WaitReadyAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(s);
        Assert.Contains("kill", s!.Ops);
        Assert.Equal(["state", "log", "metrics"], s.Topics);
        Assert.Equal("0.2.0-dev.30.9", s.Kiln);
        var auth = kap.Frames.First(f => f["t"]!.GetValue<string>() == "auth");
        Assert.StartsWith(Product.ModId + "/", auth["client"]!.GetValue<string>());
        Assert.True(log.Has("KAP: connected"));
    }

    [Fact]
    public async Task A_server_that_cannot_prove_the_token_is_never_used()
    {
        using var tmp = new TempDir();
        using var kap = new FakeKap(tmp.Path) { WrongReadyMac = true };
        var log = new TestLog();
        await using var c = Client(tmp.Path, log);
        c.Start();
        Assert.Null(await c.WaitReadyAsync(TimeSpan.FromMilliseconds(600)));
        Assert.True(kap.Handshakes >= 1);
        Assert.True(log.Has("ready mac is wrong"));
        Assert.Empty(kap.Frames.Where(f => f["t"]!.GetValue<string>() == "req"));
    }

    [Fact]
    public async Task Calls_answer_by_id_and_unknown_ops_are_not_sent()
    {
        using var tmp = new TempDir();
        using var kap = new FakeKap(tmp.Path);
        await using var c = Client(tmp.Path, new TestLog());
        c.Start();
        await c.WaitReadyAsync(TimeSpan.FromSeconds(5));
        var r = await c.CallAsync(KapCaller.ForUser("ann", CommandLevel.None), "players", new JsonObject { ["x"] = 1 });
        Assert.True(r.Ok, r.ToString());
        Assert.Equal(1, r.Data!["args"]!["x"]!.GetValue<int>());
        var u = await c.CallAsync(KapCaller.ForUser("ann", CommandLevel.None), "not_offered");
        Assert.Equal("unknown_op", u.Code);
        Assert.Empty(kap.Requests("not_offered"));
    }

    /// <summary>BINDING, end to end: what reaches Kiln.Admin.</summary>
    [Fact]
    public async Task Every_command_op_that_reaches_Kiln_Admin_carries_source_and_level()
    {
        using var tmp = new TempDir();
        using var kap = new FakeKap(tmp.Path);
        await using var c = Client(tmp.Path, new TestLog());
        c.Start();
        await c.WaitReadyAsync(TimeSpan.FromSeconds(5));
        var mod = KapCaller.ForUser("mia", CommandLevel.Everyone);
        await c.CallAsync(mod, "command_run", new JsonObject { ["line"] = "/kick bob", ["source"] = "dashboard", ["level"] = "console" });
        await c.CallAsync(mod, "command_complete", new JsonObject { ["text"] = "/ki" });
        await c.CallAsync(mod, "commands_describe");
        // the panel and level-less users cannot send them at all
        Assert.Equal("refused", (await c.CallAsync(KapCaller.Panel, "command_run", new JsonObject { ["line"] = "/stop" })).Code);
        Assert.Equal("refused", (await c.CallAsync(KapCaller.ForUser("vic", CommandLevel.None), "commands_describe")).Code);
        Assert.Equal("refused", (await c.CallAsync(mod, "command", new JsonObject { ["line"] = "/stop" })).Code);
        var all = kap.Frames.Where(f => f["t"]!.GetValue<string>() == "req" && KapOps.CommandOps.Contains(Op(f))).ToList();
        // the connect-time capability probe carries both too (a level Kiln.Admin 30.9 refuses: nothing is described)
        var probe = Assert.Single(all, f => f["args"]!["level"]!.GetValue<string>() == "level-probe");
        Assert.Equal("web:" + Product.ModId, probe["args"]!["source"]!.GetValue<string>());
        var sent = all.Where(f => f != probe).ToList();
        Assert.Equal(3, sent.Count);
        foreach (var f in sent)
        {
            Assert.Equal("web:mia", f["args"]!["source"]!.GetValue<string>());
            Assert.Equal("everyone", f["args"]!["level"]!.GetValue<string>());
        }
        Assert.Empty(kap.Requests("command"));
    }

    [Fact]
    public async Task Slow_ops_time_out_and_late_answers_are_dropped()
    {
        using var tmp = new TempDir();
        using var kap = new FakeKap(tmp.Path);
        kap.Answer = req => Op(req) == "players" ? null : new JsonObject { ["t"] = "res", ["ok"] = true, ["op"] = Op(req) };
        await using var c = Client(tmp.Path, new TestLog(), op => op == "players" ? TimeSpan.FromMilliseconds(200) : TimeSpan.FromSeconds(5));
        c.Start();
        await c.WaitReadyAsync(TimeSpan.FromSeconds(5));
        var r = await c.CallAsync(KapCaller.Panel, "players");
        Assert.Equal("timeout", r.Code);
        Assert.True((await c.CallAsync(KapCaller.Panel, "status")).Ok);
    }

    [Fact]
    public async Task The_inflight_cap_answers_busy()
    {
        using var tmp = new TempDir();
        using var kap = new FakeKap(tmp.Path) { Limits = new JsonObject { ["inflight"] = 2 } };
        var release = new TaskCompletionSource();
        kap.Answer = req =>
        {
            release.Task.Wait(3000);
            return new JsonObject { ["t"] = "res", ["ok"] = true, ["op"] = Op(req) };
        };
        await using var c = Client(tmp.Path, new TestLog());
        c.Start();
        await c.WaitReadyAsync(TimeSpan.FromSeconds(5));
        var a = c.CallAsync(KapCaller.Panel, "players");
        var b = c.CallAsync(KapCaller.Panel, "players");
        await Eventually.True(() => kap.Requests("players").Count() >= 1);
        var third = await c.CallAsync(KapCaller.Panel, "players");
        Assert.Equal("busy", third.Code);
        release.SetResult();
        Assert.True((await a).Ok);
        Assert.True((await b).Ok);
    }

    [Fact]
    public async Task Topics_are_subscribed_once_fanned_out_and_unsubscribed_with_the_last()
    {
        using var tmp = new TempDir();
        using var kap = new FakeKap(tmp.Path);
        await using var c = Client(tmp.Path, new TestLog());
        c.Start();
        await c.WaitReadyAsync(TimeSpan.FromSeconds(5));
        var got1 = new List<KapPush>();
        var got2 = new List<KapPush>();
        var s1 = c.Subscribe("state", p => { lock (got1) got1.Add(p); });
        var s2 = c.Subscribe("state", p => { lock (got2) got2.Add(p); });
        await Eventually.True(() => kap.Subscribed("state") == 1);
        Assert.Single(kap.Frames.Where(f => f["t"]!.GetValue<string>() == "sub"));
        kap.Push("state", new JsonObject { ["players"] = 3 });
        await Eventually.True(() => got1.Count == 1 && got2.Count == 1);
        Assert.Same(got1[0], got2[0]);
        Assert.Contains("\"players\":3", System.Text.Encoding.UTF8.GetString(got1[0].Payload.Span));
        Assert.NotNull(c.LastPush("state"));
        s1.Dispose();
        await Task.Delay(100);
        Assert.Equal(1, kap.Subscribed("state"));
        s2.Dispose();
        await Eventually.True(() => kap.Subscribed("state") == 0);
        // a topic the server does not offer is never asked for
        using var s3 = c.Subscribe("nope", _ => { });
        await Task.Delay(100);
        Assert.Empty(kap.Frames.Where(f => f["t"]!.GetValue<string>() == "sub" && f["topic"]!.GetValue<string>() == "nope"));
    }

    [Fact]
    public async Task Drops_and_byes_reconnect_and_resubscribe_and_pending_calls_fail()
    {
        using var tmp = new TempDir();
        using var kap = new FakeKap(tmp.Path);
        kap.Answer = req => Op(req) == "players" ? null : new JsonObject { ["t"] = "res", ["ok"] = true, ["op"] = Op(req) };
        var log = new TestLog();
        await using var c = Client(tmp.Path, log);
        var lost = 0;
        var connected = 0;
        c.Disconnected += _ => Interlocked.Increment(ref lost);
        c.Connected += _ => Interlocked.Increment(ref connected);
        c.Start();
        await c.WaitReadyAsync(TimeSpan.FromSeconds(5));
        using var sub = c.Subscribe("metrics", _ => { });
        await Eventually.True(() => kap.Subscribed("metrics") == 1);
        var pending = c.CallAsync(KapCaller.Panel, "players");
        await Eventually.True(() => kap.Requests("players").Any());
        kap.DropAll();
        Assert.Equal("shutting_down", (await pending).Code);
        await Eventually.True(() => kap.Handshakes == 2 && c.IsReady, 5000, "the reconnect");
        await Eventually.True(() => kap.Subscribed("metrics") == 1, 5000, "the re-subscription");
        kap.Bye("restart");
        await Eventually.True(() => kap.Handshakes == 3 && c.IsReady, 5000, "the reconnect after bye");
        // the events fire just after the state flips
        await Eventually.True(() => Volatile.Read(ref lost) >= 2 && Volatile.Read(ref connected) >= 3, 2000, "the events");
        Assert.True(log.Has("bye from Kiln.Admin (restart)"));
    }

    [Fact]
    public async Task A_new_endpoint_and_token_are_read_at_each_connect()
    {
        using var tmp = new TempDir();
        var first = new FakeKap(tmp.Path);
        await using var c = Client(tmp.Path, new TestLog());
        c.Start();
        await c.WaitReadyAsync(TimeSpan.FromSeconds(5));
        using var second = new FakeKap(tmp.Path); // new port, new token, same folder (a server restart)
        first.Dispose();
        await Eventually.True(() => second.Handshakes == 1 && c.IsReady, 5000, "the new server");
        Assert.True((await c.CallAsync(KapCaller.Panel, "status")).Ok);
    }

    [Fact]
    public async Task Pings_are_answered_and_stop_says_bye()
    {
        using var tmp = new TempDir();
        using var kap = new FakeKap(tmp.Path) { Limits = new JsonObject { ["pingS"] = 1, ["idleS"] = 5 } };
        var c = Client(tmp.Path, new TestLog());
        c.Start();
        await c.WaitReadyAsync(TimeSpan.FromSeconds(5));
        // a quiet client pings after limits.pingS
        await Eventually.True(() => kap.Frames.Any(f => f["t"]!.GetValue<string>() == "ping"), 4000, "a ping");
        await c.StopAsync();
        await Eventually.True(() => kap.Frames.Any(f => f["t"]!.GetValue<string>() == "bye"), 2000, "the bye");
        Assert.False(c.IsReady);
        var before = kap.Connections;
        await Task.Delay(200);
        Assert.Equal(before, kap.Connections);
        Assert.Equal("shutting_down", (await c.CallAsync(KapCaller.Panel, "status")).Code);
    }

    [Fact]
    public async Task No_endpoint_file_means_retrying_quietly()
    {
        using var tmp = new TempDir();
        var log = new TestLog();
        await using var c = Client(tmp.Path, log);
        c.Start();
        Assert.Null(await c.WaitReadyAsync(TimeSpan.FromMilliseconds(400)));
        // the same reason is logged once a minute, not at every retry
        Assert.Single(log.Lines.Where(l => l.Contains("endpoint.json is not there")));
        using var kap = new FakeKap(tmp.Path);
        Assert.NotNull(await c.WaitReadyAsync(TimeSpan.FromSeconds(5)));
    }
}

/// <summary>Kiln's host does not enforce the kiln >= 0.2.0-dev.30.9 dependency against older dev builds, so the client
/// checks Kiln.Admin itself at connect (coordinator, 2026-11-23).</summary>
public class KapCompatibilityTests
{
    private static KapClient Client(string dir, TestLog log) => new(new KapClientOptions
    {
        AdminDirectory = dir, Log = log, Backoff = [TimeSpan.FromMilliseconds(30)], KeepaliveTick = TimeSpan.FromMilliseconds(50),
    });

    [Fact]
    public async Task An_old_Kiln_Admin_is_refused_once_with_one_clear_line_and_no_retry()
    {
        using var tmp = new TempDir();
        using var kap = new FakeKap(tmp.Path);
        kap.BeOld();
        var log = new TestLog();
        await using var c = Client(tmp.Path, log);
        IReadOnlyList<string>? missing = null;
        c.Incompatible += m => missing = m;
        c.Start();
        await Eventually.True(() => c.MissingCapabilities is not null, 5000, "the verdict");
        Assert.False(c.IsReady);
        Assert.Null(await c.WaitReadyAsync(TimeSpan.FromMilliseconds(200)));
        Assert.Equal(
            ["login_mint", "login_redeem", "server_restart", "server_restart_cancel", "update_list", "update_stage", "update_cancel", "update_rollback",
             "topic metrics", "command_run source and level"], c.MissingCapabilities);
        Assert.Equal(c.MissingCapabilities, missing);
        var line = log.Lines.Single(l => l.Contains("needs Kiln"));
        Assert.Equal($"E {Product.Name} needs Kiln {Product.KilnFloor} or newer (Kiln.Admin is missing: login_mint, login_redeem, server_restart, " +
                     "server_restart_cancel, update_list, update_stage, update_cancel, update_rollback, topic metrics, command_run source and level)", line);
        // no retry loop: one connection, and it was closed with a bye
        await Task.Delay(400);
        Assert.Equal(1, kap.Connections);
        Assert.Contains(kap.Frames, f => f["t"]!.GetValue<string>() == "bye");
        Assert.Equal("shutting_down", (await c.CallAsync(KapCaller.Panel, "players")).Code);
    }

    [Fact]
    public async Task A_Kiln_Admin_that_ignores_level_is_refused_even_with_every_op()
    {
        using var tmp = new TempDir();
        using var kap = new FakeKap(tmp.Path) { IgnoresLevel = true };
        await using var c = Client(tmp.Path, new TestLog());
        c.Start();
        await Eventually.True(() => c.MissingCapabilities is not null, 5000, "the verdict");
        Assert.Equal(["command_run source and level"], c.MissingCapabilities);
    }

    [Fact]
    public async Task A_309_Kiln_Admin_passes_and_the_probe_carries_source_and_level()
    {
        using var tmp = new TempDir();
        using var kap = new FakeKap(tmp.Path);
        await using var c = Client(tmp.Path, new TestLog());
        c.Start();
        Assert.NotNull(await c.WaitReadyAsync(TimeSpan.FromSeconds(5)));
        Assert.Null(c.MissingCapabilities);
        var probe = kap.Requests("commands_describe").Single();
        Assert.StartsWith("web:", probe["args"]!["source"]!.GetValue<string>());
        Assert.NotNull(probe["args"]!["level"]);
    }
}
