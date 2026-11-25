// SPDX-License-Identifier: MIT
using System.Net;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Offline.Support;

namespace Kilnwarden.Offline.Ops;

/// <summary>Persistent mod switches: Kiln skips a kiln/mods folder that holds a .disabled file at the next start, so the
/// panel writes and removes that file (owner, recent proof, audited), and can also unload or load the mod now.</summary>
public class ModSwitchTests
{
    private static string ModsDir(OpsHarness h) => Path.Combine(h.Tmp.Path, "kiln", "mods");

    private static void Mod(OpsHarness h, string folder, string id, string version = "1.0.0")
    {
        var dir = Path.Combine(ModsDir(h), folder);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "kiln.mod.json"), $$"""{"id":"{{id}}","name":"{{id}} mod","version":"{{version}}","apiVersion":"0.2"}""");
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage r) => (await OpsHarness.Client.JsonAsync(r))?["error"]?["code"]?.GetValue<string>();

    [Fact]
    public async Task An_owner_switches_a_mod_off_and_on_for_the_next_start()
    {
        await using var h = await OpsHarness.StartAsync(k => k.Ops = [.. FakeKap.DefaultOps, "mod_unload"]);
        Mod(h, "hello", "hello");
        Mod(h, "kiln-admin", "kiln-admin");
        Mod(h, "mod", "kilnwarden");
        using var owner = await h.UserAsync("olive", BuiltinRoles.Owner);
        using var admin = await h.UserAsync("adam", BuiltinRoles.Admin);

        // everyone who sees mods sees the folders and their switch
        var list = await OpsHarness.Client.JsonAsync(await admin.GetAsync("/api/updates"));
        var hello = list!["folders"]!.AsArray().Single(f => f!["folder"]!.GetValue<string>() == "hello")!;
        Assert.Equal(("hello", false), (hello["id"]!.GetValue<string>(), hello["disabled"]!.GetValue<bool>()));

        var off = new { folder = "hello", enabled = false };
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync("/api/updates/mods/switch", off)).StatusCode);
        Assert.Equal("reauth_required", await CodeAsync(await owner.PostAsync("/api/updates/mods/switch", off)));
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync("/api/auth/reauth", new { password = OpsHarness.Password })).StatusCode);
        var r = await owner.PostAsync("/api/updates/mods/switch", off);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.True(File.Exists(Path.Combine(ModsDir(h), "hello", ".disabled")));
        Assert.Contains("web:olive", File.ReadAllText(Path.Combine(ModsDir(h), "hello", ".disabled")));
        list = await OpsHarness.Client.JsonAsync(await admin.GetAsync("/api/updates"));
        Assert.True(list!["folders"]!.AsArray().Single(f => f!["folder"]!.GetValue<string>() == "hello")!["disabled"]!.GetValue<bool>());
        Assert.Empty(h.Kap.Requests("mod_unload")); // "now" was not asked

        // on again, and load it now
        r = await owner.PostAsync("/api/updates/mods/switch", new { folder = "hello", enabled = true, now = true });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.False(File.Exists(Path.Combine(ModsDir(h), "hello", ".disabled")));
        Assert.Equal("hello", Assert.Single(h.Kap.Requests("mod_load"))["args"]!["folder"]!.GetValue<string>());
        // off now: unloaded by its id
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync("/api/updates/mods/switch", new { folder = "hello", enabled = false, now = true })).StatusCode);
        Assert.Equal("hello", Assert.Single(h.Kap.Requests("mod_unload"))["args"]!["id"]!.GetValue<string>());
        await Eventually.True(() => h.Audit.Read(50).Count(e => e.GetProperty("op").GetString() == "updates.mod_switch") == 3, 3000, "three audit entries");
    }

    [Fact]
    public async Task The_panel_and_Kiln_Admin_cannot_be_switched_off_and_paths_stay_inside_kiln_mods()
    {
        await using var h = await OpsHarness.StartAsync();
        Mod(h, "hello", "hello");
        Mod(h, "kiln-admin", "kiln-admin");
        Mod(h, "renamed-admin", "kiln-admin");
        Mod(h, "mod", "kilnwarden");
        Directory.CreateDirectory(Path.Combine(h.Tmp.Path, "kiln", "outside"));
        using var owner = await h.UserAsync("olive", BuiltinRoles.Owner);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync("/api/auth/reauth", new { password = OpsHarness.Password })).StatusCode);
        foreach (var folder in new[] { "kiln-admin", "renamed-admin", "mod" })
        {
            var r = await owner.PostAsync("/api/updates/mods/switch", new { folder, enabled = false });
            Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
            Assert.Equal("needed_by_the_panel", await CodeAsync(r));
        }
        foreach (var folder in new[] { "../outside", "..", ".", "hello/../../outside", "a\\b", "", "nope" })
            Assert.Contains((await owner.PostAsync("/api/updates/mods/switch", new { folder, enabled = false })).StatusCode,
                new[] { HttpStatusCode.UnprocessableContent, HttpStatusCode.NotFound });
        Assert.False(File.Exists(Path.Combine(h.Tmp.Path, "kiln", "outside", ".disabled")));
        Assert.False(File.Exists(Path.Combine(h.Tmp.Path, "kiln", ".disabled")));
        // a .disabled that is a folder is not touched
        Directory.CreateDirectory(Path.Combine(ModsDir(h), "hello", ".disabled"));
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsync("/api/updates/mods/switch", new { folder = "hello", enabled = true })).StatusCode);
        Assert.True(Directory.Exists(Path.Combine(ModsDir(h), "hello", ".disabled")));
    }

    [Fact]
    public async Task The_old_Kiln_Web_Admin_cannot_be_switched_on_next_to_the_panel()
    {
        // two copies would fight for the port and the data, and at the next start the panel would stay off (LegacyInstall)
        await using var h = await OpsHarness.StartAsync(k => k.Ops = [.. FakeKap.DefaultOps, "mod_unload"]);
        Mod(h, "kiln-webadmin", "kiln-webadmin", "0.1.0");
        Mod(h, "old-test-build", "kiln-webadmin", "0.1.0");
        foreach (var folder in new[] { "kiln-webadmin", "old-test-build" })
            File.WriteAllText(Path.Combine(ModsDir(h), folder, ".disabled"), "");
        using var owner = await h.UserAsync("olive", BuiltinRoles.Owner);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync("/api/auth/reauth", new { password = OpsHarness.Password })).StatusCode);
        foreach (var folder in new[] { "kiln-webadmin", "old-test-build" })
        {
            var r = await owner.PostAsync("/api/updates/mods/switch", new { folder, enabled = true, now = true });
            Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
            Assert.Equal("old_name", await CodeAsync(r));
            Assert.True(File.Exists(Path.Combine(ModsDir(h), folder, ".disabled")));
        }
        Assert.Empty(h.Kap.Requests("mod_load"));
        // switching it off stays possible
        File.Delete(Path.Combine(ModsDir(h), "old-test-build", ".disabled"));
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync("/api/updates/mods/switch", new { folder = "old-test-build", enabled = false })).StatusCode);
    }
}
