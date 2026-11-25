// SPDX-License-Identifier: MIT
// The panel without the game: the real host (core and every first-party component the mod starts) on a loopback port, the built web app, and the KAP
// client pointed at <server>/kiln/data/kiln-admin (a Kiln.Admin, or web/e2e/kap-mock.py). For the web app's development
// (vite's dev server proxies /api here: KWA_DEV_API=http://127.0.0.1:<port>) and its end-to-end tests (web/e2e).
//
//   dotnet run --project tools/devhost -- --server <dir> [--port 15800] [--web web/dist] [--tls off|self-signed]
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

string? server = null, web = null, tls = "off";
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
var host = new WebAdminHost(new WebAdminEnvironment
{
    DataDirectory = data, ModDirectory = mod, ServerDirectory = server, Log = log, KilnVersion = "devhost",
}, FirstPartyComponents.All()); // the mod's own component list (auth, identity, ACME, branding, ops, lane C)
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
