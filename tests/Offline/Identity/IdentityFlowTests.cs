// SPDX-License-Identifier: MIT
using System.Buffers.Text;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Identity;
using Kilnwarden.Offline.Support;

namespace Kilnwarden.Offline.Identity;

/// <summary>A fake steamcommunity.com check_authentication endpoint.</summary>
internal sealed class FakeSteam : HttpMessageHandler
{
    public bool Valid = true;
    public int Calls;
    public List<Dictionary<string, string>> Forms = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Interlocked.Increment(ref Calls);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(SteamOpenId.Endpoint, request.RequestUri!.AbsoluteUri);
        var form = Kilnwarden.Http.QueryString.Parse(await request.Content!.ReadAsStringAsync(ct)).ToDictionary();
        lock (Forms) Forms.Add(form);
        var ok = Valid && form.GetValueOrDefault("openid.mode") == "check_authentication";
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"ns:{SteamOpenId.Ns}\nis_valid:{(ok ? "true" : "false")}\n") };
    }
}

/// <summary>The frostdev.io broker's side, for tests: signs assertions with a test key.</summary>
internal sealed class MockBroker
{
    public static readonly byte[] Seed = Enumerable.Range(100, 32).Select(i => (byte)i).ToArray();
    public const string Origin = "https://broker.test";
    public const string Kid = "test-1";
    public static BrokerKey Key => new(Origin, Kid, Ed25519.PublicKey(Seed));

    public static string Sign(JsonObject payload, string kid = Kid, byte[]? seed = null, string alg = "EdDSA")
    {
        var h = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(new JsonObject { ["alg"] = alg, ["typ"] = DiscordBroker.Typ, ["kid"] = kid }.ToJsonString()));
        var p = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload.ToJsonString()));
        var sig = Ed25519.Sign(seed ?? Seed, Encoding.ASCII.GetBytes(h + "." + p));
        return h + "." + p + "." + Base64Url.EncodeToString(sig);
    }

    /// <summary>The assertion the broker would return for the panel's start URL.</summary>
    public static (string ReturnUrl, JsonObject Payload) For(string startUrl, string sub = "123456789012345678", string guild = "999999999999999999", params string[] roles)
    {
        var q = Kilnwarden.Http.QueryString.Parse(new Uri(startUrl).Query.TrimStart('?'));
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return (q["return"], new JsonObject
        {
            ["v"] = 1, ["iss"] = Origin, ["aud"] = DiscordBroker.Audience, ["panel"] = q["panel"], ["ret"] = q["return"], ["nonce"] = q["nonce"],
            ["iat"] = now, ["exp"] = now + 120, ["sub"] = sub, ["name"] = "tester", ["guild"] = guild,
            ["roles"] = new JsonArray([.. roles.Select(r => (JsonNode)r)]),
        });
    }
}

public sealed class IdentityFlowTests
{
    private const string SteamId = "76561198000000001";
    private const string Guild = "999999999999999999";
    private const string Allowed = "111111111111111111";

    private static async Task<(LaneHost H, FakeSteam Steam)> StartAsync(bool steam = true, bool discord = true, bool pinKey = true, string? allowed = Allowed)
    {
        var fake = new FakeSteam();
        var h = await LaneHost.StartAsync(() => [new IdentityComponent(new IdentityOptions
        {
            SteamHttp = new HttpMessageInvoker(fake), BrokerKeys = pinKey ? [MockBroker.Key] : [],
        })], (h, _) => h.Data.WriteJson("identity.json", new IdentitySettings
        {
            Steam = new SteamSettings { Enabled = steam },
            Discord = new DiscordSettings { Enabled = discord, Broker = MockBroker.Origin, GuildId = Guild, AllowedRoles = allowed is null ? [] : [allowed] },
        }));
        return (h, fake);
    }

    private static async Task<string> StartUrl(LaneHost.Browser b, string provider, string intent, HttpStatusCode expect = HttpStatusCode.OK)
    {
        var r = await b.PostAsync("/api/identity/start", new { provider, intent });
        Assert.Equal(expect, r.StatusCode);
        return expect == HttpStatusCode.OK ? (await LaneHost.Browser.JsonAsync(r))!["url"]!.GetValue<string>() : (await LaneHost.Browser.JsonAsync(r))!["error"]!["code"]!.GetValue<string>();
    }

