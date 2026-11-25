// SPDX-License-Identifier: MIT
using System.Net;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Offline.Support;
using Kilnwarden.PublicPage;
using Kilnwarden.Sample;

namespace Kilnwarden.Offline.LaneC;

[Collection("lane-c")]
public sealed class PublicPageTests
{
    [Fact]
    public async Task Off_by_default_and_only_allow_listed_parts_when_on()
    {
        await using var c = await LaneC.StartAsync();
        using var anon = c.H.NewClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/public/status")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/public/page")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/public/settings")).StatusCode);

        using var ext = SamplePanel.Register(c.ExtHost, "webadmin-sample");
        using var admin = await c.UserAsync("adam", BuiltinRoles.Admin);
        var on = new PublicSettings { Enabled = true, Rules = "Be nice <script>alert(1)</script>\n\n- no griefing\n- no cheats", JoinInfo = "Search \"Frost\"" };
        Assert.Equal("reauth_required", await LaneC.CodeAsync(await LaneC.PutAsync(admin, "/api/public/settings", on)));
        await LaneC.ReauthAsync(admin);
        on.MapDotsGrid = 5;
        Assert.Equal(HttpStatusCode.UnprocessableContent, (await LaneC.PutAsync(admin, "/api/public/settings", on)).StatusCode);
        on.MapDotsGrid = 100;
        Assert.Equal(HttpStatusCode.OK, (await LaneC.PutAsync(admin, "/api/public/settings", on)).StatusCode);
        c.Public.SetPlayers(["Ann", "Bob"], 2);

        var r = await anon.GetAsync("/api/public/status");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("public, max-age=15", r.Headers.CacheControl!.ToString());
        var s = (await LaneC.JsonAsync(r))!.AsObject();
        // names are off by default, so are mods and widgets; nothing else is in the snapshot
        Assert.Equal(["at", "joinInfo", "name", "online", "players", "rules"], s.Select(kv => kv.Key).Order());
        Assert.False(s["online"]!.GetValue<bool>()); // no KAP in this test: offline, so no count either
        Assert.Null(s["players"]);

