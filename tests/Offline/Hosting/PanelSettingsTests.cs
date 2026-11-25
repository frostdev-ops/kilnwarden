// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Offline.Support;

namespace Kilnwarden.Offline.Hosting;

/// <summary>The Panel settings page's routes: the components' settings files (validated, owner + recent proof, audited)
/// and the branding images.</summary>
[Collection("lane-c")]
public sealed class PanelSettingsTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    [Fact]
    public async Task Settings_files_are_listed_validated_and_saved_with_proof()
    {
        await using var c = await LaneC.LaneC.StartAsync(extra: FirstPartyExtras());
        using var admin = await c.UserAsync("adam", BuiltinRoles.Admin);
        using var owner = await c.UserAsync("olive", BuiltinRoles.Owner);
        var list = (await LaneC.LaneC.JsonAsync(await admin.GetAsync("/api/core/settings/files")))!["files"]!.AsArray();
        var ids = list.Select(f => f!["id"]!.GetValue<string>()).ToList();
        Assert.Contains("identity", ids);
        Assert.Contains("relay", ids);
        Assert.Contains("acme", ids);
        Assert.Contains("branding", ids);
        Assert.Contains("auth", ids);
        var relay = list.Single(f => f!["id"]!.GetValue<string>() == "relay")!;
        Assert.False(relay["value"]!["enabled"]!.GetValue<bool>());

        var body = new { enabled = true, origin = "https://frostdev.io", host = "Not A Name" };
        Assert.Equal(HttpStatusCode.Forbidden, (await LaneC.LaneC.PutAsync(admin, "/api/core/settings/files/relay", body)).StatusCode);
        Assert.Equal("reauth_required", await LaneC.LaneC.CodeAsync(await LaneC.LaneC.PutAsync(owner, "/api/core/settings/files/relay", body)));
        await LaneC.LaneC.ReauthAsync(owner);
        var r = await LaneC.LaneC.PutAsync(owner, "/api/core/settings/files/relay", body);
        Assert.Equal(HttpStatusCode.UnprocessableContent, r.StatusCode);
        Assert.Contains("host", await r.Content.ReadAsStringAsync());
        r = await LaneC.LaneC.PutAsync(owner, "/api/core/settings/files/relay", body with { host = "olive.kiln.nexus" });
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        Assert.Contains("olive.kiln.nexus", File.ReadAllText(Path.Combine(c.H.DataDir, "relay.json")));
        // the relay's status says what it waits for (this test panel has TLS off)
        await Eventually.True(() => LaneC.LaneC.JsonAsync(admin.GetAsync("/api/core/relay").GetAwaiter().GetResult()).GetAwaiter().GetResult()!["enabled"]!.GetValue<bool>(), 5000, "relay status");
        var status = (await LaneC.LaneC.JsonAsync(await admin.GetAsync("/api/core/relay")))!;
        Assert.Equal("waiting", status["state"]!.GetValue<string>());
        Assert.Contains(status["waitingFor"]!.AsArray(), w => w!.GetValue<string>().StartsWith("TLS"));
        Assert.Equal(HttpStatusCode.NotFound, (await LaneC.LaneC.PutAsync(owner, "/api/core/settings/files/nope", body)).StatusCode);
        await Eventually.True(() => c.H.Host.Services.Get<AuditLog>().Read(50).Any(e => e.GetProperty("op").GetString() == "core.settings.edit"), 3000, "audited");
    }

    [Fact]
    public async Task Branding_images_are_uploaded_sniffed_and_removed()
    {
        await using var c = await LaneC.LaneC.StartAsync(extra: FirstPartyExtras());
        using var owner = await c.UserAsync("olive", BuiltinRoles.Owner);
        await LaneC.LaneC.ReauthAsync(owner);
        using var anon = c.H.NewClient();
        var r = await owner.Http.PostAsync("api/core/branding/logo", Image(Png, "image/png"));
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var b = await LaneC.LaneC.JsonAsync(await anon.GetAsync("/api/core/branding"));
        Assert.Equal("/api/core/branding/logo", b!["logo"]!.GetValue<string>());
        Assert.Equal("image/png", (await anon.GetAsync("/api/core/branding/logo")).Content.Headers.ContentType!.MediaType);
        // not an image of the type it claims, and SVG: refused
        Assert.Equal(HttpStatusCode.UnprocessableContent, (await owner.Http.PostAsync("api/core/branding/favicon", Image("<svg/>"u8.ToArray(), "image/png"))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableContent, (await owner.Http.PostAsync("api/core/branding/favicon", Image("<svg/>"u8.ToArray(), "image/svg+xml"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Http.DeleteAsync("api/core/branding/logo")).StatusCode);
        Assert.Null((await LaneC.LaneC.JsonAsync(await anon.GetAsync("/api/core/branding")))!["logo"]);
    }

    /// <summary>DESIGN §4: map tiles served by the server. The owner copies the launcher's admin assets (game art, never
    /// shipped) into the data folder; signed-in users get them, nothing outside that folder is reachable.</summary>
    [Fact]
    public async Task Map_assets_are_served_from_the_data_folder_to_signed_in_users_only()
    {
        await using var c = await LaneC.LaneC.StartAsync(extra: FirstPartyExtras());
        using var viewer = await c.UserAsync("vic", BuiltinRoles.Viewer);
        using var anon = c.H.NewClient();
        var status = (await LaneC.LaneC.JsonAsync(await viewer.GetAsync("/api/core/map-assets-status")))!;
        Assert.False(status["present"]!.GetValue<bool>());
        var dir = Path.Combine(c.H.DataDir, "map-assets", "map", "tiles", "0");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "0_0.webp"), [.. "RIFF\0\0\0\0WEBPVP8 "u8]);
        File.WriteAllText(Path.Combine(c.H.DataDir, "secret.txt"), "nope");
        Assert.True((await LaneC.LaneC.JsonAsync(await viewer.GetAsync("/api/core/map-assets-status")))!["present"]!.GetValue<bool>());
        var r = await viewer.GetAsync("/api/core/map-assets/map/tiles/0/0_0.webp");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("image/webp", r.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/core/map-assets/map/tiles/0/0_0.webp")).StatusCode);
        foreach (var bad in new[] { "../secret.txt", "map/../../secret.txt", "%2e%2e/secret.txt", "map/tiles/0/missing.webp", ".hidden" })
            Assert.Contains((await viewer.GetAsync("/api/core/map-assets/" + bad)).StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.BadRequest });
        if (OperatingSystem.IsWindows()) return;
        // a link below the folder is not followed; the folder itself may be a link (to the launcher's admin-assets)
        var outside = Path.Combine(c.H.Tmp.Path, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllBytes(Path.Combine(outside, "x.webp"), [.. "RIFF\0\0\0\0WEBPVP8 "u8]);
        Directory.CreateSymbolicLink(Path.Combine(c.H.DataDir, "map-assets", "map", "escape"), outside);
        Assert.Equal(HttpStatusCode.NotFound, (await viewer.GetAsync("/api/core/map-assets/map/escape/x.webp")).StatusCode);
        var real = Path.Combine(c.H.Tmp.Path, "launcher-admin-assets");
        Directory.Move(Path.Combine(c.H.DataDir, "map-assets"), real);
        Directory.CreateSymbolicLink(Path.Combine(c.H.DataDir, "map-assets"), real);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/core/map-assets/map/tiles/0/0_0.webp")).StatusCode);
    }

    /// <summary>DESIGN §5: each account's own overview layout.</summary>
    [Fact]
    public async Task Each_account_keeps_its_own_dashboard_layout()
    {
        await using var c = await LaneC.LaneC.StartAsync();
        using var a = await c.UserAsync("amy", BuiltinRoles.Viewer);
        using var b = await c.UserAsync("ben", BuiltinRoles.Viewer);
        Assert.Equal(HttpStatusCode.OK, (await LaneC.LaneC.PutAsync(a, "/api/core/dashboard", new { order = new[] { "metrics/players", "panel/server" }, hidden = new[] { "panel/links" } })).StatusCode);
        var mine = (await LaneC.LaneC.JsonAsync(await a.GetAsync("/api/core/dashboard")))!;
        Assert.Equal("metrics/players", mine["order"]![0]!.GetValue<string>());
        Assert.Equal("panel/links", mine["hidden"]![0]!.GetValue<string>());
        Assert.Empty((await LaneC.LaneC.JsonAsync(await b.GetAsync("/api/core/dashboard")))!["order"]!.AsArray());
        Assert.Equal(HttpStatusCode.UnprocessableContent, (await LaneC.LaneC.PutAsync(a, "/api/core/dashboard", new { order = new[] { "../x" }, hidden = Array.Empty<string>() })).StatusCode);
    }

    /// <summary>The public page and webhooks name the server like the sign-in page does: branding, else the game's own
    /// server name (found in the lab: they said "Kilnwarden").</summary>
    [Fact]
    public async Task The_public_page_names_the_game_server()
    {
        await using var c = await LaneC.LaneC.StartAsync(extra: FirstPartyExtras());
        File.WriteAllText(Path.Combine(c.H.Tmp.Path, "enshrouded_server.json"), "{\"name\": \"Embervale\", \"queryPort\": 15637}");
        using var admin = await c.UserAsync("adam", BuiltinRoles.Admin);
        await LaneC.LaneC.ReauthAsync(admin);
        Assert.Equal(HttpStatusCode.OK, (await LaneC.LaneC.PutAsync(admin, "/api/public/settings", new Kilnwarden.PublicPage.PublicSettings { Enabled = true })).StatusCode);
        using var anon = c.H.NewClient();
        Assert.Equal("Embervale", (await LaneC.LaneC.JsonAsync(await anon.GetAsync("/api/public/status")))!["name"]!.GetValue<string>());
    }

    /// <summary>Found by the UI pass: settings.json's enums went out as numbers and a PUT with the names the data files
    /// use was a 400; the module switches had no list of every module (switched-off ones disappeared).</summary>
    [Fact]
    public async Task Panel_settings_round_trip_enum_names_and_list_every_module_switch()
    {
        await using var c = await LaneC.LaneC.StartAsync(extra: FirstPartyExtras());
        using var owner = await c.UserAsync("olive", BuiltinRoles.Owner);
        await LaneC.LaneC.ReauthAsync(owner);
        var s = (await LaneC.LaneC.JsonAsync(await owner.GetAsync("/api/core/settings")))!["settings"]!.AsObject();
        Assert.Equal("off", s["tls"]!["mode"]!.GetValue<string>());
        Assert.Equal("auto", s["tls"]!["engine"]!.GetValue<string>());
        var r = await LaneC.LaneC.PutAsync(owner, "/api/core/settings", s);
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        // every module with its switch, including one switched off
        Assert.Equal(HttpStatusCode.OK, (await LaneC.LaneC.PutAsync(owner, "/api/core/modules/metrics", new { enabled = false })).StatusCode);
        var all = (await LaneC.LaneC.JsonAsync(await owner.GetAsync("/api/core/modules/switches")))!["modules"]!.AsArray();
        var metrics = all.Single(m => m!["id"]!.GetValue<string>() == "metrics")!;
        Assert.False(metrics["enabled"]!.GetValue<bool>());
        Assert.True(metrics["canDisable"]!.GetValue<bool>());
        Assert.False(all.Single(m => m!["id"]!.GetValue<string>() == "core")!["canDisable"]!.GetValue<bool>());
        using var viewer = await c.UserAsync("vic", BuiltinRoles.Viewer);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/core/modules/switches")).StatusCode);
    }

    /// <summary>Found by the UI pass: the unavailable save-time metric had its key as its title.</summary>
    [Fact]
    public async Task The_save_time_metric_has_a_title()
    {
        await using var c = await LaneC.LaneC.StartAsync();
        var fields = (await LaneC.LaneC.JsonAsync(await (await c.UserAsync("vic", BuiltinRoles.Moderator)).GetAsync("/api/metrics/fields")))!["fields"]!.AsArray();
        Assert.Equal("World save time", fields.Single(f => f!["key"]!.GetValue<string>() == "saveMs")!["title"]!.GetValue<string>());
    }

    private static ByteArrayContent Image(byte[] bytes, string type)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(type);
        return content;
    }

    private static IEnumerable<Kilnwarden.Hosting.IWebAdminComponent> FirstPartyExtras() =>
        [new Kilnwarden.Identity.IdentityComponent(), new Kilnwarden.Tls.Acme.AcmeComponent(), new Kilnwarden.Branding.BrandingComponent(), new Kilnwarden.Relay.RelayComponent(),
         new Kilnwarden.Static.MapAssetsComponent()];
}
