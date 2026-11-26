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
        Assert.Contains($"[x] Running: https://127.0.0.1:{port}/", text);
        Assert.Contains("[ ] Owner account", text);

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
        Assert.Contains("[x] Owner account", Console(kiln, "status2", "kilnwarden status").ToJsonString());
        // the old first word (Kiln Web Admin's) still answers
        Assert.Contains("[x] Owner account", Console(kiln, "status3", "webadmin status").ToJsonString());
        Assert.Contains("Your owner account exists. Finish the setup in the panel", Console(kiln, "setup2", "kilnwarden setup").ToJsonString());
        // only the console may: a player in chat is refused (CommandPermission.Console)
        var asPlayer = Console(kiln, "player", "#as steam 76561190000000002 Gus\nkilnwarden setup");
        Assert.True(asPlayer["results"]![0]!["denied"]!.GetValue<bool>(), asPlayer.ToJsonString());
        kiln.Unload(mod);
    }

    /// <summary>0.1.2: the console-only hosted setup through Kiln's own command parser and console folder, as over FTP:
    /// the arguments reach the command, the answer holds the claim link, Kiln's log never does. The relay's and the CA's
    /// addresses point nowhere (nothing leaves this machine).</summary>
    [Fact]
    public void The_console_folder_runs_setup_hosted_with_its_name()
    {
        using var server = new TempDir();
        using var mods = new TempDir();
        var folder = ModFolder(mods);
        var port = FreePort();
        using var kiln = new KilnHarness(new KilnHarnessOptions { ServerDirectory = server.Path });
        kiln.WriteServerConfig($"{{\"queryPort\": {port - 100}}}");
        var data = Path.Combine(kiln.DataDirectory, "kilnwarden");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "relay.json"), "{\"enabled\": false, \"origin\": \"https://127.0.0.1:9\"}");
        File.WriteAllText(Path.Combine(data, "acme.json"), "{\"format\": 1, \"directory\": \"https://127.0.0.1:9/directory\"}");
        var mod = kiln.LoadFolder(folder);
        kiln.Step(2);
        Assert.True(mod.IsLoaded, mod.Error ?? kiln.DumpLogs());
        WaitForLog(kiln, "panel at ", 1);
        WaitForLog(kiln, "set up your panel (about 2 minutes)", 1);
        Assert.Contains(kiln.FindLogs("In the server console, type: kilnwarden setup hosted <a name for your panel>"), _ => true);

        string Answer(JsonNode r) => string.Join("\n", r["results"]![0]!["output"]!.AsArray().Select(o => o!.GetValue<string>()));
        var usage = Answer(Console(kiln, "usage", "kilnwarden setup hosted"));
        Assert.StartsWith("Type: kilnwarden setup hosted <name>", usage);
        Assert.StartsWith("Use at least 3 characters.", Answer(Console(kiln, "short", "kilnwarden setup hosted ab")));
        Assert.Contains("kilnwarden setup hosted <name>", Answer(Console(kiln, "other", "kilnwarden setup nonsense")));
        var answer = Answer(Console(kiln, "hosted", "kilnwarden setup hosted My-Panel"));
        Assert.Contains("your panel's address will be https://my-panel.kiln.nexus/", answer);
        Assert.Contains("https://claim.kiln.nexus/claim?v=1&host=my-panel.kiln.nexus&", answer);
        Assert.DoesNotContain("claim?v=1", kiln.DumpLogs());
        var relay = JsonNode.Parse(File.ReadAllText(Path.Combine(data, "relay.json")))!;
        Assert.Equal(("my-panel.kiln.nexus", true, "https://127.0.0.1:9"), (relay["host"]!.GetValue<string>(), relay["enabled"]!.GetValue<bool>(), relay["origin"]!.GetValue<string>()));
        var status = Answer(Console(kiln, "status", "kilnwarden status"));
        Assert.Contains("Connected to hosted access", status); // trying (nobody answers at the test relay)
        Assert.Contains("[x] Running: https://my-panel.kiln.nexus/", status);
        Assert.Contains("[ ] Owner account", status);
        kiln.Unload(mod);
    }

    /// <summary>The page at the panel's address while the old copy keeps it off: its whole HTTPS answer.</summary>
    private static async Task<string> NoticeAsync(int port)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, port);
        await using var ssl = new SslStream(tcp.GetStream(), false, (_, _, _, _) => true);
        await ssl.AuthenticateAsClientAsync("localhost");
        await ssl.WriteAsync(System.Text.Encoding.ASCII.GetBytes($"GET / HTTP/1.1\r\nHost: localhost:{port}\r\n\r\n"));
        using var r = new StreamReader(ssl);
        return await r.ReadToEndAsync();
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
    public async Task A_copy_of_the_old_mod_that_Kiln_runs_keeps_the_panel_off_and_its_data_folder_in_place()
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
        Assert.True(kiln.FindLogs("Kiln Web Admin, its old name, is still installed. Stop the server, delete the folder kiln/mods/kiln-webadmin").Any(), kiln.DumpLogs());
        Assert.False(kiln.HasLog("starting in the background", mod.Id), kiln.DumpLogs());
        Assert.Equal("{}", File.ReadAllText(Path.Combine(oldData, "accounts.json")));
        Assert.False(Directory.Exists(Path.Combine(kiln.DataDirectory, "kilnwarden")));
        // 0.1.2: the console and the panel's own address say exactly what to delete, instead of nothing answering
        var answer = Console(kiln, "status", "kilnwarden status").ToJsonString();
        Assert.Contains("delete the folder kiln/mods/kiln-webadmin", answer);
        Assert.Contains("delete the folder kiln/mods/kiln-webadmin", Console(kiln, "setup", "kilnwarden setup").ToJsonString());
        var page = await NoticeAsync(port);
        Assert.Contains("503", page.Split('\n')[0]);
        Assert.Contains("delete the folder kiln/mods/kiln-webadmin", page);
        kiln.Unload(mod);
        using (var free = new TcpListener(IPAddress.Loopback, port))
        {
            free.Start(); // the notice let go of the panel's port
            free.Stop();
        }
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
