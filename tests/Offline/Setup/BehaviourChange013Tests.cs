// SPDX-License-Identifier: MIT
// 0.1.3's changes of behaviour, written against the API 0.1.2 already had, so they ran red on 0.1.2 first: the owner's
// Discord link returns to the setup wizard, Discord sign-in for the owner alone keeps everyone else out, a "not a member
// of the Discord server" answer is told apart, the wizard's "Sign in with Discord" switches it on for the owner, and
// the Kiln Launcher's map art is found on this computer and copied in one click.
using System.Net;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Identity;
using Kilnwarden.Offline.Identity;
using Kilnwarden.Offline.Support;
using Kilnwarden.Setup;

namespace Kilnwarden.Offline.Setup;

[CollectionDefinition("launcher-env", DisableParallelization = true)]
public sealed class LauncherEnvCollection;

[Collection("launcher-env")]
public sealed class BehaviourChange013Tests
{
    private const string Guild = "999999999999999999";
    private const string Kiln = "1568454645683195976";

    private static Task<LaneHost> StartAsync(string identityJson, bool setup = false) =>
        LaneHost.StartAsync(() => setup
            ? [new IdentityComponent(new IdentityOptions { BrokerKeys = [MockBroker.Key] }), new SetupComponent()]
            : [new IdentityComponent(new IdentityOptions { BrokerKeys = [MockBroker.Key] })],
            (h, _) => File.WriteAllText(Path.Combine(h.DataDir, "identity.json"), identityJson));

    private static string Identity(bool enabled, string guild = Guild, bool ownerOnly = false) =>
        new JsonObject { ["discord"] = new JsonObject { ["enabled"] = enabled, ["broker"] = MockBroker.Origin, ["guildId"] = guild, ["ownerOnly"] = ownerOnly } }.ToJsonString();

    private static async Task<JsonNode> Json(HttpResponseMessage r) => (await LaneHost.Browser.JsonAsync(r))!;

    private static async Task<string> Code(HttpResponseMessage r) => (await Json(r))["error"]!["code"]!.GetValue<string>();

    [Fact]
    public async Task A_discord_link_started_by_the_setup_wizard_returns_to_it()
    {
        await using var h = await StartAsync(Identity(true));
        await h.AccountAsync("boss", BuiltinRoles.Owner);
        using var b = h.NewBrowser();
        await b.SignInAsync("boss");
        await b.ReauthAsync();
        var r = await b.PostAsync("/api/identity/start", new { provider = "discord", intent = "link", back = "setup" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var (ret, payload) = MockBroker.For((await Json(r))["url"]!.GetValue<string>(), guild: Guild);
        var cb = await b.Http.GetAsync(ret + "&assertion=" + MockBroker.Sign(payload));
        Assert.Equal(HttpStatusCode.SeeOther, cb.StatusCode);
        Assert.StartsWith("/setup#ext=", cb.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task A_discord_answer_of_not_a_member_is_told_apart()
    {
        await using var h = await StartAsync(Identity(true));
        using var b = h.NewBrowser();
        var r = await b.PostAsync("/api/identity/start", new { provider = "discord", intent = "login" });
        var (ret, _) = MockBroker.For((await Json(r))["url"]!.GetValue<string>());
        var cb = await b.Http.GetAsync(ret + "&error=not_member");
        var state = cb.Headers.Location!.OriginalString.Split("#ext=")[1];
        var done = await b.PostAsync("/api/identity/complete", new { state });
        Assert.Equal(HttpStatusCode.Forbidden, done.StatusCode);
        Assert.Equal("not_member", await Code(done));
    }

    [Fact]
    public async Task Discord_for_the_owner_alone_keeps_everyone_else_out()
    {
        await using var h = await StartAsync(Identity(true, ownerOnly: true));
        var vera = await h.AccountAsync("vera");
        using var v = h.NewBrowser();
        await v.SignInAsync("vera");
        await v.ReauthAsync();
        var r = await v.PostAsync("/api/identity/start", new { provider = "discord", intent = "link" });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Equal("owner_only", await Code(r));
        // an account linked before (by hand, or before ownerOnly was on) does not sign in with Discord either
        h.Auth.Accounts.Update(vera.Id, x => x with { Links = new Dictionary<string, string> { ["discord"] = "123456789012345678" } });
        using var fresh = h.NewBrowser();
        var s = await fresh.PostAsync("/api/identity/start", new { provider = "discord", intent = "login" });
        var (ret, payload) = MockBroker.For((await Json(s))["url"]!.GetValue<string>(), guild: Guild);
        var cb = await fresh.Http.GetAsync(ret + "&assertion=" + MockBroker.Sign(payload));
        var state = cb.Headers.Location!.OriginalString.Split("#ext=")[1];
        var done = await fresh.PostAsync("/api/identity/complete", new { state });
        Assert.Equal(HttpStatusCode.Forbidden, done.StatusCode);
        Assert.Equal("owner_only", await Code(done));
    }

    [Fact]
    public async Task The_setup_wizard_switches_discord_on_for_the_owner_alone()
    {
        await using var h = await StartAsync(Identity(false, guild: ""), setup: true);
        await h.AccountAsync("boss", BuiltinRoles.Owner);
        using var b = h.NewBrowser();
        await b.SignInAsync("boss");
        await b.ReauthAsync();
        // "anyway": no broker check (the test broker has no address)
        var r = await b.PostAsync("/api/setup/discord/me", new { anyway = true });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var id = JsonNode.Parse(File.ReadAllText(Path.Combine(h.DataDir, "identity.json")))!["discord"]!;
        Assert.True(id["enabled"]!.GetValue<bool>());
        Assert.True(id["ownerOnly"]!.GetValue<bool>());
        Assert.Equal(Kiln, id["guildId"]!.GetValue<string>());
    }

    [Fact]
    public async Task The_kiln_launchers_map_art_on_this_computer_is_copied_in_one_click()
    {
        using var launcher = new TempDir();
        var tiles = Path.Combine(launcher.Path, "admin-assets", "map", "tiles", "0");
        Directory.CreateDirectory(tiles);
        File.WriteAllBytes(Path.Combine(tiles, "0_0.webp"), [.. "RIFF\0\0\0\0WEBPVP8 "u8]);
        var before = Environment.GetEnvironmentVariable("KILN_DATA_DIR");
        Environment.SetEnvironmentVariable("KILN_DATA_DIR", launcher.Path);
        try
        {
            await using var c = await LaneC.LaneC.StartAsync(extra: [new Kilnwarden.Static.MapAssetsComponent()]);
            using var owner = await c.UserAsync("olive", BuiltinRoles.Owner);
            await LaneC.LaneC.ReauthAsync(owner);
            var status = (await LaneC.LaneC.JsonAsync(await owner.GetAsync("/api/core/map-assets-status")))!;
            Assert.False(status["present"]!.GetValue<bool>());
            Assert.True(status["launcher"]?.GetValue<bool>());
            var r = await owner.PostAsync("/api/core/map-assets/copy");
            Assert.Equal(HttpStatusCode.Accepted, r.StatusCode);
            for (var i = 0; i < 100 && !File.Exists(Path.Combine(c.H.DataDir, "map-assets", "map", "tiles", "0", "0_0.webp")); i++) await Task.Delay(50);
            Assert.True((await LaneC.LaneC.JsonAsync(await owner.GetAsync("/api/core/map-assets-status")))!["present"]!.GetValue<bool>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("KILN_DATA_DIR", before);
        }
    }
}
