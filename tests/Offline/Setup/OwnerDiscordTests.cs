// SPDX-License-Identifier: MIT
// The setup wizard's "Sign in with Discord" for the owner (0.1.3): /api/setup/discord/me switches Discord sign-in on for
// the owner alone (the Kiln Discord when no server is named), checks the broker answers first, and the link itself is
// the usual one (a recent proof, the pinned broker key, state and nonce, the starting browser and session, one account
// per Discord user), returning to the wizard. Then the helpers' page opens it to everyone with an account.
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Config;
using Kilnwarden.Identity;
using Kilnwarden.Offline.Identity;
using Kilnwarden.Offline.Support;
using Kilnwarden.Setup;

namespace Kilnwarden.Offline.Setup;

public sealed class OwnerDiscordTests
{
    private const string Kiln = DiscordSettings.KilnGuild;
    private const string Mine = "222222222222222222";
    private const string Owners = "123456789012345678";

    private sealed class Rig : IAsyncDisposable
    {
        public LaneHost H = null!;
        public string? BrokerProblem;
        public int Checks;
        public SettingsStore<IdentitySettings> Identity => H.Host.Services.Get<SettingsStore<IdentitySettings>>();
        public ValueTask DisposeAsync() => H.DisposeAsync();
    }

    private static async Task<Rig> StartAsync(IdentitySettings? identity = null, bool pinned = true)
    {
        var rig = new Rig();
        rig.H = await LaneHost.StartAsync(() =>
        [
            new IdentityComponent(new IdentityOptions { BrokerKeys = pinned ? [MockBroker.Key] : [] }),
            new SetupComponent(new SetupOptions { BrokerCheck = (broker, _) => { Interlocked.Increment(ref rig.Checks); Assert.Equal(MockBroker.Origin, broker); return Task.FromResult(rig.BrokerProblem); } }),
        ], (h, _) => h.Data.WriteJson("identity.json", identity ?? new IdentitySettings { Discord = new DiscordSettings { Broker = MockBroker.Origin } }));
        return rig;
    }

    private static async Task<JsonNode> Json(HttpResponseMessage r) => (await LaneHost.Browser.JsonAsync(r))!;

    private static async Task<string> Code(HttpResponseMessage r) => (await Json(r))["error"]!["code"]!.GetValue<string>();

    private static async Task<LaneHost.Browser> OwnerAsync(LaneHost h, bool reauth = true)
    {
        if (!h.Auth.Accounts.All.Any(a => a.Name == "boss")) await h.AccountAsync("boss", BuiltinRoles.Owner);
        var b = h.NewBrowser();
        await b.SignInAsync("boss");
        if (reauth) await b.ReauthAsync();
        return b;
    }