        var html = await (await anon.GetAsync("/api/public/page")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("<script", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("<li>no griefing</li>", html);
        Assert.DoesNotContain("<svg", html); // map dots are off by default
        var csp = string.Join(";", (await anon.GetAsync("/api/public/page")).Headers.GetValues("Content-Security-Policy"));
        Assert.StartsWith("default-src 'none'", csp);
        Assert.DoesNotContain("script-src", csp);

        // the owner's extension widget only after it is listed
        on.Widgets = ["sample/hello"];
        on.ShowPlayerNames = true;
        Assert.Equal(HttpStatusCode.OK, (await LaneC.PutAsync(admin, "/api/public/settings", on)).StatusCode);
        var preview = await LaneC.JsonAsync(await admin.GetAsync("/api/public/preview"));
        Assert.Equal("Hello", preview!["widgets"]![0]!["data"]!.GetValue<string>());
        Assert.Empty(preview["playerNames"]!.AsArray()); // offline: no names either
        // admin routes stay out of reach anonymously
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/public/preview")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/server/status")).StatusCode);
    }

    /// <summary>Regression (step 2 lab): with "show mods" on, the status said "mods": null. Kiln.Admin's mods op names
    /// states "Loaded" (not "loaded"), and the first snapshot was cached before the list had arrived.</summary>
    [Fact]
    public async Task Show_mods_lists_the_loaded_mods()
    {
        await using var c = await LaneC.StartAsync(kap: true, setup: k =>
        {
            k.Ops = [.. FakeKap.DefaultOps, "mods"];
            var inner = k.Answer;
            k.Answer = req => req["op"]!.GetValue<string>() != "mods" ? inner(req) : new JsonObject
            {
                ["t"] = "res", ["ok"] = true, ["op"] = "mods", ["ms"] = 1,
                ["data"] = JsonNode.Parse("""
                    [{"id":"hello","name":"Hello Kiln","version":"0.1.0","side":"Server","state":"Loaded","error":null,"folder":"hello","faults":0},
                     {"id":"kiln-admin","name":"Kiln Admin","version":"0.2.0","state":"Loaded","folder":"kiln-admin"},
                     {"id":"broken","name":"Broken","version":"1.0.0","state":"Failed","error":"boom at C:/srv"}]
                    """),
            };
        });
        using var admin = await c.UserAsync("adam", BuiltinRoles.Admin);
        await LaneC.ReauthAsync(admin);
        Assert.Equal(HttpStatusCode.OK, (await LaneC.PutAsync(admin, "/api/public/settings", new PublicSettings { Enabled = true, ShowMods = true })).StatusCode);
        using var anon = c.H.NewClient();
        JsonNode? mods = null;
        await Eventually.True(() =>
        {
            mods = LaneC.JsonAsync(anon.GetAsync("/api/public/status").GetAwaiter().GetResult()).GetAwaiter().GetResult()?["mods"];
            return mods is JsonArray { Count: > 0 };
        }, 5000, "the public status listing the mods");
        Assert.Equal("""[{"id":"hello","name":"Hello Kiln","version":"0.1.0"},{"id":"kiln-admin","name":"Kiln Admin","version":"0.2.0"}]""", mods!.ToJsonString());
        Assert.Contains("Hello Kiln", await (await anon.GetAsync("/api/public/page")).Content.ReadAsStringAsync());
    }

    [Fact]
    public void Mod_list_keeps_only_id_name_version()
    {
        var list = PublicPageComponent.ModList(JsonNode.Parse("""
            {"mods":[{"id":"a","name":"A","version":"1.0","state":"loaded","path":"C:/srv/kiln/mods/a","faults":3},
                     {"id":"b","state":"failed","error":"boom"},{"name":"no id"},
                     {"id":"c","name":"C","version":"2","state":"Loaded"},{"id":"d","state":"Failed"},{"id":"e","state":"Unloaded"}]}
            """));
        Assert.Equal("""[{"id":"a","name":"A","version":"1.0"},{"id":"c","name":"C","version":"2"}]""", list.ToJsonString());
    }

    [Fact]
    public void Rules_text_is_escaped()
    {
        var t = PublicMarkdown.Render("<b>x</b>\n\n- <i>a</i>\n- b");
        Assert.Equal("<p>&lt;b&gt;x&lt;/b&gt;</p><ul><li>&lt;i&gt;a&lt;/i&gt;</li><li>b</li></ul>", t);
    }

    /// <summary>DESIGN §5: rules are markdown. A small, safe subset: headings, lists, emphasis, code and https links,
    /// everything else escaped; no raw HTML, no other URL schemes.</summary>
    [Theory]
    [InlineData("# Rules\n\nBe **kind** and *fair*.", "<h3>Rules</h3><p>Be <strong>kind</strong> and <em>fair</em>.</p>")]
    [InlineData("1. one\n2. two", "<ol><li>one</li><li>two</li></ol>")]
    [InlineData("See [our Discord](https://discord.gg/abc) or `/help`.", "<p>See <a href=\"https://discord.gg/abc\" rel=\"nofollow noopener noreferrer\">our Discord</a> or <code>/help</code>.</p>")]
    [InlineData("[x](javascript:alert(1))", "<p>[x](javascript:alert(1))</p>")]
    [InlineData("[x](https://e.x/\"onmouseover=\"a)", "<p><a href=\"https://e.x/&quot;onmouseover=&quot;a\" rel=\"nofollow noopener noreferrer\">x</a></p>")]
    [InlineData("**<img src=x onerror=alert(1)>**", "<p><strong>&lt;img src=x onerror=alert(1)&gt;</strong></p>")]
    [InlineData("## Sub\n* a\n* b", "<h4>Sub</h4><ul><li>a</li><li>b</li></ul>")]
    public void Rules_are_safe_markdown(string text, string html) => Assert.Equal(html, PublicMarkdown.Render(text));

    /// <summary>DESIGN §5: map dots, opt-in: rounded to the grid, no names on them, drawn without script.</summary>
    [Fact]
    public async Task Map_dots_are_coarse_and_nameless()
    {
        await using var c = await LaneC.StartAsync(kap: true);
        using var admin = await c.UserAsync("adam", BuiltinRoles.Admin);
        await LaneC.ReauthAsync(admin);
        var s = new PublicSettings { Enabled = true, ShowMapDots = true, MapDotsGrid = 100 };
        Assert.Equal(HttpStatusCode.OK, (await LaneC.PutAsync(admin, "/api/public/settings", s)).StatusCode);
        c.Public.SetPlayers(["Ann", "Bob"], 2);
        c.Public.SetPositions([(1234.5, 5678.9), (10.0, 10239.0), (double.NaN, 3.0), (-50, 20000)]);
        using var anon = c.H.NewClient();
        var st = (await LaneC.JsonAsync(await anon.GetAsync("/api/public/status")))!;
        var dots = st["dots"]!.AsArray();
        Assert.Equal("[{\"x\":50,\"z\":10150},{\"x\":1250,\"z\":5650}]", dots.ToJsonString()); // sorted: the order says nothing about who
        Assert.Equal(100, st["dotsGrid"]!.GetValue<int>());
        Assert.DoesNotContain("Ann", st.ToJsonString()); // names stay off
        var html = await (await anon.GetAsync("/api/public/page")).Content.ReadAsStringAsync();
        Assert.Contains("<svg", html);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(html, "<circle ").Count);
        Assert.DoesNotContain("<script", html);
    }
}