    /// <summary>What Steam would send the browser back with.</summary>
    private static string SteamCallback(string startUrl, string steamId = SteamId, Action<Dictionary<string, string>>? tamper = null)
    {
        var q = Kilnwarden.Http.QueryString.Parse(new Uri(startUrl).Query.TrimStart('?'));
        var ret = q["openid.return_to"];
        var f = new Dictionary<string, string>
        {
            ["openid.ns"] = SteamOpenId.Ns, ["openid.mode"] = "id_res", ["openid.op_endpoint"] = SteamOpenId.Endpoint,
            ["openid.claimed_id"] = "https://steamcommunity.com/openid/id/" + steamId, ["openid.identity"] = "https://steamcommunity.com/openid/id/" + steamId,
            ["openid.return_to"] = ret, ["openid.response_nonce"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'") + "abc123",
            ["openid.assoc_handle"] = "1234567890", ["openid.signed"] = "signed,op_endpoint,claimed_id,identity,return_to,response_nonce,assoc_handle",
            ["openid.sig"] = "c2lnbmF0dXJl",
        };
        tamper?.Invoke(f);
        return ret + "&" + string.Join('&', f.Select(kv => Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value)));
    }

    /// <summary>Follows a provider callback; returns the state from the app URL it redirects to.</summary>
    private static async Task<string> Callback(LaneHost.Browser b, string url, string expectPage)
    {
        var r = await b.Http.GetAsync(url);
        Assert.Equal(HttpStatusCode.SeeOther, r.StatusCode);
        var loc = r.Headers.Location!.OriginalString;
        Assert.StartsWith(expectPage + "#ext=", loc);
        Assert.Equal("no-store", r.Headers.CacheControl?.ToString());
        return loc[(expectPage.Length + 5)..];
    }

    private static Task<HttpResponseMessage> Complete(LaneHost.Browser b, string state) => b.PostAsync("/api/identity/complete", new { state });

    private static async Task<string> Code(HttpResponseMessage r) => (await LaneHost.Browser.JsonAsync(r))!["error"]!["code"]!.GetValue<string>();

