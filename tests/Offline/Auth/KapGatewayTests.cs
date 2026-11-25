// SPDX-License-Identifier: MIT
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Kap;
using Kilnwarden.Offline.Support;

namespace Kilnwarden.Offline.Auth;

/// <summary>The admin pages' KAP WebSocket (admin-protocol.md §12) against a fake Kiln.Admin.</summary>
public class KapGatewayTests
{
    private static string S(JsonNode? n) => n!.GetValue<string>();
    private static List<string> Strings(JsonNode? a) => [.. a!.AsArray().Select(x => x!.GetValue<string>())];

    private static async Task<(AuthHarness.Client Browser, AuthHarness.KapSocket Socket, JsonObject Ready)> OpenAsync(AuthHarness h, string name, params string[] roles)
    {
        await h.AccountAsync(name, roles);
        var b = h.NewClient();
        await b.SignInAsync(name);
        var s = await b.KapAsync();
        var (_, ready) = await s.OpenAsync();
        return (b, s, ready);
    }

    [Fact]
    public async Task Sockets_without_a_session_close_4401()
    {
        await using var h = await AuthHarness.StartAsync();
        using var anon = h.NewClient();
        using var s = await anon.KapAsync();
        Assert.Equal(KapGateway.NotSignedIn, await s.ClosedAsync());
    }

    [Fact]
    public async Task Restricted_accounts_close_4403()
    {
        await using var h = await AuthHarness.StartAsync();
        var a = await h.AccountAsync("newbie", "admin");
        h.Auth.Accounts.Update(a.Id, x => x with { MustChangePassword = true });
        using var b = h.NewClient();
        await b.SignInAsync("newbie");
        using var s = await b.KapAsync();
        Assert.Equal(KapGateway.NotAllowed, await s.ClosedAsync());
    }

    [Fact]
    public async Task Ready_shows_only_what_the_user_may_use()
    {
        await using var h = await AuthHarness.StartAsync();
        var (b, s, ready) = await OpenAsync(h, "vic", "viewer");
        using var _ = b;
        using var __ = s;
        var ops = Strings(ready["ops"]);
        Assert.Contains("players", ops);
        Assert.Contains("ping", ops);
        foreach (var hidden in new[] { "kill", "command_run", "command", "login_mint", "login_redeem", "admin_token_rotate", "mod_load", "brand_new_op" })
            Assert.DoesNotContain(hidden, ops);
        Assert.Equal(["state", "metrics"], Strings(ready["topics"]));
        Assert.Equal("unknown", S(ready["restart"]!["supported"]));
        Assert.Null(ready["mac"]);
    }

    [Fact]
    public async Task Every_op_is_gated_and_refusals_are_audited()
    {
        await using var h = await AuthHarness.StartAsync();
        var (b, s, _) = await OpenAsync(h, "vic", "viewer");
        using var _1 = b;
        using var _2 = s;
        var ok = await s.CallAsync("players");
        Assert.True(ok["ok"]!.GetValue<bool>());
        foreach (var op in new[] { "kill", "login_mint", "login_redeem", "command", "admin_token_rotate", "brand_new_op", "Bad Op" })
        {
            var r = await s.CallAsync(op, new JsonObject { ["secret"] = "x", ["consumer"] = "kilnwarden" });
            Assert.False(r["ok"]!.GetValue<bool>());
            Assert.Equal("forbidden", S(r["code"]));
        }
        Assert.Empty(h.Kap!.Requests("kill"));
        Assert.Empty(h.Kap.Requests("login_mint"));
        Assert.Empty(h.Kap.Requests("login_redeem"));
        Assert.Empty(h.Kap.Requests("command"));
        await Eventually.True(() => h.Auth.Audit.Read(100).Any(e => S(JsonNode.Parse(e.GetRawText())!["op"]) == "kap.kill" && !e.GetProperty("ok").GetBoolean()));
    }

