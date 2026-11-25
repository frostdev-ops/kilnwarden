// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using Kilnwarden.Config;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Modules;
using Kilnwarden.Offline.Server;
using Kilnwarden.Offline.Support;
using Kilnwarden.Routing;
using Kilnwarden.Security;
using Kilnwarden.Storage;

namespace Kilnwarden.Offline.Hosting;

public class WebAdminHostTests
{
    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    private sealed class HostRig : IDisposable
    {
        public readonly TempDir Data = new();
        public readonly TempDir Server = new();
        public readonly TestLog Log = new();
        public readonly WebAdminHost Host;
        public readonly List<string> Events;

        public HostRig(Action<ServerSettings> edit, string? modDir = null, List<string>? events = null, int? queryPort = null,
            params IWebAdminComponent[] components)
        {
            if (queryPort is { } q) File.WriteAllText(Server.File("enshrouded_server.json"), $"{{\"queryPort\": {q}}}");
            var s = new ServerSettings();
            edit(s);
            new DataStore(Data.Path).WriteJson("settings.json", s);
            Events = events ?? [];
            Host = new WebAdminHost(new WebAdminEnvironment
            {
                DataDirectory = Data.Path, ModDirectory = modDir ?? Server.Path, ServerDirectory = Server.Path, Log = Log, KilnVersion = "test",
            }, components);
            Host.Start();
            Host.Started.Wait(30_000);
        }

        public void Dispose()
        {
            Host.Stop();
            Data.Dispose();
            Server.Dispose();
        }
    }

    private sealed class Owner : ISetupState
    {
        private bool _exists;
        public bool OwnerExists => _exists;
        public event Action? Changed;

        public void Create()
        {
            _exists = true;
            Changed?.Invoke();
        }
    }

    private sealed class Recorder(string name, List<string> events, Owner? owner = null) : IWebAdminComponent
    {
        public string Name => name;

        public void Configure(WebAdminBuilder b)
        {
            events.Add("configure " + name);
            if (owner is not null) b.SetupState = owner;
            b.Modules.Register(new ModuleSpec { Id = name, Title = name }, "test")
                .MapGet("/ping", RouteAccess.Public, _ => ValueTask.FromResult(HttpResponse.Text(200, "pong " + name)));
        }

        public Task StartAsync(WebAdminServices services, CancellationToken ct)
        {
            events.Add("start " + name);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct)
        {
            events.Add("stop " + name);
            return Task.CompletedTask;
        }
    }

    private static async Task<RawResponse> GetAsync(int port, string path)
    {
        using var c = new TcpClient();
        await c.ConnectAsync(IPAddress.Loopback, port);
        return await ServerFixture.SendAsync(c.GetStream(), $"GET {path} HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n");
    }

    [Fact]
    public async Task Plain_loopback_host_runs_components_in_order_and_serves()
    {
        var port = FreePort();
        var events = new List<string>();
        using (var rig = new HostRig(s => { s.Port = port; s.Tls.Mode = TlsMode.Off; }, events: events,
                   components: [new Recorder("a", events), new Recorder("b", events)]))
        {
            Assert.True(rig.Host.Started.IsCompletedSuccessfully, rig.Log.ToString());
            Assert.Equal("pong b", (await GetAsync(port, "/api/b/ping")).Body);
            Assert.Equal("{\"ok\":true}", (await GetAsync(port, "/api/core/health")).Body);
            Assert.Equal(401, (await GetAsync(port, "/api/core/info")).Status);
            var page = await GetAsync(port, "/");
            Assert.Contains(Product.Name, page.Body);
            Assert.Equal([new Uri($"http://127.0.0.1:{port}/"), new Uri($"http://localhost:{port}/")], rig.Host.Endpoints);
        }
        Assert.Equal(["configure a", "configure b", "start a", "start b", "stop b", "stop a"], events);
        using var again = new TcpListener(IPAddress.Loopback, port);
        again.Start(); // released
        again.Stop();
    }

    [Fact]
    public void Port_defaults_to_query_port_plus_100()
    {
        var port = FreePort();
        if (port < 1124) return;
        using var rig = new HostRig(s => s.Tls.Mode = TlsMode.Off, queryPort: port - 100);
        Assert.Equal(port, rig.Host.Server!.LocalEndPoint.Port);
    }

