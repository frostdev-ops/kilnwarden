// SPDX-License-Identifier: MIT
// The map art (0.1.3): where the Kiln Launcher runs on the server's own computer, the panel finds its admin-assets in
// the launcher's fixed folders only and copies map/ and data/ on the owner's one click: plain files of the types the
// map serves, no links, no dot-names, moved in only once complete. Elsewhere it says the launcher's art isn't here.
using System.Net;
using Kilnwarden.Auth;
using Kilnwarden.Offline.Support;
using Kilnwarden.Static;

namespace Kilnwarden.Offline.Hosting;

public sealed class MapArtCopyTests
{
    private static Func<string, string?> Env(Dictionary<string, string> v) => k => v.GetValueOrDefault(k);

    [Fact]
    public void The_launchers_folder_is_looked_for_only_where_the_launcher_keeps_it()
    {
        // Windows, the server and the launcher as the same user
        Assert.Equal([@"C:\Profiles\me\AppData\Local\Kiln\admin-assets"],
            LauncherArt.Candidates(Env([]), windows: true, localAppData: @"C:\Profiles\me\AppData\Local"));
        // under Wine: the prefix's own folder, then the Linux launcher's through Z: (Wine's home, or HOME/XDG_DATA_HOME)
        Assert.Equal([@"C:\users\steam\AppData\Local\Kiln\admin-assets", @"Z:\data\kiln\admin-assets", @"Z:\home\me\.local\share\kiln\admin-assets"],
            LauncherArt.Candidates(Env(new() { ["WINEHOMEDIR"] = @"\??\Z:\home\me", ["HOME"] = "/home/me", ["XDG_DATA_HOME"] = "/data" }), windows: true,
                localAppData: @"C:\users\steam\AppData\Local"));
        // the launcher's own override first, a Unix path made a Z: path under Wine
        Assert.Equal(@"Z:\srv\kiln-data\admin-assets", LauncherArt.Candidates(Env(new() { ["KILN_DATA_DIR"] = "/srv/kiln-data" }), windows: true, localAppData: "")[0]);
        // Linux and macOS
        Assert.Equal([Path.Combine("/x", "kiln", "admin-assets"), Path.Combine("/home/me", ".local", "share", "kiln", "admin-assets")],
            LauncherArt.Candidates(Env(new() { ["HOME"] = "/home/me", ["XDG_DATA_HOME"] = "/x" }), windows: false));
        Assert.Empty(LauncherArt.Candidates(Env([]), windows: false));
    }

    private static string Launcher(TempDir t)
    {
        var root = Path.Combine(t.Path, "kiln", "admin-assets");
        void File_(string rel, string text = "x")
        {
            var p = Path.Combine(root, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, text);
        }
        File_("map/tiles/0/0_0.webp");
        File_("map/gpu/d/0_0.webp");
        File_("map/fonts/a.ttf");
        File_("data/find.json", "{}");
        File_(".source.json", "{}");
        File_("map/.hidden.webp");
        File_("map/tool.exe");
        File_("other/x.webp");
        return root;
    }

    [Fact]
    public async Task The_owner_copies_the_launchers_map_art_in_one_click_and_only_its_map_files_come()
    {
        using var t = new TempDir();
        var source = Launcher(t);
        var outside = Path.Combine(t.Path, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "secret.json"), "{}");
        if (!OperatingSystem.IsWindows()) Directory.CreateSymbolicLink(Path.Combine(source, "map", "escape"), outside);
        await using var c = await LaneC.LaneC.StartAsync(extra: [new MapAssetsComponent(() => [Path.Combine(t.Path, "nothing-here"), source])]);
        using var viewer = await c.UserAsync("vic", BuiltinRoles.Viewer);
        using var admin = await c.UserAsync("adam", BuiltinRoles.Admin);
        using var owner = await c.UserAsync("olive", BuiltinRoles.Owner);
        var status = (await LaneC.LaneC.JsonAsync(await viewer.GetAsync("/api/core/map-assets-status")))!;
        Assert.False(status["present"]!.GetValue<bool>());
        Assert.True(status["launcher"]!.GetValue<bool>());
        Assert.Equal("idle", status["copy"]!["state"]!.GetValue<string>());