    [Fact]
    public async Task Providers_are_off_by_default()
    {
        await using var h = await LaneHost.StartAsync(() => [new IdentityComponent(new IdentityOptions { SteamHttp = new HttpMessageInvoker(new FakeSteam()) })]);
        using var b = h.NewBrowser();
        var status = await LaneHost.Browser.JsonAsync(await b.GetAsync("/api/auth/status"));
        Assert.Equal("[\"local\"]", status!["providers"]!.ToJsonString());
        Assert.Equal("unknown_provider", await StartUrl(b, "steam", "login", HttpStatusCode.NotFound));
        Assert.Equal("unknown_provider", await StartUrl(b, "discord", "login", HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task Discord_stays_off_without_a_pinned_broker_key()
    {
        var (h, _) = await StartAsync(pinKey: false);
        await using var _h = h;
        using var b = h.NewBrowser();
        Assert.Equal("unknown_provider", await StartUrl(b, "discord", "login", HttpStatusCode.NotFound));
        var ids = (await LaneHost.Browser.JsonAsync(await b.GetAsync("/api/auth/status")))!["providers"]!.AsArray().Select(p => p is JsonObject o ? o["id"]!.GetValue<string>() : p!.GetValue<string>());
        Assert.Equal(["local", "steam"], ids);
        Assert.True(h.Log.Has("no key of the broker"));
    }

    [Fact]
    public async Task Steam_login_needs_an_opt_in_link_and_grants_no_recent_proof()
    {
        var (h, steam) = await StartAsync();
        await using var _h = h;
        var a = await h.AccountAsync("alice");

        // not linked: no takeover, no account made
        using (var anon = h.NewBrowser())
        {
            var url = await StartUrl(anon, "steam", "login");
            Assert.StartsWith(SteamOpenId.Endpoint + "?", url);
            Assert.Contains("openid.realm=" + Uri.EscapeDataString(h.Origin + "/"), url);
            var state = await Callback(anon, SteamCallback(url), "/signin");
            var r = await Complete(anon, state);
            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
            Assert.Equal("not_linked", await Code(r));
            Assert.Single(h.Auth.Accounts.All);
        }

        // link: signed in, needs recent proof first
        using var alice = h.NewBrowser();
        await alice.SignInAsync("alice");
        Assert.Equal("reauth_required", await StartUrl(alice, "steam", "link", HttpStatusCode.Forbidden));
        await alice.ReauthAsync();
        var linkUrl = await StartUrl(alice, "steam", "link");
        var linkState = await Callback(alice, SteamCallback(linkUrl), "/account");
        var lr = await Complete(alice, linkState);
        Assert.Equal(HttpStatusCode.OK, lr.StatusCode);
        Assert.Equal(SteamId, h.Auth.Accounts.Find(a.Id)!.Links["steam"]);
        var links = await LaneHost.Browser.JsonAsync(await alice.GetAsync("/api/identity/links"));
        Assert.Equal(SteamId, links!["links"]![0]!["subject"]!.GetValue<string>());

        // sign in with it from another browser: a session for alice, via steam, no recent proof
        using var other = h.NewBrowser();
        var url2 = await StartUrl(other, "steam", "login");
        var st2 = await Callback(other, SteamCallback(url2), "/signin");
        var ok = await Complete(other, st2);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var me = await LaneHost.Browser.JsonAsync(await other.GetAsync("/api/auth/me"));
        Assert.Equal("alice", me!["name"]!.GetValue<string>());
        Assert.Equal("steam", me["session"]!["via"]!.GetValue<string>());
        Assert.Null(me["session"]!["reauthenticatedUntil"]);
        // Steam verified each assertion server-to-server with every openid field
        Assert.Equal(3, steam.Calls);
        Assert.All(steam.Forms, f => Assert.Equal("check_authentication", f["openid.mode"]));
        // and it is not a reauth credential
        var re = await other.PostAsync("/api/auth/reauth", new { secret = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, re.StatusCode);
    }

    [Fact]
    public async Task Intent_is_one_use_bound_to_its_browser_and_short_lived()
    {
        var (h, _) = await StartAsync();
        await using var _h = h;
        var a = await h.AccountAsync("bob");
        h.Auth.Accounts.Update(a.Id, x => x with { Links = new Dictionary<string, string> { ["steam"] = SteamId } });
        using var victim = h.NewBrowser();
        using var attacker = h.NewBrowser();
        var url = await StartUrl(attacker, "steam", "login");
        var state = await Callback(attacker, SteamCallback(url), "/signin");
        // another browser cannot redeem it (login CSRF): no binder cookie
        var r = await Complete(victim, state);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("ext_expired", await Code(r));
        Assert.Equal(HttpStatusCode.OK, (await Complete(attacker, state)).StatusCode);
        // once
        Assert.Equal(HttpStatusCode.BadRequest, (await Complete(attacker, state)).StatusCode);
        // an unknown state at the callback
        var cb = await attacker.Http.GetAsync("/api/identity/steam/callback?state=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
        Assert.Equal("/signin#ext-error=expired", cb.Headers.Location!.OriginalString);
        // not answered yet
        var url3 = await StartUrl(attacker, "steam", "login");
        var st3 = Kilnwarden.Http.QueryString.Parse(new Uri(Kilnwarden.Http.QueryString.Parse(new Uri(url3).Query.TrimStart('?'))["openid.return_to"]).Query.TrimStart('?'))["state"];
        Assert.Equal("ext_pending", await Code(await Complete(attacker, st3)));
    }

    /// <summary>Security review S1: a provider answer brought by another browser (a victim who followed the attacker's
    /// provider link) must not settle the attacker's intent: the callback needs the binder cookie of the browser that
    /// started it, and the binder cookie must travel with the provider's top-level redirect (SameSite=Lax).</summary>
    [Fact]
    public async Task A_steam_answer_from_another_browser_does_not_sign_in_the_starter()
    {
        var (h, _) = await StartAsync();
        await using var _h = h;
        var a = await h.AccountAsync("bob");
        h.Auth.Accounts.Update(a.Id, x => x with { Links = new Dictionary<string, string> { ["steam"] = SteamId } });
        using var victim = h.NewBrowser();
        using var attacker = h.NewBrowser();
        var start = await attacker.PostAsync("/api/identity/start", new { provider = "steam", intent = "login" });
        Assert.Contains(start.Headers.GetValues("Set-Cookie"), c => c.StartsWith("wa-ext=", StringComparison.Ordinal) && c.Contains("SameSite=Lax"));
        var url = (await LaneHost.Browser.JsonAsync(start))!["url"]!.GetValue<string>();
        var callback = SteamCallback(url);
        var state = Kilnwarden.Http.QueryString.Parse(new Uri(callback).Query.TrimStart('?'))["state"];
        // the victim's browser comes back from Steam: no binder, so nothing is verified or settled
        var r = await victim.Http.GetAsync(callback);
        Assert.Equal(HttpStatusCode.SeeOther, r.StatusCode);
        Assert.StartsWith("/signin#ext-error=", r.Headers.Location!.OriginalString);
        var done = await Complete(attacker, state);
        Assert.NotEqual(HttpStatusCode.OK, done.StatusCode);
        Assert.False(done.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.StartsWith("wa-session=", StringComparison.Ordinal) && !c.Contains("Max-Age=0")));
        var me = await attacker.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task A_discord_answer_from_another_browser_does_not_sign_in_the_starter()
    {
        var (h, _) = await StartAsync();
        await using var _h = h;
        var a = await h.AccountAsync("dora");
        h.Auth.Accounts.Update(a.Id, x => x with { Links = new Dictionary<string, string> { ["discord"] = "222222222222222222" } });
        using var victim = h.NewBrowser();
        using var attacker = h.NewBrowser();
        var url = await StartUrl(attacker, "discord", "login");
        var (ret, payload) = MockBroker.For(url, roles: [Allowed], sub: "222222222222222222");
        var r = await victim.Http.GetAsync(ret + "&assertion=" + MockBroker.Sign(payload));
        Assert.StartsWith("/signin#ext-error=", r.Headers.Location!.OriginalString);
        var state = Kilnwarden.Http.QueryString.Parse(new Uri(ret).Query.TrimStart('?'))["state"];
        Assert.NotEqual(HttpStatusCode.OK, (await Complete(attacker, state)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await attacker.GetAsync("/api/auth/me")).StatusCode);
    }

    /// <summary>Security review S7: one identity is linked to one account. Linking is atomic (two links racing for the
    /// same identity: one wins), and a store that somehow holds it twice refuses the sign-in instead of picking one.</summary>
    [Fact]
    public async Task One_identity_links_to_one_account_and_an_ambiguous_link_signs_no_one_in()
    {
        var (h, _) = await StartAsync();
        await using var _h = h;
        var a = await h.AccountAsync("amy");
        var b = await h.AccountAsync("ben");
        var wins = 0;
        var refused = 0;
        Parallel.For(0, 16, i =>
        {
            try
            {
                h.Auth.Accounts.LinkIfFree(i % 2 == 0 ? a.Id : b.Id, "steam", SteamId);
                Interlocked.Increment(ref wins);
            }
            catch (InvalidOperationException)
            {
                Interlocked.Increment(ref refused);
            }
        });
        Assert.Equal(1, h.Auth.Accounts.All.Count(x => x.Links.TryGetValue("steam", out var s) && s == SteamId));
        Assert.True(wins >= 1 && refused >= 1);
        // a store that holds it twice (hand-edited): no sign-in for either
        var other = h.Auth.Accounts.All.First(x => !x.Links.ContainsKey("steam"));
        h.Auth.Accounts.Update(other.Id, x => x with { Links = new Dictionary<string, string> { ["steam"] = SteamId } });
        var r = h.Auth.SignInExternal(new ExternalIdentity("steam", SteamId, null), new ClientInfo(System.Net.IPAddress.Loopback, "t"), null);
        var refusal = Assert.IsType<SignInResult.Refused>(r);
        Assert.Equal("linked_twice", refusal.Code);
    }

    /// <summary>DESIGN §3: Discord guild roles map to panel roles, opt-in, for accounts that linked their Discord. A
    /// mapped role is granted at sign-in, kept apart from the roles an admin set, and gone at once when the mapping goes;
    /// the owner role can never be mapped.</summary>
    [Fact]
    public async Task Discord_roles_map_to_panel_roles_when_the_owner_says_so()
    {
        var (h, _) = await StartAsync();
        await using var _h = h;
        var a = await h.AccountAsync("erin");
        h.Auth.Accounts.Update(a.Id, x => x with { Links = new Dictionary<string, string> { ["discord"] = "123456789012345678" } });
        var settings = new IdentitySettings
        {
            Steam = new SteamSettings { Enabled = true },
            Discord = new DiscordSettings { Enabled = true, Broker = MockBroker.Origin, GuildId = Guild, AllowedRoles = [Allowed], RoleMap = [new RoleMapping { From = Allowed, Role = "moderator" }] },
        };
        Assert.Contains(IdentitySettings.Validate(new IdentitySettings { Discord = new DiscordSettings { RoleMap = [new RoleMapping { From = Allowed, Role = "owner" }] } }),
            p => p.Contains("owner"));
        h.Data.WriteJson("identity.json", settings);
        await Eventually.True(() => h.Host.Services.Get<IdentityService>().Settings.Discord.RoleMap.Count == 1, 8000, "identity.json reloaded");
        using var b = h.NewBrowser();
        var url = await StartUrl(b, "discord", "login");
        var (ret, p) = MockBroker.For(url, roles: [Allowed]);
        var st = await Callback(b, ret + "&assertion=" + MockBroker.Sign(p), "/signin");
        var r = await Complete(b, st);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var me = (await LaneHost.Browser.JsonAsync(await b.GetAsync("/api/auth/me")))!;
        Assert.Contains("moderator", me["roles"]!.AsArray().Select(x => x!.GetValue<string>()));
        var acct = h.Auth.Accounts.Find(a.Id)!;
        Assert.Equal(["viewer"], acct.Roles); // the admin-set roles are unchanged
        Assert.Equal(["moderator"], acct.Synced["discord"]);
        // the owner drops the mapping: the grant is gone at once (the session's roles follow)
        h.Data.WriteJson("identity.json", new IdentitySettings { Steam = settings.Steam, Discord = new DiscordSettings { Enabled = true, Broker = MockBroker.Origin, GuildId = Guild, AllowedRoles = [Allowed] } });
        await Eventually.True(() => h.Host.Services.Get<IdentityService>().Settings.Discord.RoleMap.Count == 0, 8000, "identity.json reloaded");
        me = (await LaneHost.Browser.JsonAsync(await b.GetAsync("/api/auth/me")))!;
        Assert.DoesNotContain("moderator", me["roles"]!.AsArray().Select(x => x!.GetValue<string>()));
    }

    public static TheoryData<string> SteamTampers => ["return_to", "endpoint", "claimed", "identity", "nonce_old", "unsigned", "steam_says_no", "cancel"];

    [Theory]
    [MemberData(nameof(SteamTampers))]
    public async Task Bad_steam_answers_are_refused(string how)
    {
        var (h, steam) = await StartAsync();
        await using var _h = h;
        var a = await h.AccountAsync("carol");
        h.Auth.Accounts.Update(a.Id, x => x with { Links = new Dictionary<string, string> { ["steam"] = SteamId } });
        using var b = h.NewBrowser();
        var url = await StartUrl(b, "steam", "login");
        if (how == "steam_says_no") steam.Valid = false;
        var cb = SteamCallback(url, tamper: f =>
        {
            switch (how)
            {
                case "return_to": f["openid.return_to"] += "x"; break;
                case "endpoint": f["openid.op_endpoint"] = "https://evil.example/openid/login"; break;
                case "claimed": f["openid.claimed_id"] = f["openid.identity"] = "https://evil.example/openid/id/" + SteamId; break;
                case "identity": f["openid.identity"] = "https://steamcommunity.com/openid/id/76561198000000002"; break;
                case "nonce_old": f["openid.response_nonce"] = DateTimeOffset.UtcNow.AddMinutes(-30).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'") + "x"; break;
                case "unsigned": f["openid.signed"] = "op_endpoint,claimed_id,identity"; break;
                case "cancel": f["openid.mode"] = "cancel"; break;
            }
        });
        var state = await Callback(b, cb, "/signin");
        var r = await Complete(b, state);
        Assert.Equal(how == "cancel" ? HttpStatusCode.BadRequest : HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Equal(how == "cancel" ? "ext_canceled" : "ext_failed", await Code(r));
        Assert.False((await LaneHost.Browser.JsonAsync(await b.GetAsync("/api/auth/status")))!["signedIn"]!.GetValue<bool>());
        // only the last step asks Steam
        Assert.Equal(how == "steam_says_no" ? 1 : 0, steam.Calls);
    }

    [Fact]
    public async Task Discord_link_and_login_through_the_broker()
    {
        var (h, _) = await StartAsync();
        await using var _h = h;
        var a = await h.AccountAsync("dave");
        using var dave = h.NewBrowser();
        await dave.SignInAsync("dave");
        await dave.ReauthAsync();
        var url = await StartUrl(dave, "discord", "link");
        Assert.StartsWith(MockBroker.Origin + "/kiln/auth/discord?v=1&panel=", url);
        Assert.Contains("guild=" + Guild, url);
        var (ret, payload) = MockBroker.For(url, roles: [Allowed]);
        var st = await Callback(dave, ret + "&assertion=" + MockBroker.Sign(payload), "/account");
        Assert.Equal(HttpStatusCode.OK, (await Complete(dave, st)).StatusCode);
        Assert.Equal("123456789012345678", h.Auth.Accounts.Find(a.Id)!.Links["discord"]);

        using var fresh = h.NewBrowser();
        var url2 = await StartUrl(fresh, "discord", "login");
        var (ret2, p2) = MockBroker.For(url2, roles: ["222", Allowed]);
        p2["roles"] = new JsonArray("22222222222222222", Allowed);
        var st2 = await Callback(fresh, ret2 + "&assertion=" + MockBroker.Sign(p2), "/signin");
        var r = await Complete(fresh, st2);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var me = (await LaneHost.Browser.JsonAsync(r))!["user"]!;
        Assert.Equal("dave", me["name"]!.GetValue<string>());
        Assert.Equal("discord", me["session"]!["via"]!.GetValue<string>());
        Assert.Null(me["session"]!["reauthenticatedUntil"]);
        // roles are a gate, never a grant: still a viewer
        Assert.Equal("[\"viewer\"]", me["roles"]!.ToJsonString());
        Assert.False(me["owner"]!.GetValue<bool>());
    }

    public static TheoryData<string> DiscordTampers =>
        ["panel", "ret", "nonce", "expired", "too_long", "future", "guild", "role", "token", "kid", "key", "alg", "iss", "aud", "garbage", "broker_error"];

    [Theory]
    [MemberData(nameof(DiscordTampers))]
    public async Task Bad_broker_assertions_are_refused(string how)
    {
        var (h, _) = await StartAsync();
        await using var _h = h;
        var a = await h.AccountAsync("erin");
        h.Auth.Accounts.Update(a.Id, x => x with { Links = new Dictionary<string, string> { ["discord"] = "123456789012345678" } });
        using var b = h.NewBrowser();
        var url = await StartUrl(b, "discord", "login");
        var (ret, p) = MockBroker.For(url, roles: [Allowed]);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string kid = MockBroker.Kid, alg = "EdDSA";
        byte[]? seed = null;
        switch (how)
        {
            case "panel": p["panel"] = "00000000000000000000000000000000"; break;
            case "ret": p["ret"] = h.Origin + "/elsewhere"; break;
            case "nonce": p["nonce"] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"; break;
            case "expired": p["iat"] = now - 400; p["exp"] = now - 100; break;
            case "too_long": p["exp"] = now + 3600; break;
            case "future": p["iat"] = now + 600; p["exp"] = now + 700; break;
            case "guild": p["guild"] = "888888888888888888"; break;
            case "role": p["roles"] = new JsonArray("33333333333333333"); break;
            case "token": p["access_token"] = "abc"; break;
            case "kid": kid = "other"; break;
            case "key": seed = new byte[32]; break;
            case "alg": alg = "none"; break;
            case "iss": p["iss"] = "https://evil.example"; break;
            case "aud": p["aud"] = "something-else"; break;
        }
        var assertion = how == "garbage" ? "a.b.c" : MockBroker.Sign(p, kid, seed, alg);
        var cb = how == "broker_error" ? ret + "&error=access_denied" : ret + "&assertion=" + assertion;
        var st = await Callback(b, cb, "/signin");
        var r = await Complete(b, st);
        var code = await Code(r);
        Assert.Equal(how switch { "role" => "not_allowed", "broker_error" => "ext_canceled", _ => "ext_failed" }, code);
        Assert.False((await LaneHost.Browser.JsonAsync(await b.GetAsync("/api/auth/status")))!["signedIn"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Linking_is_one_account_per_identity_and_unlinking_needs_recent_proof()
    {
        var (h, _) = await StartAsync();
        await using var _h = h;
        var a = await h.AccountAsync("frank");
        await h.AccountAsync("gina");
        h.Auth.Accounts.Update(a.Id, x => x with { Links = new Dictionary<string, string> { ["steam"] = SteamId } });
        using var gina = h.NewBrowser();
        await gina.SignInAsync("gina");
        await gina.ReauthAsync();
        var url = await StartUrl(gina, "steam", "link");
        var st = await Callback(gina, SteamCallback(url), "/account");
        var r = await Complete(gina, st);
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Equal("linked_elsewhere", await Code(r));

        using var frank = h.NewBrowser();
        await frank.SignInAsync("frank");
        var d = await frank.DeleteAsync("/api/identity/links/steam");
        Assert.Equal(HttpStatusCode.Forbidden, d.StatusCode);
        Assert.Equal("reauth_required", await Code(d));
        await frank.ReauthAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await frank.DeleteAsync("/api/identity/links/steam")).StatusCode);
        Assert.Empty(h.Auth.Accounts.Find(a.Id)!.Links);
    }

    [Fact]
    public async Task A_link_completes_only_in_the_session_that_started_it()
    {
        var (h, _) = await StartAsync();
        await using var _h = h;
        var a = await h.AccountAsync("hana");
        using var b = h.NewBrowser();
        await b.SignInAsync("hana");
        await b.ReauthAsync();
        var url = await StartUrl(b, "steam", "link");
        var st = await Callback(b, SteamCallback(url), "/account");
        // signs out and back in (a new session) before completing
        await b.PostAsync("/api/auth/logout");
        await b.SignInAsync("hana");
        var r = await Complete(b, st);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Empty(h.Auth.Accounts.Find(a.Id)!.Links);
    }

    [Fact]
    public async Task External_sign_in_keeps_a_two_factor_requirement()
    {
        var (h, _) = await StartAsync();
        await using var _h = h;
        var a = await h.AccountAsync("ivan", BuiltinRoles.Admin);
        var admin = h.Auth.Roles.Find(BuiltinRoles.Admin)!;
        h.Auth.Roles.Put(admin with { Require2fa = true });
        h.Auth.Accounts.Update(a.Id, x => x with { Links = new Dictionary<string, string> { ["steam"] = SteamId } });
        using var b = h.NewBrowser();
        var url = await StartUrl(b, "steam", "login");
        var st = await Callback(b, SteamCallback(url), "/signin");
        var r = await Complete(b, st);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var me = (await LaneHost.Browser.JsonAsync(r))!["user"]!;
        Assert.NotNull(me["restriction"]);
        // a restricted session cannot reach the admin routes
        Assert.Equal(HttpStatusCode.Forbidden, (await b.GetAsync("/api/users/")).StatusCode);
    }
}
