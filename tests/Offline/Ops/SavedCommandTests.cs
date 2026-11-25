// SPDX-License-Identifier: MIT
using System.Net;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Offline.Support;

namespace Kilnwarden.Offline.Ops;

public class SavedCommandTests
{
    private static async Task<string> SaveAsync(OpsHarness.Client c, object body)
    {
        var r = await c.PostAsync("/api/saved-commands", body);
        Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await OpsHarness.Client.JsonAsync(r))!["id"]!.GetValue<string>();
    }

    [Fact]
    public async Task A_saved_command_runs_as_the_runner_with_the_runners_level_and_is_audited_without_its_arguments()
    {
        await using var h = await OpsHarness.StartAsync();
        using var admin = await h.UserAsync("alice", BuiltinRoles.Admin);
        using var mod = await h.UserAsync("bob", BuiltinRoles.Moderator);
        var id = await SaveAsync(admin, new { name = "Greet", line = "/say secret-word hello", shared = true, form = new { command = "say", values = new[] { "hello" } } });
        var r = await mod.PostAsync($"/api/saved-commands/{id}/run");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var req = Assert.Single(h.Kap.Requests("command_run"));
        Assert.Equal("web:bob", req["args"]!["source"]!.GetValue<string>());
        Assert.Equal("everyone", req["args"]!["level"]!.GetValue<string>());
        await Eventually.True(() => h.Audit.Read(50).Any(e => e.GetProperty("op").GetString() == "saved-commands.run"), 3000, "the audit entry");
        var entry = h.Audit.Read(50).First(e => e.GetProperty("op").GetString() == "saved-commands.run");
        var text = entry.ToString();
        Assert.Contains("\"command\":\"say\"", text);
        Assert.DoesNotContain("secret-word", text);
        Assert.Equal("bob", entry.GetProperty("userName").GetString());
    }

    [Fact]
    public async Task Confirmation_visibility_and_who_may_edit()
    {
        await using var h = await OpsHarness.StartAsync();
        using var admin = await h.UserAsync("alice", BuiltinRoles.Admin);
        using var mod = await h.UserAsync("bob", BuiltinRoles.Moderator);
        using var mod2 = await h.UserAsync("carol", BuiltinRoles.Moderator);
        using var viewer = await h.UserAsync("vic", BuiltinRoles.Viewer);
        var shared = await SaveAsync(mod, new { name = "Heal all", line = "/heal @a", shared = true, confirm = true });
        var mine = await SaveAsync(mod, new { name = "Private", line = "/time day" });
        // confirm
        var r = await mod2.PostAsync($"/api/saved-commands/{shared}/run");
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Equal("confirm_required", await OpsHarness.Client.CodeAsync(r));
        Assert.Equal(HttpStatusCode.OK, (await mod2.PostAsync($"/api/saved-commands/{shared}/run", new { confirmed = true })).StatusCode);
        // visibility: the private one is bob's; a viewer (no command_run) sees nothing at all
        var list = (await OpsHarness.Client.JsonAsync(await mod2.GetAsync("/api/saved-commands")))!.AsArray();
        Assert.Equal([shared], list.Select(x => x!["id"]!.GetValue<string>()));
        Assert.Equal(HttpStatusCode.NotFound, (await mod2.PostAsync($"/api/saved-commands/{mine}/run")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/saved-commands")).StatusCode);
        // edit: carol (moderator) may not change bob's shared one; alice (admin, manage_all) may
        var edit = new { name = "Heal everyone", line = "/heal @a", shared = true, confirm = true };
        Assert.Equal(HttpStatusCode.Forbidden, (await mod2.PutAsync($"/api/saved-commands/{shared}", edit)).StatusCode);
        var ok = await OpsHarness.Client.JsonAsync(await admin.PutAsync($"/api/saved-commands/{shared}", edit));
        Assert.Equal(2, ok!["revision"]!.GetValue<long>());
        Assert.Equal("bob", ok["owner"]!["name"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.NoContent, (await mod.DeleteAsync($"/api/saved-commands/{mine}")).StatusCode);
    }

    [Fact]
    public async Task A_console_level_run_needs_recent_proof_and_bad_lines_are_refused()
    {
        await using var h = await OpsHarness.StartAsync();
        using var owner = await h.UserAsync("olivia", BuiltinRoles.Owner);
        var id = await SaveAsync(owner, new { name = "Stop", line = "/stop" });
        var r = await owner.PostAsync($"/api/saved-commands/{id}/run");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Equal("reauth_required", await OpsHarness.Client.CodeAsync(r));
        Assert.Empty(h.Kap.Requests("command_run"));
        await owner.ReauthAsync();
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync($"/api/saved-commands/{id}/run")).StatusCode);
        Assert.Equal((HttpStatusCode)422, (await owner.PostAsync("/api/saved-commands", new { name = "x", line = "/say a\nb" })).StatusCode);
        Assert.Equal((HttpStatusCode)422, (await owner.PostAsync("/api/saved-commands", new { name = "x", line = "   " })).StatusCode);
        Assert.Equal((HttpStatusCode)422, (await owner.PostAsync("/api/saved-commands", new { name = "x", line = "/say hi", form = new { big = new string('a', 9000) } })).StatusCode);
    }

    [Fact]
    public async Task A_role_with_command_rules_cannot_save_or_run_a_command_it_may_not_use()
    {
        await using var h = await OpsHarness.StartAsync(k => k.Answer = req => new JsonObject
        {
            ["t"] = "res", ["ok"] = true, ["op"] = req["op"]!.GetValue<string>(), ["ms"] = 1,
            ["data"] = req["op"]!.GetValue<string>() == "commands_describe"
                ? new JsonArray(new JsonObject { ["name"] = "heal", ["permission"] = "everyone" }, new JsonObject { ["name"] = "kick", ["permission"] = "everyone" })
                : new JsonObject(),
        });
        h.Auth.Roles.Put(new RoleDef { Id = "healer", Name = "Healer", CommandLevel = Kilnwarden.Kap.CommandLevel.Everyone, CommandOnly = ["heal"], Grants = ["saved-commands.*", "commands.command_run"] });
        using var healer = await h.UserAsync("hank", "healer");
        Assert.Equal(HttpStatusCode.Forbidden, (await healer.PostAsync("/api/saved-commands", new { name = "Kick", line = "/kick bob" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await healer.PostAsync("/api/saved-commands", new { name = "Heal", line = "/heal hank" })).StatusCode);
        using var mod = await h.UserAsync("bob", BuiltinRoles.Moderator);
        var kick = await SaveAsync(mod, new { name = "Kick", line = "/kick someone", shared = true });
        var list = (await OpsHarness.Client.JsonAsync(await healer.GetAsync("/api/saved-commands")))!.AsArray();
        Assert.False(list.Single(x => x!["id"]!.GetValue<string>() == kick)!["runnable"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.Forbidden, (await healer.PostAsync($"/api/saved-commands/{kick}/run")).StatusCode);
        Assert.Empty(h.Kap.Requests("command_run"));
    }

    [Fact]
    public async Task Pins_are_per_user()
    {
        await using var h = await OpsHarness.StartAsync();
        using var mod = await h.UserAsync("bob", BuiltinRoles.Moderator);
        using var mod2 = await h.UserAsync("carol", BuiltinRoles.Moderator);
        var id = await SaveAsync(mod, new { name = "Day", line = "/time day", shared = true });
        Assert.Equal(HttpStatusCode.OK, (await mod.PutAsync($"/api/saved-commands/{id}/pin", new { pinned = true })).StatusCode);
        Assert.Single((await OpsHarness.Client.JsonAsync(await mod.GetAsync("/api/saved-commands/pinned")))!.AsArray());
        Assert.Empty((await OpsHarness.Client.JsonAsync(await mod2.GetAsync("/api/saved-commands/pinned")))!.AsArray());
    }
}