        await LaneC.LaneC.ReauthAsync(admin);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync("/api/core/map-assets/copy")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync("/api/core/map-assets/copy")).StatusCode);
        var r = await owner.PostAsync("/api/core/map-assets/copy");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Equal("reauth_required", await LaneC.LaneC.CodeAsync(r));
        await LaneC.LaneC.ReauthAsync(owner);
        Assert.Equal(HttpStatusCode.Accepted, (await owner.PostAsync("/api/core/map-assets/copy")).StatusCode);
        for (var i = 0; i < 200; i++)
        {
            status = (await LaneC.LaneC.JsonAsync(await viewer.GetAsync("/api/core/map-assets-status")))!;
            if (status["copy"]!["state"]!.GetValue<string>() != "copying") break;
            await Task.Delay(25);
        }
        Assert.Equal("done", status["copy"]!["state"]!.GetValue<string>());
        Assert.Equal(4, status["copy"]!["files"]!.GetValue<int>());
        Assert.True(status["present"]!.GetValue<bool>());
        var art = Path.Combine(c.H.DataDir, "map-assets");
        Assert.Equal(["data/find.json", "map/fonts/a.ttf", "map/gpu/d/0_0.webp", "map/tiles/0/0_0.webp"],
            Directory.EnumerateFiles(art, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(art, f).Replace('\\', '/')).Order(StringComparer.Ordinal));
        Assert.Empty(Directory.EnumerateDirectories(c.H.DataDir, "map-assets.copying-*"));
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/core/map-assets/map/tiles/0/0_0.webp")).StatusCode);
        // once it is here, a second copy is refused (nothing is overwritten)
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsync("/api/core/map-assets/copy")).StatusCode);
    }

    [Fact]
    public async Task Without_the_launchers_art_on_this_computer_the_copy_says_so()
    {
        using var t = new TempDir();
        Directory.CreateDirectory(Path.Combine(t.Path, "empty-launcher", "admin-assets"));
        await using var c = await LaneC.LaneC.StartAsync(extra: [new MapAssetsComponent(() => [Path.Combine(t.Path, "empty-launcher", "admin-assets")])]);
        using var owner = await c.UserAsync("olive", BuiltinRoles.Owner);
        await LaneC.LaneC.ReauthAsync(owner);
        Assert.False((await LaneC.LaneC.JsonAsync(await owner.GetAsync("/api/core/map-assets-status")))!["launcher"]!.GetValue<bool>());
        var r = await owner.PostAsync("/api/core/map-assets/copy");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Contains("isn't on this computer", (await LaneC.LaneC.JsonAsync(r))!["error"]!["message"]!.GetValue<string>());
        Assert.False(Directory.Exists(Path.Combine(c.H.DataDir, "map-assets")));
    }

    /// <summary>Review of 0.1.3: everyone signed in reads the copy's state (the Map page's notice shows it), so a copy that
    /// fails says so in plain words and names no folder of this computer; the full reason goes to the server's log.</summary>
    [Fact]
    public async Task A_copy_that_fails_names_no_folder_of_this_computer()
    {
        using var t = new TempDir();
        var source = Launcher(t);
        await using var c = await LaneC.LaneC.StartAsync(extra: [new MapAssetsComponent(() => [source])]);
        using var viewer = await c.UserAsync("vic", BuiltinRoles.Viewer);
        using var owner = await c.UserAsync("olive", BuiltinRoles.Owner);
        await LaneC.LaneC.ReauthAsync(owner);
        string body;
        // a file the copy cannot read (another program holds it): the copy stops
        using (new FileStream(Path.Combine(source, "map", "tiles", "0", "0_0.webp"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Equal(HttpStatusCode.Accepted, (await owner.PostAsync("/api/core/map-assets/copy")).StatusCode);
            for (var i = 0; ; i++)
            {
                body = await (await viewer.GetAsync("/api/core/map-assets-status")).Content.ReadAsStringAsync();
                if (!body.Contains("\"copying\"") || i > 200) break;
                await Task.Delay(25);
            }
        }
        var status = System.Text.Json.Nodes.JsonNode.Parse(body)!;
        Assert.Equal("failed", status["copy"]!["state"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(status["copy"]!["error"]!.GetValue<string>()));
        Assert.False(status["present"]!.GetValue<bool>());
        foreach (var folder in new[] { t.Path, source, c.H.DataDir, Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) })
            Assert.DoesNotContain(System.Text.Json.JsonSerializer.Serialize(folder).Trim('"'), body);
        Assert.DoesNotContain("0_0.webp", body);
        Assert.Empty(Directory.EnumerateDirectories(c.H.DataDir, "map-assets.copying-*"));
    }

    /// <summary>Review of 0.1.3: a copy cut short by a crash or a kill leaves its working folder (up to the copy's 2 GB)
    /// beside the data folder's map-assets; the next start deletes it, and nothing else.</summary>
    [Fact]
    public async Task A_copy_cut_short_leaves_nothing_behind_after_the_next_start()
    {
        using var t = new TempDir();
        var outside = Path.Combine(t.Path, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "keep.webp"), "x");
        string? stale = null, art = null;
        await using var h = await LaneHost.StartAsync(() => [new MapAssetsComponent(() => [])], (h, _) =>
        {
            stale = Path.Combine(h.DataDir, "map-assets.copying-0badf00d");
            Directory.CreateDirectory(Path.Combine(stale, "map", "tiles"));
            File.WriteAllText(Path.Combine(stale, "map", "tiles", "0_0.webp"), "x");
            art = Path.Combine(h.DataDir, "map-assets", "map");
            Directory.CreateDirectory(art);
            File.WriteAllText(Path.Combine(art, "keep.webp"), "x");
            if (!OperatingSystem.IsWindows()) Directory.CreateSymbolicLink(Path.Combine(h.DataDir, "map-assets.copying-link"), outside);
        });
        await Eventually.True(() => !Directory.Exists(stale), 5000, "the stale working folder deleted");
        Assert.True(File.Exists(Path.Combine(art!, "keep.webp")));
        Assert.True(File.Exists(Path.Combine(outside, "keep.webp")));
    }

    [Fact]
    public void A_folder_with_no_map_files_is_refused_and_leaves_nothing_behind()
    {
        using var t = new TempDir();
        var src = Path.Combine(t.Path, "src");
        Directory.CreateDirectory(Path.Combine(src, "map"));
        File.WriteAllText(Path.Combine(src, "map", "readme.txt"), "not art");
        var root = Path.Combine(t.Path, "data", "map-assets");
        Directory.CreateDirectory(Path.GetDirectoryName(root)!);
        var e = Assert.Throws<MapAssetsComponent.CopyRefused>(() => MapAssetsComponent.CopyFrom(src, root, CancellationToken.None));
        Assert.Contains("no map art", e.Message);
        Assert.False(Directory.Exists(root));
        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(t.Path, "data")));
    }
}
