// SPDX-License-Identifier: MIT
using System.Net;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Offline.Support;

namespace Kilnwarden.Offline.Ops;

public class UpdatesTests
{
    /// <summary>A fake Kiln.Admin for staging: update_list names the incoming folder and one installed mod; update_stage
    /// records what was in the source folder when it was called (path -> sha256).</summary>
    private static (Action<FakeKap> Setup, Dictionary<string, string> Seen) Staging(Func<string> incoming)
    {
        var seen = new Dictionary<string, string>();
        return (k => k.Answer = req =>
        {
            var op = req["op"]!.GetValue<string>();
            var args = req["args"] as JsonObject;
            JsonNode data = op switch
            {
                "update_list" => new JsonObject
                {
                    ["incoming"] = incoming(),
                    ["units"] = new JsonArray(new JsonObject { ["unit"] = "mod.cool-mod", ["kind"] = "mod", ["folder"] = "cool-mod", ["id"] = "cool-mod", ["current"] = new JsonObject { ["version"] = "1.1.0" } }),
                },
                "update_stage" => Stage(incoming(), args!, seen),
                _ => new JsonObject { ["args"] = args?.DeepClone() },
            };
            return new JsonObject { ["t"] = "res", ["ok"] = true, ["op"] = op, ["ms"] = 1, ["data"] = data };
        }, seen);
    }

