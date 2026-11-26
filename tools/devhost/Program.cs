// SPDX-License-Identifier: MIT
// The panel without the game: the real host (core and every first-party component the mod starts) on a loopback port, the built web app, and the KAP
// client pointed at <server>/kiln/data/kiln-admin (a Kiln.Admin, or web/e2e/kap-mock.py). For the web app's development
// (vite's dev server proxies /api here: KWA_DEV_API=http://127.0.0.1:<port>) and its end-to-end tests (web/e2e).
//
//   dotnet run --project tools/devhost -- --server <dir> [--port 15800] [--web web/dist] [--tls off|self-signed]
//       [--relay-origin <https origin>] [--relay-zone <zone>] [--acme-directory <https url>] [--acme-wait <seconds>]
//       [--probe-via <host:port>] [--launcher-assets <dir>] [--broker-origin <https origin>]
//
// The setup wizard's end-to-end runs (0.1.2): --relay-origin and --relay-zone seed relay.json (off) with a test relay,
// --acme-directory and --acme-wait seed acme.json with a test CA, as a harness does before the owner touches anything
// (the wizard keeps both); --probe-via sends the wizard's address check to a test relay's browser side. The dev host
// also runs the server console's kilnwarden commands from <dir>/kiln/control/console/*.cmd, answering in
// <name>.result.json as Kiln's console folder does, so the console-only setup runs here too.
// 0.1.3: --launcher-assets names the folder that stands in for the Kiln Launcher's admin-assets (the Map page's "copy
// the launcher's map art"); without it the dev host finds none, whatever is on this PC. --broker-origin seeds identity.json
// (Discord sign-in off) with a test broker, as --relay-origin does for the relay: the wizard's "Sign in with Discord"
// then switches it on and links through that broker (a build pinned to its key: hosted access's end-to-end runs).
//
// <dir> is a server folder of its own (made if missing): kiln/data/kilnwarden gets settings.json (this port, TLS as
// given, loopback) and auth.json (sign-in attempt budgets raised for tests) unless they exist. The log goes to
// stdout and <dir>/devhost.log (the first-run setup link is in it). Ctrl-C or SIGTERM stops it the OnUnload way.
using System.Runtime.InteropServices;
using Kilnwarden;
using Kilnwarden.Auth;
using Kilnwarden.Config;
using Kilnwarden.Hosting;
using Kilnwarden.Storage;

string? server = null, web = null, tls = "off", relayOrigin = null, relayZone = null, acmeDirectory = null, probeVia = null, launcherAssets = null, brokerOrigin = null;
int? acmeWait = null;
var port = 15800;
for (var i = 0; i < args.Length; i++)
{
    string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
    switch (args[i])
    {
        case "--server": server = Next(); break;
        case "--port": port = int.Parse(Next()); break;
        case "--web": web = Next(); break;
        case "--tls": tls = Next(); break;
        case "--relay-origin": relayOrigin = Next(); break;
        case "--relay-zone": relayZone = Next(); break;
        case "--acme-directory": acmeDirectory = Next(); break;
        case "--acme-wait": acmeWait = int.Parse(Next()); break;
        case "--probe-via": probeVia = Next(); break;
        case "--launcher-assets": launcherAssets = Path.GetFullPath(Next()); break;
        case "--broker-origin": brokerOrigin = Next(); break;
        default:
            Console.Error.WriteLine($"unknown argument {args[i]}\nusage: devhost --server <dir> [--port 15800] [--web web/dist] [--tls off|self-signed]");
            return 2;
    }
}
if (server is null)
{
    Console.Error.WriteLine("usage: devhost --server <dir> [--port 15800] [--web web/dist] [--tls off|self-signed]");
    return 2;
}
server = Path.GetFullPath(server);
var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
web = Path.GetFullPath(web ?? Path.Combine(repo, "web", "dist"));
var data = Path.Combine(server, "kiln", "data", Product.ModId);
var mod = Path.Combine(server, "kiln", "mods", Product.ModId);
Directory.CreateDirectory(data);
var store = new DataStore(data);
if (!store.Exists("settings.json"))
{
    var s = new ServerSettings { Port = port };
    s.Tls.Mode = tls == "off" ? TlsMode.Off : TlsMode.SelfSigned;
    store.WriteJson("settings.json", s);
}
if (!store.Exists("auth.json")) store.WriteJson("auth.json", new AuthSettings
{
    // This disposable host is not shipped. Repeated sign-in/step-up scenarios share one loopback address and account;
    // use the supported test budgets without changing the mod's safe defaults.
    SignIn = new() { AttemptsPerMinutePerIp = 600, AttemptsPerMinutePerAccount = 60, IpFreeFailures = 1000, AccountFreeFailures = 100 },
});
if ((relayOrigin ?? relayZone) is not null && !store.Exists("relay.json"))
    store.WriteJson("relay.json", new Kilnwarden.Relay.RelaySettings { Enabled = false, Origin = relayOrigin ?? "https://frostdev.io", Zone = relayZone ?? "kiln.nexus" });
if (brokerOrigin is not null && !store.Exists("identity.json"))
    store.WriteJson("identity.json", new Kilnwarden.Identity.IdentitySettings { Discord = new Kilnwarden.Identity.DiscordSettings { Broker = brokerOrigin } });
