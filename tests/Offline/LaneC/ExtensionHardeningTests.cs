// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;
using Kilnwarden.Extensions;
using Kilnwarden.Modules;

namespace Kilnwarden.Offline.LaneC;

/// <summary>Security review I2, I3, I5: extension actions' audit records, settings files and event names.</summary>
[Collection("lane-c")]
public sealed class ExtensionHardeningTests
{
    [Fact]
    public void Secret_looking_arguments_are_masked_at_any_depth_and_in_any_case()
    {
        var args = JsonNode.Parse("""
            {"name":"ok","Password":"p1","nested":{"apiToken":"t1","list":[{"secret":"s1","keep":"k"}]},"clientKey":"c1","declared":"d"}
            """)!.AsObject();
        var o = ActionRoutes.Redact(args, ["declared"]).ToJsonString();
        foreach (var leaked in new[] { "p1", "t1", "s1", "c1", "\"d\"" }) Assert.DoesNotContain(leaked, o);
        Assert.Contains("\"ok\"", o);
        Assert.Contains("\"k\"", o);
    }

    [Fact]
    public void First_party_event_prefixes_cannot_be_extension_modules()
    {
        foreach (var id in new[] { "player", "mod", "crash", "save", "schedule", "admin", "webhook", "server", "activity", "metrics" })
            Assert.Contains(id, ExtensionHost.ReservedIds);
    }

    [Fact]
    public async Task Extension_settings_files_are_written_owner_only()
    {
        if (OperatingSystem.IsWindows()) return;
        await using var c = await LaneC.StartAsync();
        using var ext = Kilnwarden.Sample.SamplePanel.Register(c.ExtHost, "webadmin-sample");
        using var owner = await c.UserAsync("olive", Kilnwarden.Auth.BuiltinRoles.Owner);
        await LaneC.ReauthAsync(owner);
        var r = await LaneC.PutAsync(owner, "/api/sample/settings", new { greeting = "Hi", maxVisits = 7 });
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        var file = Path.Combine(c.H.DataDir, "ext", "sample.json");
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
    }
}