    [Fact]
    public async Task Default_settings_are_https_on_loopback_with_kwtls_ecdsa()
    {
        Native.Kwtls();
        var port = FreePort();
        using var rig = new HostRig(s => s.Port = port, modDir: Path.GetDirectoryName(Native.KwtlsPath));
        Assert.True(rig.Host.Started.IsCompletedSuccessfully, rig.Log.ToString());
        Assert.StartsWith("kwtls " + Product.KwtlsVersion, rig.Host.TlsEngine);
        Assert.Equal("ecdsa-p256", rig.Host.Certificate!.KeyType);
        Assert.Equal(IPAddress.Loopback, rig.Host.Server!.LocalEndPoint.Address);
        Assert.True(rig.Log.Has("SHA-256 " + rig.Host.Certificate.Sha256));
        var endpoint = rig.Host.Data.ReadJson<System.Text.Json.Nodes.JsonObject>("launcher-endpoint.json")!;
        Assert.Equal("127.0.0.1", endpoint["host"]!.GetValue<string>());
        Assert.Equal(port, endpoint["port"]!.GetValue<int>());
        Assert.Equal(rig.Host.Certificate.Sha256, endpoint["sha256"]!.GetValue<string>());
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, port);
        await using var ssl = new SslStream(tcp.GetStream(), false, (_, _, _, _) => true);
        await ssl.AuthenticateAsClientAsync("localhost");
        Assert.Equal(SslProtocols.Tls13, ssl.SslProtocol);
        var r = await ServerFixture.SendAsync(ssl, "GET /api/core/health HTTP/1.1\r\nHost: localhost\r\n\r\n");
        Assert.Equal(200, r.Status);
        Assert.Null(r.Headers.GetValueOrDefault("Strict-Transport-Security"));
        tcp.Dispose();
        rig.Host.Stop();
        Assert.False(rig.Host.Data.Exists("launcher-endpoint.json"));
    }

    [Fact]
    public void Without_kwtls_it_falls_back_to_sslstream_rsa()
    {
        var port = FreePort();
        using var rig = new HostRig(s => s.Port = port);
        Assert.True(rig.Host.Started.IsCompletedSuccessfully, rig.Log.ToString());
        Assert.Equal("sslstream", rig.Host.TlsEngine);
        Assert.Equal("rsa-2048", rig.Host.Certificate!.KeyType);
        Assert.True(rig.Log.Has("falling back to SslStream"));
    }

    [Fact]
    public async Task Public_bind_waits_for_the_owner_then_rebinds_with_a_warning()
    {
        var port = FreePort();
        var owner = new Owner();
        using var rig = new HostRig(s => { s.Port = port; s.Bind = "0.0.0.0"; s.Tls.Mode = TlsMode.Off; s.ReverseProxy.Enabled = true; },
            components: new Recorder("x", [], owner));
        Assert.Equal(IPAddress.Loopback, rig.Host.Server!.LocalEndPoint.Address);
        Assert.True(rig.Log.Has("waits until the owner account exists"));
        Assert.False(rig.Log.Has("PUBLIC:"));
        owner.Create();
        await rig.Host.Restarted;
        Assert.Equal(IPAddress.Any, rig.Host.Server!.LocalEndPoint.Address);
        Assert.True(rig.Log.Has("PUBLIC:"));
        Assert.Equal("pong x", (await GetAsync(port, "/api/x/ping")).Body);
    }

    [Fact]
    public void Plain_http_on_a_public_address_needs_the_proxy_switch()
    {
        var port = FreePort();
        var owner = new Owner();
        owner.Create();
        using var rig = new HostRig(s => { s.Port = port; s.Bind = "0.0.0.0"; s.Tls.Mode = TlsMode.Off; }, components: new Recorder("x", [], owner));
        Assert.Equal(IPAddress.Loopback, rig.Host.Server!.LocalEndPoint.Address);
        Assert.True(rig.Log.Has("needs reverseProxy.enabled"));
    }

    [Fact]
    public async Task Settings_reload_moves_the_listener_and_limits_apply_live()
    {
        var port = FreePort();
        var port2 = FreePort();
        using var rig = new HostRig(s => { s.Port = port; s.Tls.Mode = TlsMode.Off; });
        var s = new DataStore(rig.Data.Path).ReadJson<ServerSettings>("settings.json")!;
        s.Port = port2;
        new DataStore(rig.Data.Path).WriteJson("settings.json", s);
        Assert.True(rig.Host.Settings.Poll());
        await rig.Host.Restarted;
        Assert.Equal(port2, rig.Host.Server!.LocalEndPoint.Port);
        Assert.Equal(200, (await GetAsync(port2, "/api/core/health")).Status);
        await Assert.ThrowsAnyAsync<SocketException>(() => GetAsync(port, "/api/core/health"));

        s.Limits.RequestsPerSecondPerIp = 0.001;
        s.Limits.RequestBurstPerIp = 1;
        new DataStore(rig.Data.Path).WriteJson("settings.json", s);
        Assert.True(rig.Host.Settings.Poll());
        await rig.Host.Restarted;
        Assert.Equal(port2, rig.Host.Server!.LocalEndPoint.Port); // same listener
        var statuses = new[] { (await GetAsync(port2, "/api/core/health")).Status, (await GetAsync(port2, "/api/core/health")).Status };
        Assert.Contains(429, statuses);
    }

    [Fact]
    public async Task Start_stop_five_times_on_one_port()
    {
        var port = FreePort();
        for (var i = 0; i < 5; i++)
        {
            using var rig = new HostRig(s => { s.Port = port; s.Tls.Mode = TlsMode.Off; });
            Assert.True(rig.Host.Started.IsCompletedSuccessfully, rig.Log.ToString());
            using var c = new TcpClient();
            await c.ConnectAsync(IPAddress.Loopback, port);
            await c.GetStream().WriteAsync("GET /api/core/health HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n"u8.ToArray());
            Assert.Equal(200, (await RawResponse.ReadAsync(c.GetStream())).Status);
            // the keep-alive connection is still open when the host stops
        }
    }

    [Fact]
    public async Task Core_routes_with_a_signed_in_owner()
    {
        var port = FreePort();
        var gate = new AllowAs(new TestPrincipal("owner", "*"));
        using var rig = new HostRig(s => { s.Port = port; s.Tls.Mode = TlsMode.Off; }, components: gate);
        var info = await GetAsync(port, "/api/core/info");
        Assert.Equal(200, info.Status);
        Assert.Contains("\"engine\":\"off\"", info.Body);
        Assert.Contains("\"id\":\"core\"", (await GetAsync(port, "/api/core/modules")).Body);
        var settings = await GetAsync(port, "/api/core/settings");
        Assert.Contains("\"bind\":\"127.0.0.1\"", settings.Body);
    }

    /// <summary>A stand-in for the auth component: every request is the given user.</summary>
    private sealed class AllowAs(IPrincipal user) : IWebAdminComponent
    {
        public string Name => "allow-as";

        public void Configure(WebAdminBuilder b) =>
            b.Use(PipelineStage.Session, new Middleware((ctx, next) =>
            {
                ctx.User = user;
                return next(ctx);
            }));
    }
}