if ((acmeDirectory is not null || acmeWait is not null) && !store.Exists("acme.json"))
    store.WriteJson("acme.json", new Kilnwarden.Tls.Acme.AcmeSettings
    {
        Directory = acmeDirectory ?? Kilnwarden.Tls.Acme.AcmeSettings.LetsEncrypt, Dns01PropagationSeconds = acmeWait ?? 60,
        // a test CA answers at once; a failure is retried after a minute, not an hour
        MinRetryMinutes = acmeDirectory is null ? 60 : 1,
    });
// the mod folder's web/: a copy of the build (StaticFiles reads it once at start)
var webDst = Path.Combine(mod, "web");
if (Directory.Exists(webDst)) Directory.Delete(webDst, true);
if (File.Exists(Path.Combine(web, "index.html")))
{
    foreach (var f in Directory.EnumerateFiles(web, "*", SearchOption.AllDirectories))
    {
        var dst = Path.Combine(webDst, Path.GetRelativePath(web, f));
        Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
        File.Copy(f, dst);
    }
}
else Console.Error.WriteLine($"devhost: no web app in {web} (bun run build in web/): the placeholder page answers");

var logFile = new StreamWriter(Path.Combine(server, "devhost.log"), append: true) { AutoFlush = true };
var log = new ConsoleLog(logFile);
var components = FirstPartyComponents.All(); // the mod's own component list (auth, identity, ACME, branding, ops, lane C, setup)
IReadOnlyList<string> launcherFolders = launcherAssets is null ? [] : [launcherAssets];
components = [.. components.Select(c => c is Kilnwarden.Static.MapAssetsComponent ? new Kilnwarden.Static.MapAssetsComponent(() => launcherFolders) : c)];
if (probeVia is { } via)
{
    var (viaHost, viaPort) = (via[..via.LastIndexOf(':')], int.Parse(via[(via.LastIndexOf(':') + 1)..]));
    var probe = new Kilnwarden.Setup.SelfProbe
    {
        Connect = async (_, _, ct) =>
        {
            var tcp = new System.Net.Sockets.TcpClient();
            await tcp.ConnectAsync(viaHost, viaPort, ct);
            return tcp.GetStream();
        },
    };
    components = [.. components.Select(c => c is Kilnwarden.Setup.SetupComponent ? new Kilnwarden.Setup.SetupComponent(new Kilnwarden.Setup.SetupOptions { Probe = probe, ProbeEvery = TimeSpan.FromSeconds(2) }) : c)];
}
var host = new WebAdminHost(new WebAdminEnvironment
{
    DataDirectory = data, ModDirectory = mod, ServerDirectory = server, Log = log, KilnVersion = "devhost",
}, components);
host.Start();
try
{
    await host.Started;
}
catch (Exception e)
{
    log.Error("devhost: the start failed", e);
    return 1;
}
log.Info($"devhost: ready on port {port} (server folder {server})");

// Kiln's console folder: kiln/control/console/<name>.cmd -> <name>.result.json (the kilnwarden commands only)
var consoleDir = Path.Combine(server, "kiln", "control", "console");
Directory.CreateDirectory(consoleDir);
using var consoleTimer = new Timer(_ =>
{
    foreach (var cmd in Directory.EnumerateFiles(consoleDir, "*.cmd"))
    {
        try
        {
            var lines = File.ReadAllLines(cmd).Where(l => l.Trim().Length > 0).ToList();
            File.Delete(cmd);
            var results = new System.Text.Json.Nodes.JsonArray();
            foreach (var line in lines)
            {
                var answer = ConsoleCommands.Run(host, line.Trim());
                results.Add(new System.Text.Json.Nodes.JsonObject
                {
                    ["line"] = line.Trim(), ["ok"] = answer is not null,
                    ["output"] = new System.Text.Json.Nodes.JsonArray([.. (answer ?? "unknown command").Split('\n').Select(o => (System.Text.Json.Nodes.JsonNode)o)]),
                });
            }
            var name = Path.GetFileNameWithoutExtension(cmd);
            var tmp = Path.Combine(consoleDir, name + ".result.json.tmp");
            File.WriteAllText(tmp, new System.Text.Json.Nodes.JsonObject { ["file"] = name + ".cmd", ["ok"] = true, ["results"] = results }.ToJsonString());
            File.Move(tmp, Path.Combine(consoleDir, name + ".result.json"), overwrite: true);
            log.Info($"devhost: console {name}.cmd ({lines.Count} line(s))");
        }
        catch (IOException)
        {
            // still being written: the next tick
        }
    }
}, null, 500, 500);

var done = new TaskCompletionSource();
using var term = PosixSignalRegistration.Create(PosixSignal.SIGTERM, c =>
{
    c.Cancel = true;
    done.TrySetResult();
});
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    done.TrySetResult();
};
await done.Task;
log.Info("devhost: " + host.Stop());
return 0;

internal sealed class ConsoleLog(StreamWriter file) : IWebAdminLog
{
    private readonly object _gate = new();

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} {level} {message}";
        lock (_gate)
        {
            Console.WriteLine(line);
            file.WriteLine(line);
        }
    }

    public void Debug(string message) => Write("D", message);
    public void Info(string message) => Write("I", message);
    public void Warn(string message) => Write("W", message);
    public void Error(string message, Exception? error = null) => Write("E", error is null ? message : $"{message} :: {error}");
}