    /// <summary>The wizard's link: start (back "setup"), the broker's answer (or an error), back to /setup with the state.</summary>
    private static async Task<string> LinkFromWizard(LaneHost.Browser b, string guild, string sub = Owners, string? brokerError = null)
    {
        var r = await b.PostAsync("/api/identity/start", new { provider = "discord", intent = "link", back = "setup" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var url = (await Json(r))["url"]!.GetValue<string>();
        Assert.Contains("guild=" + guild, url);
        var (ret, payload) = MockBroker.For(url, sub: sub, guild: guild);
        var cb = await b.Http.GetAsync(ret + (brokerError is null ? "&assertion=" + MockBroker.Sign(payload) : "&error=" + brokerError));
        Assert.Equal(HttpStatusCode.SeeOther, cb.StatusCode);
        var loc = cb.Headers.Location!.OriginalString;
        Assert.StartsWith("/setup#ext=", loc);
        return loc["/setup#ext=".Length..];
    }

    private static Task<HttpResponseMessage> Complete(LaneHost.Browser b, string state) => b.PostAsync("/api/identity/complete", new { state });

    [Fact]
    public async Task The_owner_links_discord_from_the_wizard_and_signs_in_with_it_the_password_stays()
    {
        await using var rig = await StartAsync();
        var h = rig.H;
        using var boss = await OwnerAsync(h);
        var state = await Json(await boss.GetAsync("/api/setup/state"));
        Assert.False(state["discord"]!["enabled"]!.GetValue<bool>());
        Assert.False(state["discord"]!["linked"]!.GetValue<bool>());

        var r = await boss.PostAsync("/api/setup/discord/me", new { });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(1, rig.Checks);
        Assert.Equal(Kiln, (await Json(r))["guildId"]!.GetValue<string>());
        Assert.True(rig.Identity.Current.Discord is { Enabled: true, OwnerOnly: true, GuildId: Kiln });
        state = await Json(await boss.GetAsync("/api/setup/state"));
        Assert.True(state["discord"]!["ownerOnly"]!.GetValue<bool>());
        Assert.True(state["discord"]!["kilnGuild"]!.GetValue<bool>());

        var st = await LinkFromWizard(boss, Kiln);
        var done = await Complete(boss, st);
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        Assert.Equal(Owners, h.Auth.Accounts.All.Single(a => a.Name == "boss").Links["discord"]);
        Assert.True((await Json(await boss.GetAsync("/api/setup/state")))["discord"]!["linked"]!.GetValue<bool>());

        // sign in with Discord now; the password still works too
        using var fresh = h.NewBrowser();
        var s = await fresh.PostAsync("/api/identity/start", new { provider = "discord", intent = "login" });
        var (ret, payload) = MockBroker.For((await Json(s))["url"]!.GetValue<string>(), sub: Owners, guild: Kiln);
        var cb = await fresh.Http.GetAsync(ret + "&assertion=" + MockBroker.Sign(payload));
        Assert.StartsWith("/signin#ext=", cb.Headers.Location!.OriginalString);
        var login = await Complete(fresh, cb.Headers.Location!.OriginalString.Split("#ext=")[1]);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var me = (await Json(login))["user"]!;
        Assert.Equal("boss", me["name"]!.GetValue<string>());
        Assert.True(me["owner"]!.GetValue<bool>());
        using var withPassword = h.NewBrowser();
        await withPassword.SignInAsync("boss");
    }

    [Fact]
    public async Task When_the_broker_does_not_answer_nothing_changes_and_the_owner_can_still_go_on()
    {
        await using var rig = await StartAsync();
        rig.BrokerProblem = "https://broker.test/kiln/auth/discord/health did not answer within 8 seconds";
        using var boss = await OwnerAsync(rig.H);
        var r = await boss.PostAsync("/api/setup/discord/me", new { });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
        Assert.Equal("broker_unreachable", await Code(r));
        Assert.Contains("did not answer", (await Json(r))["error"]!["message"]!.GetValue<string>());
        Assert.False(rig.Identity.Current.Discord.Enabled);
        // "Try Discord anyway": no check, switched on
        r = await boss.PostAsync("/api/setup/discord/me", new { anyway = true });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(1, rig.Checks);
        Assert.True(rig.Identity.Current.Discord.Enabled);
    }

    [Fact]
    public async Task A_server_the_owner_named_is_kept_and_discord_already_open_to_everyone_stays_so()
    {
        await using (var rig = await StartAsync(new IdentitySettings { Discord = new DiscordSettings { Broker = MockBroker.Origin, GuildId = Mine } }))
        {
            using var boss = await OwnerAsync(rig.H);
            Assert.Equal(HttpStatusCode.OK, (await boss.PostAsync("/api/setup/discord/me", new { })).StatusCode);
            Assert.True(rig.Identity.Current.Discord is { Enabled: true, OwnerOnly: true, GuildId: Mine });
            Assert.False((await Json(await boss.GetAsync("/api/setup/state")))["discord"]!["kilnGuild"]!.GetValue<bool>());
        }
        await using (var rig = await StartAsync(new IdentitySettings { Discord = new DiscordSettings { Enabled = true, Broker = MockBroker.Origin, GuildId = Mine } }))
        {
            using var boss = await OwnerAsync(rig.H);
            Assert.Equal(HttpStatusCode.OK, (await boss.PostAsync("/api/setup/discord/me", new { })).StatusCode);
            Assert.True(rig.Identity.Current.Discord is { Enabled: true, OwnerOnly: false, GuildId: Mine });
        }
    }

    [Fact]
    public async Task Only_the_owner_with_a_recent_proof_and_only_a_build_with_the_brokers_key()
    {
        await using (var rig = await StartAsync())
        {
            await rig.H.AccountAsync("adam", BuiltinRoles.Admin);
            using var adam = rig.H.NewBrowser();
            await adam.SignInAsync("adam");
            await adam.ReauthAsync();
            Assert.Equal(HttpStatusCode.Forbidden, (await adam.PostAsync("/api/setup/discord/me", new { })).StatusCode);
            using var boss = await OwnerAsync(rig.H, reauth: false);
            var r = await boss.PostAsync("/api/setup/discord/me", new { });
            Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
            Assert.Equal("reauth_required", await Code(r));
            // the wizard's return is the owner's: an admin's link cannot ask for it
            r = await adam.PostAsync("/api/identity/start", new { provider = "discord", intent = "link", back = "setup" });
            Assert.Equal(HttpStatusCode.NotFound, r.StatusCode); // Discord is still off here
            Assert.Equal(0, rig.Checks);
            Assert.False(rig.Identity.Current.Discord.Enabled);
        }
        await using (var rig = await StartAsync(pinned: false))
        {
            using var boss = await OwnerAsync(rig.H);
            Assert.Equal(HttpStatusCode.Conflict, (await boss.PostAsync("/api/setup/discord/me", new { })).StatusCode);
            Assert.Equal(0, rig.Checks);
            Assert.False(rig.Identity.Current.Discord.Enabled);
        }
    }

    [Fact]
    public async Task Back_names_only_the_wizard_and_only_for_the_owners_own_link()
    {
        await using var rig = await StartAsync(new IdentitySettings { Discord = new DiscordSettings { Enabled = true, Broker = MockBroker.Origin, GuildId = Mine } });
        using var boss = await OwnerAsync(rig.H);
        foreach (var bad in new object[]
        {
            new { provider = "discord", intent = "login", back = "setup" },
            new { provider = "discord", intent = "link", back = "https://evil.example/" },
            new { provider = "discord", intent = "link", back = "/setup" },
        })
        {
            var r = await boss.PostAsync("/api/identity/start", bad);
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }
        await rig.H.AccountAsync("vera");
        using var vera = rig.H.NewBrowser();
        await vera.SignInAsync("vera");
        await vera.ReauthAsync();
        var v = await vera.PostAsync("/api/identity/start", new { provider = "discord", intent = "link", back = "setup" });
        Assert.Equal(HttpStatusCode.Forbidden, v.StatusCode);
        // without back: My account, as before
        v = await vera.PostAsync("/api/identity/start", new { provider = "discord", intent = "link" });
        var (ret, payload) = MockBroker.For((await Json(v))["url"]!.GetValue<string>(), guild: Mine);
        Assert.StartsWith("/account#ext=", (await vera.Http.GetAsync(ret + "&assertion=" + MockBroker.Sign(payload))).Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task A_canceled_link_a_discord_account_linked_to_someone_else_and_a_not_member_answer_from_the_wizard()
    {
        await using var rig = await StartAsync();
        var h = rig.H;
        var other = await h.AccountAsync("olga");
        h.Auth.Accounts.Update(other.Id, x => x with { Links = new Dictionary<string, string> { ["discord"] = Owners } });
        using var boss = await OwnerAsync(h);
        Assert.Equal(HttpStatusCode.OK, (await boss.PostAsync("/api/setup/discord/me", new { })).StatusCode);

        var canceled = await Complete(boss, await LinkFromWizard(boss, Kiln, brokerError: "access_denied"));
        Assert.Equal(HttpStatusCode.BadRequest, canceled.StatusCode);
        Assert.Equal("ext_canceled", await Code(canceled));

        var notMember = await Complete(boss, await LinkFromWizard(boss, Kiln, brokerError: "not_member"));
        Assert.Equal(HttpStatusCode.Forbidden, notMember.StatusCode);
        Assert.Equal("not_member", await Code(notMember));

        var taken = await Complete(boss, await LinkFromWizard(boss, Kiln, sub: Owners));
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal("linked_elsewhere", await Code(taken));
        Assert.False(h.Auth.Accounts.All.Single(a => a.Name == "boss").Links.ContainsKey("discord"));
        Assert.Equal(Owners, h.Auth.Accounts.Find(other.Id)!.Links["discord"]);

        // a state from the wizard completes only in the browser that started it
        var st = await LinkFromWizard(boss, Kiln, sub: "333333333333333333");
        using var thief = await OwnerAsync(h);
        Assert.Equal(HttpStatusCode.BadRequest, (await Complete(thief, st)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Complete(boss, st)).StatusCode);
    }

    [Fact]
    public async Task Discord_for_the_owner_alone_until_the_helpers_page_opens_it()
    {
        await using var rig = await StartAsync();
        var h = rig.H;
        using var boss = await OwnerAsync(h);
        Assert.Equal(HttpStatusCode.OK, (await boss.PostAsync("/api/setup/discord/me", new { })).StatusCode);
        await h.AccountAsync("vera");
        using var vera = h.NewBrowser();
        await vera.SignInAsync("vera");
        await vera.ReauthAsync();
        // My account does not offer it; starting a link is refused
        var links = await Json(await vera.GetAsync("/api/identity/links"));
        Assert.DoesNotContain(links["providers"]!.AsArray(), p => p!["id"]!.GetValue<string>() == "discord");
        Assert.Contains((await Json(await boss.GetAsync("/api/identity/links")))["providers"]!.AsArray(), p => p!["id"]!.GetValue<string>() == "discord");
        var r = await vera.PostAsync("/api/identity/start", new { provider = "discord", intent = "link" });
        Assert.Equal("owner_only", await Code(r));

        // the helpers' page: their own Discord server, for everyone who links
        Assert.Equal(HttpStatusCode.OK, (await boss.PostAsync("/api/setup/discord", new { guildId = Mine })).StatusCode);
        Assert.True(rig.Identity.Current.Discord is { Enabled: true, OwnerOnly: false, GuildId: Mine });
        r = await vera.PostAsync("/api/identity/start", new { provider = "discord", intent = "link" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var url = (await Json(r))["url"]!.GetValue<string>();

        // switched back to the owner alone while vera's link is at Discord: it is refused when it comes back
        var s = h.Host.Services.Get<SettingsStore<IdentitySettings>>();
        var next = System.Text.Json.JsonSerializer.Deserialize<IdentitySettings>(System.Text.Json.JsonSerializer.Serialize(s.Current, Kilnwarden.Storage.DataStore.Json), Kilnwarden.Storage.DataStore.Json)!;
        next.Discord.OwnerOnly = true;
        s.Save(next);
        var (ret, payload) = MockBroker.For(url, sub: "444444444444444444", guild: Mine);
        var st = (await vera.Http.GetAsync(ret + "&assertion=" + MockBroker.Sign(payload))).Headers.Location!.OriginalString.Split("#ext=")[1];
        var done = await Complete(vera, st);
        Assert.Equal(HttpStatusCode.Forbidden, done.StatusCode);
        Assert.Equal("owner_only", await Code(done));
        Assert.False(h.Auth.Accounts.All.Single(a => a.Name == "vera").Links.ContainsKey("discord"));
    }

    /// <summary>Review of 0.1.3: owners only keeps Discord from everyone else entirely, its role mapping too. A helper's
    /// panel role mapped from their Discord roles while Discord was open (recorded at their last Discord sign-in) went away
    /// when Discord sign-in was switched off; the wizard's "Sign in with Discord" (on for the owner alone) must not give it
    /// back, since that helper can no longer sign in with Discord for it to be checked again.</summary>
    [Fact]
    public async Task Owners_only_keeps_discord_mapped_roles_from_everyone_else_too()
    {
        const string mod = "333333333333333333";
        await using var rig = await StartAsync(new IdentitySettings
        {
            Discord = new DiscordSettings { Broker = MockBroker.Origin, GuildId = Mine, RoleMap = [new RoleMapping { From = mod, Role = BuiltinRoles.Moderator }] },
        });
        var h = rig.H;
        var vera = await h.AccountAsync("vera");
        h.Auth.Accounts.Update(vera.Id, x => x with
        {
            Links = new Dictionary<string, string> { ["discord"] = "444444444444444444" },
            Synced = new Dictionary<string, IReadOnlyList<string>> { ["discord"] = [BuiltinRoles.Moderator] },
        });
        using var v = h.NewBrowser();
        await v.SignInAsync("vera");
        async Task<List<string>> Roles() => [.. (await Json(await v.GetAsync("/api/auth/me")))["roles"]!.AsArray().Select(x => x!.GetValue<string>())];
        Assert.DoesNotContain(BuiltinRoles.Moderator, await Roles());

        using var boss = await OwnerAsync(h);
        Assert.Equal(HttpStatusCode.OK, (await boss.PostAsync("/api/setup/discord/me", new { })).StatusCode);
        Assert.True(rig.Identity.Current.Discord is { Enabled: true, OwnerOnly: true, GuildId: Mine });
        Assert.DoesNotContain(BuiltinRoles.Moderator, await Roles());
        // My account says her link is switched off (it is, for her)
        var link = (await Json(await v.GetAsync("/api/identity/links")))["links"]!.AsArray().Single()!;
        Assert.Equal("discord", link["provider"]!.GetValue<string>());
        Assert.False(link["enabled"]!.GetValue<bool>());
        // her own sign-in with Discord says why it is refused
        using var fresh = h.NewBrowser();
        var s = await fresh.PostAsync("/api/identity/start", new { provider = "discord", intent = "login" });
        var (ret, payload) = MockBroker.For((await Json(s))["url"]!.GetValue<string>(), sub: "444444444444444444", guild: Mine, roles: [mod]);
        var st = (await fresh.Http.GetAsync(ret + "&assertion=" + MockBroker.Sign(payload))).Headers.Location!.OriginalString.Split("#ext=")[1];
        var refused = await Complete(fresh, st);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("owner_only", await Code(refused));

        // open to everyone again (the helpers' page): the mapping applies again, as before 0.1.3
        Assert.Equal(HttpStatusCode.OK, (await boss.PostAsync("/api/setup/discord", new { guildId = Mine })).StatusCode);
        Assert.Contains(BuiltinRoles.Moderator, await Roles());
        Assert.True((await Json(await v.GetAsync("/api/identity/links")))["links"]!.AsArray().Single()!["enabled"]!.GetValue<bool>());
    }

    [Fact]
    public async Task The_broker_check_wants_its_health_page_to_answer()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var status = 200;
        string? path = null;
        _ = Task.Run(async () =>
        {
            while (true)
            {
                using var c = await listener.AcceptTcpClientAsync();
                using var s = c.GetStream();
                var buf = new byte[4096];
                var n = await s.ReadAsync(buf);
                path = Encoding.ASCII.GetString(buf, 0, n).Split(' ')[1];
                var body = "{\"ok\":true}";
                await s.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {status} X\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}"));
            }
        });
        Assert.Null(await SetupComponent.CheckBrokerAsync($"http://127.0.0.1:{port}", CancellationToken.None));
        Assert.Equal("/kiln/auth/discord/health", path);
        status = 503;
        Assert.Contains("answered 503", await SetupComponent.CheckBrokerAsync($"http://127.0.0.1:{port}/", CancellationToken.None));
        listener.Stop();
        Assert.NotNull(await SetupComponent.CheckBrokerAsync($"http://127.0.0.1:{LaneHost.FreePort()}", CancellationToken.None));
    }
}