    /// <summary>BINDING through the browser's path: a moderator's lines reach Kiln.Admin as web:&lt;name&gt; at "everyone",
    /// whatever the page sends.</summary>
    [Fact]
    public async Task Command_ops_from_a_page_carry_the_users_source_and_level()
    {
        await using var h = await AuthHarness.StartAsync();
        var (b, s, ready) = await OpenAsync(h, "mia", "moderator");
        using var _1 = b;
        using var _2 = s;
        Assert.Contains("command_run", Strings(ready["ops"]));
        await s.CallAsync("command_run", new JsonObject { ["line"] = "/kick bob", ["source"] = "dashboard", ["level"] = "console" });
        await s.CallAsync("command_complete", new JsonObject { ["text"] = "/ki", ["level"] = "admin" });
        await s.CallAsync("commands_describe", new JsonObject { ["source"] = "web:owner" });
        var sent = h.Kap!.Frames.Where(f => S(f["t"]) == "req" && KapOps.CommandOps.Contains(S(f["op"])) && S(f["args"]!["level"]) != "level-probe").ToList();
        Assert.Equal(3, sent.Count);
        Assert.All(sent, f =>
        {
            Assert.Equal("web:mia", S(f["args"]!["source"]));
            Assert.Equal("everyone", S(f["args"]!["level"]));
        });
        // running a line as a player needs commands.run_as (admins have it, moderators not)
        var asPlayer = await s.CallAsync("command_run", new JsonObject { ["line"] = "/home", ["steamId"] = "76561198000000000" });
        Assert.Equal("forbidden", S(asPlayer["code"]));
        var (b2, s2, _) = await OpenAsync(h, "ada", "admin");
        using var _3 = b2;
        using var _4 = s2;
        Assert.True((await s2.CallAsync("command_run", new JsonObject { ["line"] = "/home", ["steamId"] = "76561198000000000" }))["ok"]!.GetValue<bool>());
        Assert.Equal("admin", S(h.Kap.Requests("command_run").Last()["args"]!["level"]));
        // restart and update ops record the user, not the page's claim
        var (b3, s3, _) = await OpenAsync(h, "own", "owner");
        using var _5 = b3;
        using var _6 = s3;
        Assert.Equal(System.Net.HttpStatusCode.OK, (await b3.PostAsync("/api/auth/reauth", new { password = AuthHarness.Password })).StatusCode);
        await s3.CallAsync("server_restart", new JsonObject { ["seconds"] = 60, ["by"] = "someone else" });
        Assert.Equal("web:own", S(h.Kap.Requests("server_restart").Single()["args"]!["by"]));
    }

    /// <summary>Audit follow-up through the browser's path: command_run entries name the command and carry safe
    /// metadata, for lines that ran and lines the gate refused; the arguments never reach the audit log.</summary>
    [Fact]
    public async Task Command_run_audit_entries_name_the_command_without_its_arguments()
    {
        await using var h = await AuthHarness.StartAsync();
        var (b, s, _) = await OpenAsync(h, "mia", "moderator");
        using var _1 = b;
        using var _2 = s;
        Assert.True((await s.CallAsync("command_run", new JsonObject { ["line"] = "/kick bob private-fixture-secret" }))["ok"]!.GetValue<bool>());
        Assert.Equal("forbidden", S((await s.CallAsync("command_run", new JsonObject { ["line"] = "/home private-fixture-secret", ["steamId"] = "76561198000000042" }))["code"]));
        List<JsonNode> runs = [];
        await Eventually.True(() =>
        {
            runs = [.. h.Auth.Audit.Read(100).Select(e => JsonNode.Parse(e.GetRawText())!).Where(e => S(e["op"]) == "kap.command_run")];
            return runs.Count == 2;
        });
        var ran = runs.Single(e => e["ok"]!.GetValue<bool>());
        Assert.Equal("kick", S(ran["args"]!["command"]));
        Assert.Equal(2, ran["args"]!["argCount"]!.GetValue<int>());
        Assert.Equal("everyone", S(ran["args"]!["level"]));
        Assert.False(ran["args"]!["asPlayer"]!.GetValue<bool>());
        var refused = runs.Single(e => !e["ok"]!.GetValue<bool>());
        Assert.Equal("home", S(refused["args"]!["command"]));
        Assert.True(refused["args"]!["asPlayer"]!.GetValue<bool>());
        var all = string.Join("\n", h.Auth.Audit.Read(1000).Select(e => e.GetRawText()));
        Assert.DoesNotContain("private-fixture-secret", all);
        Assert.DoesNotContain("76561198000000042", all);
        Assert.DoesNotContain("bob", all);
    }