    private static JsonObject Stage(string incoming, JsonObject args, Dictionary<string, string> seen)
    {
        var dir = Path.Combine(incoming, args["source"]!.GetValue<string>());
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            seen[Path.GetRelativePath(dir, f).Replace('\\', '/')] = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(f)));
        return new JsonObject { ["unit"] = "mod.cool-mod", ["staged"] = new JsonObject { ["version"] = "1.2.0" }, ["note"] = "applied at the next start" };
    }

    private static async Task<JsonNode> UploadAsync(OpsHarness.Client c, byte[] zip, string? sha = null)
    {
        var open = await c.PostAsync("/api/updates/uploads", new { name = "cool-mod.zip", size = zip.Length, sha256 = sha });
        Assert.True(open.StatusCode == HttpStatusCode.Created, await open.Content.ReadAsStringAsync());
        var o = (await OpsHarness.Client.JsonAsync(open))!;
        var id = o["id"]!.GetValue<string>();
        var chunk = 700; // several chunks
        for (var off = 0; off < zip.Length; off += chunk)
        {
            var r = await c.PutBytesAsync($"/api/updates/uploads/{id}?offset={off}", zip[off..Math.Min(zip.Length, off + chunk)]);
            Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        }
        var fin = await c.PostAsync($"/api/updates/uploads/{id}/finish", new { });
        Assert.True(fin.StatusCode == HttpStatusCode.Created, await fin.Content.ReadAsStringAsync());
        return (await OpsHarness.Client.JsonAsync(fin))!;
    }

    private static byte[] ModZip() =>
        Zips.Make(("cool-mod/kiln.mod.json", Zips.Mod()), ("cool-mod/Cool.dll", new string('M', 3000)), ("cool-mod/data/x.json", "{}"));

    [Fact]
    public async Task An_owner_uploads_checks_and_stages_a_mod_for_the_next_start()
    {
        string? incoming = null;
        var (setup, seen) = Staging(() => incoming!);
        await using var h = await OpsHarness.StartAsync(setup);
        incoming = h.Incoming;
        using var owner = await h.UserAsync("olivia", BuiltinRoles.Owner);
        var zip = ModZip();
        var sha = Convert.ToHexStringLower(SHA256.HashData(zip));
        var plan = await UploadAsync(owner, zip, sha);
        Assert.Equal(("mod", "cool-mod", "1.2.0", "1.1.0", sha), (plan["kind"]!.GetValue<string>(), plan["modId"]!.GetValue<string>(),
            plan["version"]!.GetValue<string>(), plan["current"]!.GetValue<string>(), plan["sha256"]!.GetValue<string>()));
        var planId = plan["id"]!.GetValue<string>();
        // staging is sensitive: proof first, and the digest echoed back
        var r = await owner.PostAsync($"/api/updates/plans/{planId}/stage", new { sha256 = sha });
        Assert.Equal("reauth_required", await OpsHarness.Client.CodeAsync(r));
        await owner.ReauthAsync();
        Assert.Equal((HttpStatusCode)422, (await owner.PostAsync($"/api/updates/plans/{planId}/stage", new { sha256 = new string('0', 64) })).StatusCode);
        r = await owner.PostAsync($"/api/updates/plans/{planId}/stage", new { sha256 = sha });
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var req = Assert.Single(h.Kap.Requests("update_stage"));
        var args = req["args"]!;
        Assert.Equal("mod", args["kind"]!.GetValue<string>());
        Assert.Equal("cool-mod", args["id"]!.GetValue<string>());
        Assert.Equal("web:olivia", args["by"]!.GetValue<string>());
        Assert.StartsWith("kilnwarden-", args["source"]!.GetValue<string>());
        var files = args["files"]!.AsArray().ToDictionary(f => f!["path"]!.GetValue<string>(), f => f!["sha256"]!.GetValue<string>());
        Assert.Equal(["Cool.dll", "data/x.json", "kiln.mod.json"], files.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(seen, files); // the list is exactly the folder Kiln.Admin copied from
        Assert.Empty(Directory.EnumerateDirectories(h.Incoming)); // removed after the call
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/updates/plans/{planId}")).StatusCode);
        await Eventually.True(() => h.Audit.Read(50).Any(e => e.GetProperty("op").GetString() == "updates.stage" && e.GetProperty("ok").GetBoolean()), 3000, "the audit entry");
    }

    [Fact]
    public async Task Only_owner_accounts_prepare_and_a_wrong_digest_or_package_is_refused()
    {
        await using var h = await OpsHarness.StartAsync();
        h.Auth.Roles.Put(new RoleDef { Id = "stager", Name = "Stager", Grants = ["updates.*", "mods.*"] });
        using var stager = await h.UserAsync("sam", "stager");
        var r = await stager.PostAsync("/api/updates/uploads", new { name = "x.zip", size = 10 });
        Assert.Equal("owner_only", await OpsHarness.Client.CodeAsync(r));
        using var admin = await h.UserAsync("alice", BuiltinRoles.Admin);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync("/api/updates/uploads", new { name = "x.zip", size = 10 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/updates")).StatusCode);
        using var owner = await h.UserAsync("olivia", BuiltinRoles.Owner);
        var zip = ModZip();
        var open = await OpsHarness.Client.JsonAsync(await owner.PostAsync("/api/updates/uploads", new { name = "m.zip", size = zip.Length, sha256 = new string('a', 64) }));
        var id = open!["id"]!.GetValue<string>();
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutBytesAsync($"/api/updates/uploads/{id}?offset=5", zip[..10])).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PutBytesAsync($"/api/updates/uploads/{id}?offset=0", zip)).StatusCode);
        var fin = await owner.PostAsync($"/api/updates/uploads/{id}/finish", new { });
        Assert.Equal("checksum", await OpsHarness.Client.CodeAsync(fin));
        // the wrong package for the id asked for
        var other = Zips.Make(("kiln.mod.json", Zips.Mod("other-mod")));
        var o2 = await OpsHarness.Client.JsonAsync(await owner.PostAsync("/api/updates/uploads", new { name = "o.zip", size = other.Length }));
        await owner.PutBytesAsync($"/api/updates/uploads/{o2!["id"]!.GetValue<string>()}?offset=0", other);
        var f2 = await owner.PostAsync($"/api/updates/uploads/{o2["id"]!.GetValue<string>()}/finish", new { expectId = "cool-mod" });
        Assert.Equal("wrong_package", await OpsHarness.Client.CodeAsync(f2));
        // a bad archive
        var evil = Zips.Make(("kiln.mod.json", Zips.Mod()), ("../evil.dll", "x"));
        var o3 = await OpsHarness.Client.JsonAsync(await owner.PostAsync("/api/updates/uploads", new { name = "e.zip", size = evil.Length }));
        await owner.PutBytesAsync($"/api/updates/uploads/{o3!["id"]!.GetValue<string>()}?offset=0", evil);
        Assert.Equal("archive", await OpsHarness.Client.CodeAsync(await owner.PostAsync($"/api/updates/uploads/{o3["id"]!.GetValue<string>()}/finish", new { })));
    }

    [Fact]
    public async Task A_url_needs_an_allowed_origin_and_its_digest()
    {
        await using var h = await OpsHarness.StartAsync();
        using var owner = await h.UserAsync("olivia", BuiltinRoles.Owner);
        var zip = ModZip();
        var sha = Convert.ToHexStringLower(SHA256.HashData(zip));
        h.Downloader.Files["https://github.com/me/cool/releases/download/v1/cool.zip"] = zip;
        Assert.Equal("source_refused", await OpsHarness.Client.CodeAsync(await owner.PostAsync("/api/updates/fetch", new { url = "https://evil.example/cool.zip", sha256 = sha })));
        Assert.Equal("source_refused", await OpsHarness.Client.CodeAsync(await owner.PostAsync("/api/updates/fetch", new { url = "http://github.com/me/cool.zip", sha256 = sha })));
        Assert.Empty(h.Downloader.Asked);
        Assert.Equal((HttpStatusCode)422, (await owner.PostAsync("/api/updates/fetch", new { url = "https://github.com/me/cool/releases/download/v1/cool.zip" })).StatusCode);
        Assert.Equal("checksum", await OpsHarness.Client.CodeAsync(await owner.PostAsync("/api/updates/fetch",
            new { url = "https://github.com/me/cool/releases/download/v1/cool.zip", sha256 = new string('b', 64) })));
        var ok = await owner.PostAsync("/api/updates/fetch", new { url = "https://github.com/me/cool/releases/download/v1/cool.zip?token=hush", sha256 = sha });
        Assert.Equal(HttpStatusCode.BadGateway, ok.StatusCode); // not in the fake's table with the query: 404
        Assert.DoesNotContain("hush", string.Join("\n", h.Audit.Read(100).Select(e => e.ToString())));
        var good = await owner.PostAsync("/api/updates/fetch", new { url = "https://github.com/me/cool/releases/download/v1/cool.zip", sha256 = sha });
        Assert.Equal(HttpStatusCode.Created, good.StatusCode);
        // the allowlist is the owner's, with proof
        var put = await owner.PutAsync("/api/updates/sources", new { allowedOrigins = new[] { "https://example.org" } });
        Assert.Equal("reauth_required", await OpsHarness.Client.CodeAsync(put));
        await owner.ReauthAsync();
        Assert.Equal((HttpStatusCode)422, (await owner.PutAsync("/api/updates/sources", new { allowedOrigins = new[] { "http://example.org" } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsync("/api/updates/sources", new { allowedOrigins = new[] { "https://example.org" } })).StatusCode);
    }

    [Fact]
    public async Task Lifecycle_cancel_and_rollback_go_through_the_ops_own_gate()
    {
        await using var h = await OpsHarness.StartAsync();
        using var admin = await h.UserAsync("alice", BuiltinRoles.Admin);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync("/api/updates/mods/load", new { folder = "cool-mod" })).StatusCode);
        using var owner = await h.UserAsync("olivia", BuiltinRoles.Owner);
        var r = await owner.PostAsync("/api/updates/mods/load", new { folder = "cool-mod" });
        Assert.Equal("reauth_required", await OpsHarness.Client.CodeAsync(r));
        Assert.Equal("reauth_required", await OpsHarness.Client.CodeAsync(await owner.PostAsync("/api/updates/units/mod.cool-mod/rollback")));
        await owner.ReauthAsync();
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync("/api/updates/mods/load", new { folder = "cool-mod" })).StatusCode);
        Assert.Equal((HttpStatusCode)422, (await owner.PostAsync("/api/updates/mods/load", new { folder = "../x" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync("/api/updates/units/mod.cool-mod/rollback")).StatusCode);
        Assert.Equal((HttpStatusCode)422, (await owner.PostAsync("/api/updates/units/mod..x/cancel")).StatusCode);
        Assert.Single(h.Kap.Requests("mod_load"));
        Assert.Equal("web:olivia", Assert.Single(h.Kap.Requests("update_rollback"))["args"]!["by"]!.GetValue<string>());
    }
}
