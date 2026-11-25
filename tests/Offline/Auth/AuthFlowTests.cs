// SPDX-License-Identifier: MIT
using System.Net.Http.Json;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Kilnwarden.Auth;
using Kilnwarden.Offline.Support;

namespace Kilnwarden.Offline.Auth;

/// <summary>The auth component in the real host behind the real HTTP server (a browser's view).</summary>
public class AuthFlowTests
{
    private static string SetupToken(TestLog log) =>
        Regex.Match(string.Join("\n", log.Lines), @"setup#token=([A-Za-z0-9_-]{43})").Groups[1].Value;

    private static async Task<string> CodeAsync(HttpResponseMessage r) => (await JsonNode.ParseAsync(await r.Content.ReadAsStreamAsync()))?["error"]?["code"]?.GetValue<string>() ?? "";

    [Fact]
    public async Task First_run_setup_link_creates_the_owner_once()
    {
        await using var h = await AuthHarness.StartAsync(kap: false);
        Assert.False(h.Auth.OwnerExists);
        var token = SetupToken(h.Log);
        Assert.Equal(43, token.Length);
        // once the listener is up kiln.log has the whole link, and the audit log never the token
        await Eventually.True(() => h.Log.Has($"http://127.0.0.1:{h.Port}/setup#token={token}"), 5000, "the full setup link");
        await Eventually.True(() => h.Auth.Audit.Read(10).Any(e => e.GetProperty("op").GetString() == "auth.setup_link"));
        Assert.DoesNotContain(token, string.Join("\n", h.Auth.Audit.Read(100).Select(e => e.GetRawText())));
        using var b = h.NewClient();
        var status = await b.JsonAsync(await b.GetAsync("/api/auth/status"));
        Assert.False(status!["ownerExists"]!.GetValue<bool>());
        var bad = await b.PostAsync("/api/auth/setup", new { token = new string('A', 43), name = "owner", password = AuthHarness.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        var weak = await b.PostAsync("/api/auth/setup", new { token, name = "owner", password = "short" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, weak.StatusCode);
        var ok = await b.PostAsync("/api/auth/setup", new { token, name = "owner", password = AuthHarness.Password });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.NotNull(b.Session);
        Assert.True(h.Auth.OwnerExists);
        var me = await b.JsonAsync(await b.GetAsync("/api/auth/me"));
        Assert.True(me!["owner"]!.GetValue<bool>());
        Assert.Equal("console", me["commandLevel"]!.GetValue<string>());
        using var c = h.NewClient();
        var again = await c.PostAsync("/api/auth/setup", new { token, name = "owner2", password = AuthHarness.Password });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.True(h.Log.Has("owner account 'owner' created"));
    }

    [Fact]
    public async Task Wrong_names_and_wrong_passwords_look_the_same()
    {
        await using var h = await AuthHarness.StartAsync(kap: false);
        await h.AccountAsync("alice", "admin");
        await h.AccountAsync("dis", "viewer");
        h.Auth.Accounts.Update(h.Auth.Accounts.FindByName("dis")!.Id, a => a with { Disabled = true });
        using var b = h.NewClient();
        var r1 = await b.SignInAsync("alice", "wrong password!");
        var r2 = await b.SignInAsync("nobody", "wrong password!");
        var r3 = await b.SignInAsync("dis", AuthHarness.Password);
        var bodies = new[] { await r1.Content.ReadAsStringAsync(), await r2.Content.ReadAsStringAsync(), await r3.Content.ReadAsStringAsync() };
        Assert.All(new[] { r1, r2, r3 }, r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
        Assert.Single(bodies.Distinct());
        Assert.Null(b.Session);
        var good = await b.SignInAsync("ALICE");
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
        var cookie = good.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("wa-session="));
        Assert.Contains("HttpOnly", cookie);
        Assert.Contains("SameSite=Strict", cookie);
        Assert.Contains("Path=/", cookie);
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await b.PostAsync("/api/auth/logout")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Lockouts_hit_real_and_unknown_names_alike()
    {
        await using var h = await AuthHarness.StartAsync(kap: false);
        await h.AccountAsync("alice", "admin");
        using var b = h.NewClient();
        foreach (var name in new[] { "alice", "ghost" })
        {
            for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await b.SignInAsync(name, "nope nope nope")).StatusCode);
            var locked = await b.SignInAsync(name);
            Assert.Equal((HttpStatusCode)429, locked.StatusCode);
            Assert.True(locked.Headers.RetryAfter is not null);
        }
        Assert.Null(b.Session);
    }

    [Fact]
    public async Task State_changing_requests_need_the_panels_origin()
    {
        await using var h = await AuthHarness.StartAsync(kap: false);
        await h.AccountAsync("alice", "admin");
        using var http = new HttpClient { BaseAddress = new Uri(h.Origin) };
        var body = AuthHarness.Client.Json(new { name = "alice", password = AuthHarness.Password });
        Assert.Equal(HttpStatusCode.Forbidden, (await http.PostAsync("/api/auth/login", body)).StatusCode);
        foreach (var (origin, site) in new[] { ("https://evil.example", (string?)null), (h.Origin.Replace("127.0.0.1", "localhost"), null), ("null", null), (h.Origin, "cross-site"), (h.Origin, "same-site") })
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = AuthHarness.Client.Json(new { name = "alice", password = AuthHarness.Password }) };
            req.Headers.Add("Origin", origin);
            if (site is not null) req.Headers.Add("Sec-Fetch-Site", site);
            var r = await http.SendAsync(req);
            Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
            Assert.Equal("csrf", await CodeAsync(r));
        }
        using var okReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = AuthHarness.Client.Json(new { name = "alice", password = AuthHarness.Password }) };
        okReq.Headers.Add("Origin", h.Origin);
        okReq.Headers.Add("Sec-Fetch-Site", "same-origin");
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(okReq)).StatusCode);
        // reads need no Origin
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/api/auth/status")).StatusCode);
    }

    [Fact]
    public async Task The_gate_checks_every_route()
    {
        await using var h = await AuthHarness.StartAsync(kap: false);
        await h.AccountAsync("vic", "viewer");
        await h.AccountAsync("ada", "admin");
        using var anon = h.NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/core/modules")).StatusCode);
        using var vic = h.NewClient();
        await vic.SignInAsync("vic");
        Assert.Equal(HttpStatusCode.Forbidden, (await vic.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await vic.GetAsync("/api/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await vic.GetAsync("/api/core/modules")).StatusCode);
        using var ada = h.NewClient();
        await ada.SignInAsync("ada");
        Assert.Equal(HttpStatusCode.OK, (await ada.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ada.PostAsync("/api/users", new { name = "eve", password = AuthHarness.Password, roles = new[] { "viewer" } })).StatusCode);
        await Eventually.True(() => h.Auth.Audit.Read(50).Any(e => e.GetProperty("op").GetString() == "auth.denied" && e.GetProperty("userName").GetString() == "vic"));
    }

    [Fact]
    public async Task Two_factor_setup_sign_in_replay_and_recovery()
    {
        await using var h = await AuthHarness.StartAsync(kap: false);
        await h.AccountAsync("tess", "admin");
        using var b = h.NewClient();
        using var other = h.NewClient();
        await b.SignInAsync("tess");
        await other.SignInAsync("tess");
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.PostAsync("/api/auth/2fa/setup", new { password = "wrong one!!" })).StatusCode);
        var setup = await b.JsonAsync(await b.PostAsync("/api/auth/2fa/setup", new { password = AuthHarness.Password }));
        var secret = Base32.Decode(setup!["secret"]!.GetValue<string>())!;
        Assert.StartsWith("otpauth://totp/", setup["uri"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.PostAsync("/api/auth/2fa/enable", new { code = "000000" })).StatusCode);
        var step = Totp.StepAt(DateTimeOffset.UtcNow);
        var enabled = await b.PostAsync("/api/auth/2fa/enable", new { code = Totp.Code(secret, step) });
        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
        var codes = (await b.JsonAsync(enabled))!["recoveryCodes"]!.AsArray().Select(c => c!.GetValue<string>()).ToList();
        Assert.Equal(10, codes.Count);
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync("/api/auth/me")).StatusCode);              // rotated, still in
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/auth/me")).StatusCode);  // the other session ended
        await b.PostAsync("/api/auth/logout");

        // sign in: the password alone is not enough
        var first = await b.JsonAsync(await b.SignInAsync("tess"));
        Assert.Equal("2fa", first!["next"]!.GetValue<string>());
        Assert.Null(b.Session);
        Assert.NotNull(b.Cookie(AuthCookies.TwoFactor));
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.PostAsync("/api/auth/login/2fa", new { code = "123456" })).StatusCode);
        // the code used to enable it was spent: the next step's code works, and only once
        var next = Totp.Code(secret, step + 1);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.PostAsync("/api/auth/login/2fa", new { code = Totp.Code(secret, step) })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await b.PostAsync("/api/auth/login/2fa", new { code = next })).StatusCode);
        Assert.Equal("password+2fa", (await b.JsonAsync(await b.GetAsync("/api/auth/me")))!["session"]!["via"]!.GetValue<string>());
        await b.PostAsync("/api/auth/logout");
        Assert.Equal("2fa", (await b.JsonAsync(await b.SignInAsync("tess")))!["next"]!.GetValue<string>());
        var replay = await b.PostAsync("/api/auth/login/2fa", new { code = next });
        Assert.Equal("invalid_code", await CodeAsync(replay));
        // a recovery code works once
        Assert.Equal(HttpStatusCode.OK, (await b.PostAsync("/api/auth/login/2fa", new { recoveryCode = codes[0] })).StatusCode);
        await b.PostAsync("/api/auth/logout");
        Assert.Equal("2fa", (await b.JsonAsync(await b.SignInAsync("tess")))!["next"]!.GetValue<string>());
        Assert.Equal("invalid_code", await CodeAsync(await b.PostAsync("/api/auth/login/2fa", new { recoveryCode = codes[0] })));
        Assert.Equal(9, h.Auth.Accounts.FindByName("tess")!.RecoveryCodes.Count);
    }

    [Fact]
    public async Task Two_factor_tickets_allow_five_tries()
    {
        await using var h = await AuthHarness.StartAsync(kap: false);
        var a = await h.AccountAsync("tom", "viewer");
        h.Auth.Accounts.Update(a.Id, x => x with { TotpSecret = Base32.Encode(Totp.NewSecret()) });
        using var b = h.NewClient();
        await b.SignInAsync("tom");
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await b.PostAsync("/api/auth/login/2fa", new { code = "000000" })).StatusCode);
        var r = await b.PostAsync("/api/auth/login/2fa", new { code = "000000" });
        Assert.Contains(await CodeAsync(r), new[] { "2fa_expired", "rate_limited" });
    }

    [Fact]
    public async Task Restricted_sessions_can_only_fix_the_account()
    {
        await using var h = await AuthHarness.StartAsync(kap: false);
        var a = await h.AccountAsync("newbie", "admin");
        h.Auth.Accounts.Update(a.Id, x => x with { MustChangePassword = true });
        using var b = h.NewClient();
        await b.SignInAsync("newbie");
        var blocked = await b.GetAsync("/api/core/modules");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("password_change_required", await CodeAsync(blocked));
        Assert.Equal("password_change_required", (await b.JsonAsync(await b.GetAsync("/api/auth/me")))!["restriction"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, (await b.PostAsync("/api/auth/password", new { current = AuthHarness.Password, password = "a much better one 42" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync("/api/core/modules")).StatusCode);

        // a role that requires two-factor
        h.Auth.Roles.Put(Kilnwarden.Auth.RoleStore.Default("moderator") with { Require2fa = true });
        await h.AccountAsync("moe", "moderator");
        using var m = h.NewClient();
        await m.SignInAsync("moe");
        var r = await m.GetAsync("/api/core/modules");
        Assert.Equal("2fa_setup_required", await CodeAsync(r));
        Assert.Equal(HttpStatusCode.OK, (await m.PostAsync("/api/auth/2fa/setup", new { password = AuthHarness.Password })).StatusCode);
    }

    [Fact]
    public async Task A_role_change_rotates_the_session_id()
    {
        await using var h = await AuthHarness.StartAsync(kap: false);
        var u = await h.AccountAsync("rot", "viewer");
        using var b = h.NewClient();
        await b.SignInAsync("rot");
        var before = b.Session;
        Assert.Equal(HttpStatusCode.Forbidden, (await b.GetAsync("/api/users")).StatusCode);
        h.Auth.Accounts.Update(u.Id, x => x with { Roles = ["admin"], PrivilegeStamp = x.PrivilegeStamp + 1 });
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync("/api/users")).StatusCode);
        Assert.NotEqual(before, b.Session);
        Assert.Null(h.Auth.Sessions.Find(before));
    }

    [Fact]
    public async Task Nobody_hands_out_more_than_they_have()
    {
        await using var h = await AuthHarness.StartAsync(kap: false);
        h.Auth.Roles.Put(Kilnwarden.Auth.RoleStore.Default("admin") with { Grants = ["users.manage", "users.roles"] });
        var owner = await h.AccountAsync("own", "owner");
        var admin = await h.AccountAsync("adm", "admin");
        using var a = h.NewClient();
        await a.SignInAsync("adm");
        Assert.Equal(HttpStatusCode.OK, (await a.PostAsync("/api/auth/reauth", new { password = AuthHarness.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.PostAsync("/api/users", new { name = "evil", password = AuthHarness.Password, roles = new[] { "owner" } })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await a.PostAsync("/api/users", new { name = "helper", password = AuthHarness.Password, roles = new[] { "moderator" } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.Http.PatchAsync($"api/users/{owner.Id}", AuthHarness.Client.Json(new { disabled = true }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.Http.PatchAsync($"api/users/{admin.Id}", AuthHarness.Client.Json(new { roles = new[] { "owner" } }))).StatusCode);
        // a custom role can't get permissions the admin lacks (users.view is the admin's; core.settings.edit is the owner's)
        Assert.Equal(HttpStatusCode.Forbidden, (await a.PostAsync("/api/users/roles", new { id = "sneaky", name = "S", grants = new[] { "core.settings.edit" } })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await a.PostAsync("/api/users/roles", new { id = "helpers", name = "Helpers", grants = new[] { "players.players" }, commandLevel = "everyone" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.Http.PutAsync("api/users/roles/admin", AuthHarness.Client.Json(new { grants = new[] { "core.settings.edit" } }))).StatusCode);
        // the owner may, and the last owner stays
        using var o = h.NewClient();
        await o.SignInAsync("own");
        Assert.Equal(HttpStatusCode.OK, (await o.PostAsync("/api/auth/reauth", new { password = AuthHarness.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await o.Http.PatchAsync($"api/users/{owner.Id}", AuthHarness.Client.Json(new { disabled = true }))).StatusCode); // self
        var helper = h.Auth.Accounts.FindByName("helper")!;
        Assert.Equal(HttpStatusCode.OK, (await o.Http.PatchAsync($"api/users/{helper.Id}", AuthHarness.Client.Json(new { roles = new[] { "owner" } }))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await o.Http.PatchAsync($"api/users/{helper.Id}", AuthHarness.Client.Json(new { roles = new[] { "viewer" } }))).StatusCode);
        await Eventually.True(() => h.Auth.Audit.Read(100).Count(e => e.GetProperty("op").GetString() == "users.update") == 2);
    }

    [Fact]
    public async Task Sessions_list_and_revoke_are_per_account()
    {
        await using var h = await AuthHarness.StartAsync(kap: false);
        await h.AccountAsync("ses1", "viewer");
        await h.AccountAsync("ses2", "viewer");
        using var a1 = h.NewClient();
        using var a2 = h.NewClient();
        using var b = h.NewClient();
        await a1.SignInAsync("ses1");
        await a2.SignInAsync("ses1");
        await b.SignInAsync("ses2");
        var list = (await a1.JsonAsync(await a1.GetAsync("/api/auth/sessions")))!.AsArray();
        Assert.Equal(2, list.Count);
        var mine = list.Single(s => s!["current"]!.GetValue<bool>())!["handle"]!.GetValue<string>();
        var bHandle = h.Auth.Sessions.ForAccount(h.Auth.Accounts.FindByName("ses2")!.Id).Single().Handle;
        Assert.Equal(HttpStatusCode.NotFound, (await a1.Http.DeleteAsync($"api/auth/sessions/{bHandle}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync("/api/auth/me")).StatusCode);
        var r = await a1.JsonAsync(await a1.PostAsync("/api/auth/sessions/revoke-others"));
        Assert.Equal(1, r!["revoked"]!.GetValue<int>());
        Assert.Equal(HttpStatusCode.Unauthorized, (await a2.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a1.Http.DeleteAsync($"api/auth/sessions/{mine}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await a1.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task The_launchers_one_time_login_redeems_over_KAP()
    {
        var good = new string('a', 64);
        await using var h = await AuthHarness.StartAsync(kap: true, setup: k => k.Answer = req =>
        {
            var op = req["op"]!.GetValue<string>();
            if (op != "login_redeem") return new JsonObject { ["t"] = "res", ["ok"] = true, ["op"] = op };
            var ok = req["args"]!["secret"]!.GetValue<string>() == good && req["args"]!["consumer"]!.GetValue<string>() == Product.ModId;
            return ok
                ? new JsonObject { ["t"] = "res", ["ok"] = true, ["op"] = op, ["data"] = new JsonObject { ["role"] = "owner", ["mintedBy"] = "kiln-launcher/0.2.0" } }
                : new JsonObject { ["t"] = "res", ["ok"] = false, ["op"] = op, ["code"] = "op_failed", ["error"] = "no such login" };
        });
        using var b = h.NewClient();
        // no owner yet: a setup token
        var r = await b.JsonAsync(await b.PostAsync("/api/auth/launcher", new { secret = good }));
        Assert.Equal("setup", r!["next"]!.GetValue<string>());
        var token = r["setupToken"]!.GetValue<string>();
        Assert.Equal(HttpStatusCode.OK, (await b.PostAsync("/api/auth/setup", new { token, name = "boss", password = AuthHarness.Password })).StatusCode);
        await b.PostAsync("/api/auth/logout");
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.PostAsync("/api/auth/launcher", new { secret = new string('b', 64) })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.PostAsync("/api/auth/launcher", new { secret = "short" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await b.PostAsync("/api/auth/launcher", new { secret = good })).StatusCode);
        var me = await b.JsonAsync(await b.GetAsync("/api/auth/me"));
        Assert.Equal("boss", me!["name"]!.GetValue<string>());
        Assert.Equal("launcher", me["session"]!["via"]!.GetValue<string>());
        // the browser never chooses the consumer, and the redeem is the panel's own call: the mod id, then (only after a
        // refusal) the old mod id that released launchers mint for
        Assert.All(h.Kap!.Requests("login_redeem"), f => Assert.Contains(f["args"]!["consumer"]!.GetValue<string>(), new[] { Product.ModId, Kilnwarden.Hosting.LegacyInstall.OldModId }));
    }

    [Fact]
    public async Task A_login_a_launcher_minted_for_the_old_mod_id_still_signs_in()
    {
        var good = new string('c', 64);
        await using var h = await AuthHarness.StartAsync(kap: true, setup: k => k.Answer = req =>
        {
            var op = req["op"]!.GetValue<string>();
            if (op != "login_redeem") return new JsonObject { ["t"] = "res", ["ok"] = true, ["op"] = op };
            var ok = req["args"]!["secret"]!.GetValue<string>() == good && req["args"]!["consumer"]!.GetValue<string>() == Kilnwarden.Hosting.LegacyInstall.OldModId;
            return ok
                ? new JsonObject { ["t"] = "res", ["ok"] = true, ["op"] = op, ["data"] = new JsonObject { ["role"] = "owner", ["mintedBy"] = "kiln-launcher/0.2.0" } }
                : new JsonObject { ["t"] = "res", ["ok"] = false, ["op"] = op, ["code"] = "op_failed", ["error"] = "no such login" };
        });
        using var b = h.NewClient();
        var r = await b.JsonAsync(await b.PostAsync("/api/auth/launcher", new { secret = good }));
        Assert.Equal("setup", r!["next"]!.GetValue<string>());
        var consumers = h.Kap!.Requests("login_redeem").Select(f => f["args"]!["consumer"]!.GetValue<string>()).ToArray();
        Assert.Equal(new[] { Product.ModId, Kilnwarden.Hosting.LegacyInstall.OldModId }, consumers);
        // a wrong secret is refused for both
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.PostAsync("/api/auth/launcher", new { secret = new string('d', 64) })).StatusCode);
    }
}

/// <summary>An older Kiln.Admin (a Kiln before 0.2.0-dev.30.9, which Kiln's host loads us on anyway): the panel refuses
/// to serve, stays on loopback, and says why.</summary>
public class KilnTooOldTests
{
    [Fact]
    public async Task The_panel_refuses_to_serve_on_an_old_Kiln_and_stays_on_loopback()
    {
        await using var h = await AuthHarness.StartAsync(setup: k => k.BeOld(), waitKap: false);
        await Eventually.True(() => h.KapClient.MissingCapabilities is not null, 5000, "the verdict");
        await h.AccountAsync("own", "owner");
        // the bind rule: no owner as far as the listeners know, so loopback only
        Assert.False(h.Host.Services.Get<Kilnwarden.Hosting.ISetupState>().OwnerExists);
        using var b = h.NewClient();
        var page = await b.GetAsync("/");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, page.StatusCode);
        Assert.StartsWith("text/html", page.Content.Headers.ContentType!.ToString());
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains($"needs Kiln {Product.KilnFloor} or newer", html);
        Assert.Contains("login_mint", html);
        Assert.DoesNotContain("<script", html);
        foreach (var r in new[] { await b.GetAsync("/api/auth/status"), await b.SignInAsync("own"), await b.GetAsync("/api/core/modules") })
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
            Assert.Equal("kiln_too_old", (await b.JsonAsync(r))!["error"]!["code"]!.GetValue<string>());
        }
        Assert.Single(h.Log.Lines, l => l.Contains("needs Kiln"));
        Assert.DoesNotContain(h.Log.Lines, l => l.StartsWith("E ") && !l.Contains("needs Kiln"));
    }

    [Fact]
    public async Task A_309_Kiln_Admin_leaves_the_owner_rule_alone()
    {
        await using var h = await AuthHarness.StartAsync();
        await h.AccountAsync("own", "owner");
        Assert.True(h.Host.Services.Get<Kilnwarden.Hosting.ISetupState>().OwnerExists);
    }
}