    /// <summary>Audit follow-up 2 through the browser's path: the entry's ok is still the transport outcome (the line
    /// reached Kiln.Admin, which answered), and outcome carries the command's own result as Kiln.Admin's command_run
    /// reports it: ran ok, no such command, denied, parse error. No output or error text, line, arguments or steamId.</summary>
    [Fact]
    public async Task Command_run_audit_entries_carry_the_commands_own_outcome()
    {
        await using var h = await AuthHarness.StartAsync(setup: k =>
        {
            var inner = k.Answer;
            k.Answer = req =>
            {
                if (S(req["op"]) != "command_run") return inner(req);
                var name = S(req["args"]!["line"]).TrimStart('/').Split(' ')[0];
                var (found, ok, denied, parseError, error) = name switch
                {
                    "kick" => (true, true, false, false, (string?)null),
                    "time" => (false, false, false, false, "no command '/time' private-fixture-error"),
                    "stop" => (true, false, true, false, "you may not private-fixture-error"),
                    _ => (true, false, false, true, "bad argument private-fixture-error"),
                };
                return new JsonObject
                {
                    ["t"] = "res", ["ok"] = true, ["op"] = "command_run", ["ms"] = 1,
                    ["data"] = new JsonObject
                    {
                        ["name"] = name, ["found"] = found, ["ok"] = ok, ["denied"] = denied, ["canceled"] = false, ["parseError"] = parseError,
                        ["error"] = error, ["output"] = new JsonArray("private-fixture-output"),
                    },
                };
            };
        });
        var (b, s, _) = await OpenAsync(h, "ada", "admin");
        using var _1 = b;
        using var _2 = s;
        foreach (var line in new[] { "/kick bob private-fixture-secret", "/time private-fixture-secret", "/stop private-fixture-secret", "/give private-fixture-secret" })
            Assert.True((await s.CallAsync("command_run", new JsonObject { ["line"] = line, ["steamId"] = "76561198000000042" }))["ok"]!.GetValue<bool>());
        List<JsonNode> runs = [];
        await Eventually.True(() =>
        {
            runs = [.. h.Auth.Audit.Read(100).Select(e => JsonNode.Parse(e.GetRawText())!).Where(e => S(e["op"]) == "kap.command_run")];
            return runs.Count == 4;
        });
        JsonNode Outcome(string command)
        {
            var e = runs.Single(x => x["args"]!["command"]?.GetValue<string>() == command);
            Assert.True(e["ok"]!.GetValue<bool>()); // transport: reached Kiln.Admin and it answered
            Assert.Null(e["error"]);
            return e["args"]!["outcome"]!;
        }
        bool B(JsonNode o, string f) => o[f]!.GetValue<bool>();
        var ran = Outcome("kick");
        Assert.True(B(ran, "found") && B(ran, "ok") && !B(ran, "denied") && !B(ran, "parseError") && !B(ran, "canceled"));
        var missing = Outcome("time");
        Assert.True(!B(missing, "found") && !B(missing, "ok") && !B(missing, "denied") && !B(missing, "parseError"));
        var denied = Outcome("stop");
        Assert.True(B(denied, "found") && !B(denied, "ok") && B(denied, "denied") && !B(denied, "parseError"));
        var parse = Outcome("give");
        Assert.True(B(parse, "found") && !B(parse, "ok") && !B(parse, "denied") && B(parse, "parseError"));
        var all = string.Join("\n", h.Auth.Audit.Read(1000).Select(e => e.GetRawText()));
        foreach (var leak in new[] { "private-fixture", "76561198000000042", "bob", "no command" })
            Assert.DoesNotContain(leak, all);
    }

