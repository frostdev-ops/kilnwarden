// SPDX-License-Identifier: MIT
// The built mod folder in Kiln's test harness (no game), loaded as Kiln's deploy copies it: it starts in the background,
// serves HTTPS on loopback through kwtls, survives five reloads with live connections and no leaked load context, and
// releases its port on unload.
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.Json.Nodes;
using Kiln.TestKit;
using Kilnwarden.Offline.Server;
using Kilnwarden.Offline.Support;

namespace Kilnwarden.Offline.Mod;

public sealed class ModLoadTests
{
    /// <summary>A copy of src/Kilnwarden's output folder (the deployable mod folder) with the host build of kwtls and
    /// without the kiln-admin dependency (the kit has no Kiln.Admin).</summary>
    private static string ModFolder(TempDir dir)
    {
        var cfg = new DirectoryInfo(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)).Parent!.Name; // bin/<cfg>/net10.0
        var built = Path.Combine(Native.RepoRoot, "src", "Kilnwarden", "bin", cfg, "net10.0");
        var dst = Path.Combine(dir.Path, "kilnwarden");
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(built)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)));
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(dst, "kiln.mod.json")))!.AsObject();
        manifest["dependencies"]!.AsObject().Remove("kiln-admin");
        File.WriteAllText(Path.Combine(dst, "kiln.mod.json"), manifest.ToJsonString());
        // on Windows the build output already holds kwtls-<version>.dll (the same dist/ file)
        if (File.Exists(Native.KwtlsPath)) File.Copy(Native.KwtlsPath, Path.Combine(dst, Path.GetFileName(Native.KwtlsPath)), overwrite: true);
        return dst;
    }

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    private static void WaitForLog(KilnHarness kiln, string text, int count)
    {
        for (var i = 0; i < 300 && kiln.FindLogs(text).Count() < count; i++) Thread.Sleep(50);
        Assert.True(kiln.FindLogs(text).Count() >= count, kiln.DumpLogs());
    }

    private static async Task<(SslProtocols, RawResponse, SslStream)> HealthAsync(int port)
    {
        var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, port);
        var ssl = new SslStream(tcp.GetStream(), false, (_, _, _, _) => true);
        await ssl.AuthenticateAsClientAsync("localhost");
        var r = await ServerFixture.SendAsync(ssl, "GET /api/core/health HTTP/1.1\r\nHost: localhost\r\n\r\n");
        return (ssl.SslProtocol, r, ssl);
    }

    /// <summary>Kiln's console folder (kiln/control/console/*.cmd, which works over FTP): the console runs it, the answer
    /// is &lt;name&gt;.result.json beside it.</summary>
    private static System.Text.Json.Nodes.JsonNode Console(KilnHarness kiln, string name, string line)
    {
        var dir = Path.Combine(kiln.ControlDirectory, "console");
        Directory.CreateDirectory(dir);
        var result = Path.Combine(dir, name + ".result.json");
        File.Delete(result);
        // as over FTP: upload under another name, then rename (Kiln never reads a half-uploaded file)
        File.WriteAllText(Path.Combine(dir, name + ".txt"), line + "\n");
        File.Move(Path.Combine(dir, name + ".txt"), Path.Combine(dir, name + ".cmd"));
        for (var i = 0; i < 200 && !File.Exists(result); i++)
        {
            kiln.Step(1);
            Thread.Sleep(25);
        }
        Assert.True(File.Exists(result), "no answer in the console folder\n" + kiln.DumpLogs());
        for (var i = 0; ; i++)
        {
            try { return JsonNode.Parse(File.ReadAllText(result))!; }
            catch (System.Text.Json.JsonException) when (i < 20) { Thread.Sleep(50); }
        }
    }

    /// <summary>A rented game server with FTP and the host's web console only: the owner drops a console file, reads the
    /// setup link from the answer file and creates the owner; kilnwarden status says where the panel is.</summary>
    [Fact]
    public async Task The_console_folder_gives_the_first_setup_link_with_file_access_alone()
    {
        using var server = new TempDir();
        using var mods = new TempDir();
        var folder = ModFolder(mods);
        var port = FreePort();
        using var kiln = new KilnHarness(new KilnHarnessOptions { ServerDirectory = server.Path });
        kiln.WriteServerConfig($"{{\"queryPort\": {port - 100}}}");
        var mod = kiln.LoadFolder(folder);
        kiln.Step(2);
        Assert.True(mod.IsLoaded, mod.Error ?? kiln.DumpLogs());
        WaitForLog(kiln, "started in", 1);
        WaitForLog(kiln, "panel at ", 1);

        var status = Console(kiln, "status", "kilnwarden status");
        var text = status.ToJsonString();
        Assert.True(status["ok"]!.GetValue<bool>(), text);
        Assert.Contains($"listening at https://127.0.0.1:{port}/", text);
        Assert.Contains("owner account: not yet", text);

        var setup = Console(kiln, "setup", "kilnwarden setup");
        var line = string.Join("\n", setup["results"]![0]!["output"]!.AsArray().Select(o => o!.GetValue<string>()));
        Assert.True(setup["ok"]!.GetValue<bool>(), setup.ToJsonString());
        var token = System.Text.RegularExpressions.Regex.Match(line, @"/setup#token=([A-Za-z0-9_-]{43})").Groups[1].Value;
        Assert.Equal(43, token.Length);

        // the browser's setup request, over the panel's own TLS
        var body = $"{{\"token\":\"{token}\",\"name\":\"owner\",\"password\":\"correct horse battery staple 42\"}}";
        using (var tcp = new TcpClient())
        {
            await tcp.ConnectAsync(IPAddress.Loopback, port);
            using var ssl = new SslStream(tcp.GetStream(), false, (_, _, _, _) => true);
            await ssl.AuthenticateAsClientAsync("localhost");
            var r = await ServerFixture.SendAsync(ssl, $"POST /api/auth/setup HTTP/1.1\r\nHost: localhost:{port}\r\nOrigin: https://localhost:{port}\r\n" +
                $"Content-Type: application/json\r\nContent-Length: {body.Length}\r\n\r\n{body}");
            Assert.Equal(200, r.Status);
        }
        Assert.Contains("owner account: yes", Console(kiln, "status2", "kilnwarden status").ToJsonString());
        // the old first word (Kiln Web Admin's) still answers
        Assert.Contains("owner account: yes", Console(kiln, "status3", "webadmin status").ToJsonString());
        Assert.Contains("The owner account exists", Console(kiln, "setup2", "kilnwarden setup").ToJsonString());
        // only the console may: a player in chat is refused (CommandPermission.Console)
        var asPlayer = Console(kiln, "player", "#as steam 76561190000000002 Gus\nkilnwarden setup");
        Assert.True(asPlayer["results"]![0]!["denied"]!.GetValue<bool>(), asPlayer.ToJsonString());
        kiln.Unload(mod);
    }

    /// <summary>Stands in for a Kiln Web Admin 0.1.0 test build (mod id kiln-webadmin) that Kiln runs.</summary>
    public sealed class OldWebAdminStub : Kiln.Mod
    {
        public override void OnLoad()
        {
        }
    }

    /// <summary>A copy of the old mod that Kiln has loaded keeps the panel off, even where the folder check cannot see it
    /// (a profile entry outside kiln/mods, or a folder switched off after it loaded): Kilnwarden neither moves the old
    /// data folder from under it nor takes the port.</summary>
    [Fact]
    public void A_copy_of_the_old_mod_that_Kiln_runs_keeps_the_panel_off_and_its_data_folder_in_place()
    {
        using var server = new TempDir();
        using var mods = new TempDir();
        var folder = ModFolder(mods);
        var port = FreePort();
        using var kiln = new KilnHarness(new KilnHarnessOptions { ServerDirectory = server.Path });
        kiln.WriteServerConfig($"{{\"queryPort\": {port - 100}}}");
        var old = kiln.Load<OldWebAdminStub>(new ModManifest(Kilnwarden.Hosting.LegacyInstall.OldModId));
        kiln.Step(2);
        Assert.True(old.IsLoaded, old.Error ?? kiln.DumpLogs());
        File.WriteAllText(Path.Combine(kiln.ModsDirectory, Kilnwarden.Hosting.LegacyInstall.OldModId, ".disabled"), "");
        var oldData = Path.Combine(kiln.DataDirectory, Kilnwarden.Hosting.LegacyInstall.OldModId);
        Directory.CreateDirectory(oldData);
        File.WriteAllText(Path.Combine(oldData, "accounts.json"), "{}");

        var mod = kiln.LoadFolder(folder);
        kiln.Step(2);
        Assert.True(kiln.FindLogs("this panel's old name, is still installed").Any(), kiln.DumpLogs());
        Assert.False(kiln.HasLog("starting in the background", mod.Id), kiln.DumpLogs());
        Assert.Equal("{}", File.ReadAllText(Path.Combine(oldData, "accounts.json")));
        Assert.False(Directory.Exists(Path.Combine(kiln.DataDirectory, "kilnwarden")));
        using (var free = new TcpListener(IPAddress.Loopback, port))
        {
            free.Start(); // nobody took the panel's port
            free.Stop();
        }
        kiln.Unload(mod);
        kiln.Unload(old);
    }

    [Fact]
    public async Task Loads_serves_https_reloads_five_times_and_unloads_cleanly()
    {
        using var server = new TempDir();
        using var mods = new TempDir();
        var folder = ModFolder(mods);
        var port = FreePort();
        using var kiln = new KilnHarness(new KilnHarnessOptions { ServerDirectory = server.Path });
        kiln.WriteServerConfig($"{{\"queryPort\": {port - 100}}}");
        var mod = kiln.LoadFolder(folder);
        kiln.Step(2);
        Assert.True(mod.IsLoaded, mod.Error ?? kiln.DumpLogs());
        Assert.True(kiln.HasLog("starting in the background", mod.Id), kiln.DumpLogs());
        WaitForLog(kiln, "started in", 1);

        var (proto, r, live) = await HealthAsync(port);
        Assert.Equal(200, r.Status);
        if (File.Exists(Native.KwtlsPath))
        {
            Assert.Equal(SslProtocols.Tls13, proto);
            Assert.True(kiln.HasLog("kwtls " + Product.KwtlsVersion, mod.Id), kiln.DumpLogs());
        }

        var current = mod;
        for (var i = 2; i <= 6; i++)
        {
            current = kiln.Reload(current);   // throws ModLeakException when the old load context stays alive
            kiln.Step(2);
            Assert.True(current.IsLoaded, current.Error ?? kiln.DumpLogs());
            WaitForLog(kiln, "started in", i);
            Assert.True(await live.ClosedWithinAsync(2000));   // the connection of the previous instance was closed
            live.Dispose();
            (_, r, live) = await HealthAsync(port);
            Assert.Equal(200, r.Status);
        }
        live.Dispose();
        Assert.Empty(kiln.LogsOf(current).Where(l => l.Level == Kiln.LogLevel.Error).Select(l => l.Text));
        kiln.Unload(current);
        using var again = new TcpListener(IPAddress.Loopback, port);
        again.Start();   // the port is free after the unload
        again.Stop();
        Assert.True(kiln.HasLog("OnUnload", null) || kiln.FindLogs("OnUnload").Any(), kiln.DumpLogs());
    }
}