    [Fact]
    public async Task Role_command_rules_pick_the_level_or_refuse()
    {
        await using var h = await AuthHarness.StartAsync(setup: k =>
        {
            var inner = k.Answer;
            k.Answer = req => S(req["op"]) == "commands_describe"
                ? new JsonObject
                {
                    ["t"] = "res", ["ok"] = true, ["op"] = "commands_describe",
                    ["data"] = new JsonArray(
                        new JsonObject { ["name"] = "kick", ["aliases"] = new JsonArray("k"), ["permission"] = "admin", ["node"] = "kiln.command.kick" },
                        new JsonObject { ["name"] = "stop", ["aliases"] = new JsonArray(), ["permission"] = "admin", ["node"] = "kiln.command.stop" },
                        new JsonObject { ["name"] = "help", ["aliases"] = new JsonArray(), ["permission"] = "everyone", ["node"] = "kiln.command.help" }),
                }
                : inner(req);
        });
        h.Auth.Roles.Put(new RoleDef { Id = "kicker", Name = "Kicker", Grants = ["commands.command_run", "commands.commands_describe"], CommandLevel = CommandLevel.Admin, CommandOnly = ["kick"] });
        var (b, s, _) = await OpenAsync(h, "kim", "moderator", "kicker");
        using var _1 = b;
        using var _2 = s;
        Assert.True((await s.CallAsync("command_run", new JsonObject { ["line"] = "/k troll" }))["ok"]!.GetValue<bool>());
        Assert.Equal("admin", S(h.Kap!.Requests("command_run").Last()["args"]!["level"]));
        Assert.True((await s.CallAsync("command_run", new JsonObject { ["line"] = "help" }))["ok"]!.GetValue<bool>());
        Assert.Equal("everyone", S(h.Kap.Requests("command_run").Last()["args"]!["level"]));
        var stop = await s.CallAsync("command_run", new JsonObject { ["line"] = "/stop" });
        Assert.Equal("forbidden", S(stop["code"]));
        Assert.Equal(2, h.Kap.Requests("command_run").Count());
        var described = await s.CallAsync("commands_describe");
        Assert.Equal(["kick", "help"], described["data"]!.AsArray().Select(c => S(c!["name"])));
    }

    [Fact]
    public async Task Topics_are_gated_and_pushes_forwarded()
    {
        await using var h = await AuthHarness.StartAsync();
        var (b, s, _) = await OpenAsync(h, "vic", "viewer");
        using var _1 = b;
        using var _2 = s;
        await s.SendAsync(new JsonObject { ["t"] = "sub", ["id"] = 1, ["topic"] = "log" });
        var refused = await s.NextAsync(f => S(f["t"]) == "res" && f["id"]!.GetValue<int>() == 1);
        Assert.Equal("forbidden", S(refused["code"]));
        await s.SendAsync(new JsonObject { ["t"] = "sub", ["id"] = 2, ["topic"] = "state" });
        Assert.True((await s.NextAsync(f => S(f["t"]) == "res" && f["id"]!.GetValue<int>() == 2))["ok"]!.GetValue<bool>());
        await Eventually.True(() => h.Kap!.Subscribed("state") == 1);
        h.Kap!.Push("state", new JsonObject { ["players"] = 2 });
        var push = await s.NextAsync(f => S(f["t"]) == "push");
        Assert.Equal(2, push["data"]!["players"]!.GetValue<int>());
        Assert.Equal(0, h.Kap.Subscribed("log"));
        await s.SendAsync(new JsonObject { ["t"] = "unsub", ["id"] = 3, ["topic"] = "state" });
        await s.NextAsync(f => S(f["t"]) == "res" && f["id"]!.GetValue<int>() == 3);
        await Eventually.True(() => h.Kap.Subscribed("state") == 0);
    }

    [Fact]
    public async Task A_signed_out_or_demoted_user_loses_the_socket()
    {
        await using var h = await AuthHarness.StartAsync();
        var (b, s, _) = await OpenAsync(h, "ada", "admin");
        using var _1 = b;
        using var _2 = s;
        Assert.True((await s.CallAsync("kill", new JsonObject { ["entity"] = 5 }))["ok"]!.GetValue<bool>());
        // a role change applies to the very next message
        var a = h.Auth.Accounts.FindByName("ada")!;
        h.Auth.Accounts.Update(a.Id, x => x with { Roles = ["viewer"], PrivilegeStamp = x.PrivilegeStamp + 1 });
        Assert.Equal("forbidden", S((await s.CallAsync("kill", new JsonObject { ["entity"] = 5 }))["code"]));
        // signing out ends it (4401) without any message from the page (the periodic check)
        await b.PostAsync("/api/auth/logout");
        Assert.Equal(KapGateway.NotSignedIn, await s.ClosedAsync());
    }

    [Fact]
    public async Task Socket_requests_keep_the_session_alive_and_a_quiet_socket_times_out()
    {
        var time = new ManualTime(DateTimeOffset.UtcNow);
        await using var h = await AuthHarness.StartAsync(time: time);
        var (b, s, _) = await OpenAsync(h, "ada", "admin");
        using var _1 = b;
        using var _2 = s;
        for (var i = 0; i < 3; i++)
        {
            time.Advance(TimeSpan.FromMinutes(50));      // idle limit 60 min: only the requests keep it alive
            Assert.True((await s.CallAsync("players"))["ok"]!.GetValue<bool>());
        }
        time.Advance(TimeSpan.FromMinutes(61));
        Assert.Equal(KapGateway.NotSignedIn, await s.ClosedAsync());
    }

    [Fact]
    public async Task Kiln_Admin_dropping_sends_bye_and_1012()
    {
        await using var h = await AuthHarness.StartAsync();
        var (b, s, _) = await OpenAsync(h, "vic", "viewer");
        using var _1 = b;
        using var _2 = s;
        h.Kap!.DropAll();
        var bye = await s.NextAsync(f => S(f["t"]) == "bye");
        Assert.Equal("kiln-admin", S(bye["reason"]));
        Assert.Equal(KapGateway.ServiceRestart, await s.ClosedAsync());
        await Eventually.True(() => h.KapClient.IsReady, 5000, "the reconnect");
        using var again = await b.KapAsync();
        await again.OpenAsync();
    }

    [Fact]
    public async Task No_Kiln_Admin_means_an_error_frame_and_1013()
    {
        await using var h = await AuthHarness.StartAsync(kap: false);
        await h.AccountAsync("vic", "viewer");
        using var b = h.NewClient();
        await b.SignInAsync("vic");
        using var s = await b.KapAsync();
        var err = await s.NextAsync(f => S(f["t"]) == "error");
        Assert.Equal("shutting_down", S(err["code"]));
        Assert.Equal(KapGateway.TryAgainLater, await s.ClosedAsync());
    }

    [Fact]
    public async Task Bad_frames_are_answered_per_the_protocol()
    {
        await using var h = await AuthHarness.StartAsync();
        var (b, s, _) = await OpenAsync(h, "vic", "viewer");
        using var _1 = b;
        using var _2 = s;
        await s.SendAsync(new JsonObject { ["t"] = "wat" });
        Assert.Equal("bad_frame", S((await s.NextAsync(f => S(f["t"]) == "error"))["code"]));
        await s.SendAsync(new JsonObject { ["t"] = "req", ["op"] = "players" });
        Assert.Equal("bad_frame", S((await s.NextAsync(f => S(f["t"]) == "error"))["code"]));
        var args = await s.CallAsync("players", null);
        Assert.True(args["ok"]!.GetValue<bool>());
        await s.SendAsync(new JsonObject { ["t"] = "req", ["id"] = 99, ["op"] = "players", ["args"] = "nope" });
        Assert.False((await s.NextAsync(f => S(f["t"]) == "res" && f["id"]!.GetValue<int>() == 99))["ok"]!.GetValue<bool>());
        await s.SendAsync(new JsonObject { ["t"] = "ping", ["id"] = 5, ["ts"] = 123 });
        Assert.Equal(123, (await s.NextAsync(f => S(f["t"]) == "pong"))["ts"]!.GetValue<int>());
        await s.SendRawAsync("{\"t\":\"req\",\"t\":\"x\"}");
        await s.NextAsync(f => S(f["t"]) == "error");
        Assert.Equal(WebSocketCloseStatus.InvalidPayloadData, await s.ClosedAsync());
    }

    [Fact]
    public async Task Switched_off_modules_refuse_their_ops()
    {
        await using var h = await AuthHarness.StartAsync();
        h.Host.Modules.SetEnabled("combat", false);
        var (b, s, ready) = await OpenAsync(h, "ada", "admin");
        using var _1 = b;
        using var _2 = s;
        Assert.DoesNotContain("kill", Strings(ready["ops"]));
        Assert.Equal("forbidden", S((await s.CallAsync("kill", new JsonObject { ["entity"] = 1 }))["code"]));
    }

    [Fact]
    public async Task Sockets_per_account_are_capped()
    {
        await using var h = await AuthHarness.StartAsync();
        await h.AccountAsync("tabby", "viewer");
        using var b = h.NewClient();
        await b.SignInAsync("tabby");
        var open = new List<AuthHarness.KapSocket>();
        for (var i = 0; i < 8; i++)
        {
            var s = await b.KapAsync();
            await s.OpenAsync();
            open.Add(s);
        }
        using var ninth = await b.KapAsync();
        Assert.Equal(KapGateway.TryAgainLater, await ninth.ClosedAsync());
        foreach (var s in open) s.Dispose();
    }
}
