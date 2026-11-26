// SPDX-License-Identifier: MIT
// The rented-server path from the server console alone (0.1.2): kilnwarden setup hosted <name> sets everything, gives the
// claim link and opens the hour in which the relay may connect before the owner exists; kilnwarden setup then gives the
// link to create the owner over hosted access, but only once the name has its trusted certificate. kiln.log's link never
// creates the owner through the relay; the console's hosted link does, over the panel's own TLS.
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Kilnwarden.Auth;
using Kilnwarden.Config;
using Kilnwarden.Hosting;
using Kilnwarden.Offline.Acme;
using Kilnwarden.Offline.Support;
using Kilnwarden.Relay;
using Kilnwarden.Setup;
using Kilnwarden.Tls.Acme;

namespace Kilnwarden.Offline.Setup;

[Collection("setup")]
public sealed class ConsoleSetupTests
{
    private const string Host = "alpha.kiln.nexus";
    private static readonly AcmePolling Fast = new(TimeSpan.FromMilliseconds(20), 50, TimeSpan.FromSeconds(10));

    private static string Token(string text) => Regex.Match(text, @"setup#token=([A-Za-z0-9_-]{43})").Groups[1].Value;

    /// <summary>A browser that reaches the panel through the relay: its bytes go in as a relayed stream announced from a
    /// public address, TLS with the name as SNI (the certificate the name gets).</summary>
    private static async Task<Stream> ThroughRelay(WebAdminHost host)
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var c = new TcpClient();
        var accept = l.AcceptTcpClientAsync();
        await c.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)l.LocalEndpoint).Port);
        var serverEnd = (await accept).GetStream();
        l.Stop();
        _ = host.Server!.AdmitRelayAsync(serverEnd, new IPEndPoint(IPAddress.Parse("8.8.8.8"), 50123));
        return c.GetStream();
    }

    private static async Task<RawResponse> SetupThroughRelay(WebAdminHost host, string token)
    {
        await using var ssl = new SslStream(await ThroughRelay(host), false, (_, _, _, _) => true);
        await ssl.AuthenticateAsClientAsync(Host);
        var body = $"{{\"token\":\"{token}\",\"name\":\"owner\",\"password\":\"{LaneHost.Password}\"}}";
        return await ServerFixture.SendAsync(ssl, $"POST /api/auth/setup HTTP/1.1\r\nHost: {Host}\r\nOrigin: https://{Host}\r\n" +
                                                  $"Content-Type: application/json\r\nContent-Length: {body.Length}\r\n\r\n{body}");
    }

    [Fact]
    public async Task The_console_alone_sets_up_hosted_access_and_the_owner_is_made_over_its_tls_with_a_link_only_the_console_gives()
    {
        AcmeManager? m = null;
        var ca = new MockAcmeIssuer(() => m?.Responder?.Port ?? 0);
        var relay = new FakeRelay();
        WebAdminHost? hostRef = null;
        // the address check goes through the relay too, and trusts the test CA
        var probe = new SelfProbe
        {
            Connect = async (_, _, _) => await ThroughRelay(hostRef!),
            Validate = (_, _, _, _) => true,
        };
        var setup = new SetupComponent(new SetupOptions { Probe = probe, ProbeEvery = TimeSpan.FromMilliseconds(200) });
        await using var h = await LaneHost.StartAsync(() =>
        [
            new AcmeComponent(new AcmeOptions { Time = TimeProvider.System, Http = new HttpMessageInvoker(ca, disposeHandler: false), Polling = Fast, FirstCheck = TimeSpan.FromHours(1), Period = TimeSpan.FromHours(1) }),
            relay, setup,
        ], (h, s) =>
        {
            s.Tls.Mode = TlsMode.SelfSigned;
            s.Tls.Engine = TlsEngine.SslStream;
            h.Data.WriteJson("relay.json", new RelaySettings { Enabled = false, Origin = MockAcmeIssuer.Relay });
            h.Data.WriteJson("acme.json", new AcmeSettings { Directory = MockAcmeIssuer.Directory, Dns01PropagationSeconds = 5 });
        });
        hostRef = h.Host;
        m = h.Host.Services.Get<AcmeManager>();
        var window = h.Host.Services.Get<HostedSetupWindow>();
        await Eventually.True(() => h.Log.Has("setup#token="), 5000, "kiln.log's link");
        var logToken = Token(h.Log.ToString());
        Assert.False(window.Open);

        // a name that can't be: said, nothing changes
        Assert.StartsWith("Use at least 3 characters.", ConsoleCommands.Setup(h.Host, "hosted", "ab"));
        Assert.StartsWith("Type: kilnwarden setup hosted <name>", ConsoleCommands.Setup(h.Host, "hosted", ""));
        Assert.False(h.Data.ReadJson<RelaySettings>("relay.json")!.Enabled);

        var answer = ConsoleCommands.Setup(h.Host, "hosted", "alpha");
        Assert.Contains($"your panel's address will be https://{Host}/", answer);
        Assert.Contains("1. Claim the name with Discord", answer);
        Assert.Contains($"https://claim.kiln.nexus/claim?v=1&host={Host}&", answer);
        Assert.Contains("type kilnwarden setup again", answer);
        Assert.Contains("letsencrypt.org/repository", answer);
        Assert.DoesNotContain("claim?v=1", h.Log.ToString()); // the claim link is the console's only
        Assert.True(window.Open);
        Assert.Equal(TlsMode.Acme, h.Data.ReadJson<ServerSettings>("settings.json")!.Tls.Mode);
        Assert.Equal("127.0.0.1", h.Data.ReadJson<ServerSettings>("settings.json")!.Bind); // no port is opened

        // not claimed yet: kilnwarden setup says so, and gives no account link
        relay.Set("error", "unclaimed", "the relay does not know alpha.kiln.nexus for this server's key");
        var progress = ConsoleCommands.Setup(h.Host);
        Assert.Contains("[x] Connected to hosted access", progress);
        Assert.Contains("[ ] Name claimed (alpha.kiln.nexus): waiting for your claim", progress);
        Assert.Contains("No link, or it expired? Type kilnwarden setup hosted alpha", progress);
        Assert.DoesNotContain("setup#token=", progress);

        // the claim went through: the link is up, the certificate follows by DNS-01 (before any owner: the window)
        using (var key = new RelayKey(h.Data)) ca.DnsPublicKey = File.ReadAllText(Path.Combine(h.DataDir, "relay-public.pem"));
        relay.Set("connected");
        progress = ConsoleCommands.Setup(h.Host);
        Assert.Contains("[x] Name claimed", progress);
        Assert.DoesNotContain("setup#token=", progress); // not secure yet: no link
        await Eventually.True(() => m.Good is not null, 15000, "the certificate");

        // ready: the console's link, for the hosted address, never in kiln.log
        await Eventually.True(() => h.Host.Services.Get<SetupComponent>().HostedSteps(out _)![3].State == "done", 8000, "the address check");
        var ready = ConsoleCommands.Setup(h.Host);
        Assert.StartsWith("Ready. Open this link to create your owner account (it works once, for 30 minutes): https://alpha.kiln.nexus/setup#token=", ready);
        var hostedToken = Token(ready);
        Assert.DoesNotContain(hostedToken, h.Log.ToString());
        await Eventually.True(() => h.Host.Services.Get<SetupComponent>().HostedSteps(out _)![3].State == "done", 8000, "the address check");

        // kiln.log's link does not create the owner through the relay (a pasted log opens nothing) ...
        var refused = await SetupThroughRelay(h.Host, logToken);
        Assert.Equal(403, refused.Status);
        Assert.Contains("setup_link_this_machine", refused.Body);
        Assert.False(h.Auth.OwnerExists);
        // ... the console's does, over the panel's own TLS (the name's certificate)
        var made = await SetupThroughRelay(h.Host, hostedToken);
        Assert.Equal(200, made.Status);
        Assert.True(h.Auth.OwnerExists);
        Assert.False(window.Open); // the owner exists: the console's window has nothing left to do
        // every link stops working with the owner
        Assert.Equal(409, (await SetupThroughRelay(h.Host, hostedToken)).Status);
        Assert.Contains("[x] Owner account", ConsoleCommands.Status(h.Host));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_hosted_account_link_waits_for_trust_and_the_certificate_actually_being_served(bool trusted)
    {
        AcmeManager? m = null;
        using var ca = new MockAcmeIssuer(() => m?.Responder?.Port ?? 0);
        var relay = new FakeRelay();
        WebAdminHost? host = null;
        var probe = new SelfProbe
        {
            Connect = async (_, _, _) => await ThroughRelay(host!),
            Validate = (_, _, _, _) => trusted,
        };
        var setup = new SetupComponent(new SetupOptions { Probe = probe, ProbeEvery = TimeSpan.FromMilliseconds(100) });
        await using var h = await LaneHost.StartAsync(() =>
        [
            new AcmeComponent(new AcmeOptions { Time = TimeProvider.System, Http = new HttpMessageInvoker(ca, false), Polling = Fast, FirstCheck = TimeSpan.FromHours(1), Period = TimeSpan.FromHours(1) }),
            relay, setup,
        ], (h, s) =>
        {
            s.Tls.Mode = TlsMode.SelfSigned;
            s.Tls.Engine = TlsEngine.SslStream;
            h.Data.WriteJson("relay.json", new RelaySettings { Origin = MockAcmeIssuer.Relay });
            h.Data.WriteJson("acme.json", new AcmeSettings { Directory = MockAcmeIssuer.Directory, Dns01PropagationSeconds = 5 });
        });
        host = h.Host;
        m = host.Services.Get<AcmeManager>();
        ConsoleCommands.Setup(host, "hosted", "alpha");
        using (var key = new RelayKey(h.Data)) ca.DnsPublicKey = File.ReadAllText(Path.Combine(h.DataDir, "relay-public.pem"));
        relay.Set("connected");
        await Eventually.True(() => m.Good is not null, 15000, "the cached certificate");
        await Eventually.True(() => setup.HostedSteps(out _)![3].State == (trusted ? "done" : "failed"), 8000, "the trust check");
        if (trusted)
        {
            // A cached ACME certificate is not proof that the current listener serves it.
            var settings = h.Data.ReadJson<ServerSettings>("settings.json")!;
            settings.Tls.Mode = TlsMode.SelfSigned;
            host.Settings.Save(settings);
            await Eventually.True(() => host.Server?.Tls?.Certificate.Source == "self-signed", 5000, "the replacement listener");
        }
        Assert.NotNull(m.Good);
        Assert.DoesNotContain("setup#token=", ConsoleCommands.Setup(host));
        Assert.False(h.Auth.OwnerExists);
    }

    [Fact]
    public void The_consoles_window_lasts_an_hour_and_never_opens_once_the_owner_exists()
    {
        var time = new ManualTime(DateTimeOffset.UtcNow);
        var w = new HostedSetupWindow(time, () => false);
        Assert.False(w.Open);
        Assert.True(w.Extend());
        Assert.True(w.Open);
        time.Advance(TimeSpan.FromMinutes(59));
        Assert.True(w.Open);
        time.Advance(TimeSpan.FromMinutes(2));
        Assert.False(w.Open);
        var owner = new HostedSetupWindow(time, () => true);
        Assert.False(owner.Extend());
        Assert.False(owner.Open);
    }

    [Fact]
    public async Task Status_says_each_step_and_the_next_action()
    {
        var relay = new FakeRelay();
        await using var h = await LaneHost.StartAsync(() => [relay, new SetupComponent()], (h, s) =>
        {
            s.Tls.Mode = TlsMode.SelfSigned;
            s.Tls.Engine = TlsEngine.SslStream;
            h.Data.WriteJson("relay.json", new RelaySettings { Enabled = true, Origin = "https://127.0.0.1:9", Host = Host });
        });
        relay.Set("error", "dns", "cannot look up frostdev.io:443: this server's DNS does not answer for it");
        var s = ConsoleCommands.Status(h.Host);
        Assert.Contains("[!] Connected to hosted access: This server can't look up 127.0.0.1.", s);
        Assert.Contains("(details: cannot look up frostdev.io:443", s);
        Assert.Contains("[ ] Owner account", s);
        Assert.EndsWith("Next: Its internet connection or DNS isn't working. Ask your host to let the server reach the internet.", s);
        relay.Set("error", "clock", "the relay refused this server's signature");
        Assert.Contains("[!] Name claimed (alpha.kiln.nexus): This server's clock is wrong.", ConsoleCommands.Status(h.Host));
    }

    [Fact]
    public async Task While_the_old_name_keeps_the_panel_off_its_address_says_what_to_delete()
    {
        var text = SetupTexts.OldCopy("kiln/mods/kiln-webadmin");
        using var tmp = new TempDir();
        var port = LaneHost.FreePort();
        var log = new List<string>();
        using (var notice = LegacyNotice.TryStart(tmp.Path, null, text, log.Add, port))
        {
            Assert.NotNull(notice);
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(IPAddress.Loopback, port);
            await using var ssl = new SslStream(tcp.GetStream(), false, (_, _, _, _) => true);
            await ssl.AuthenticateAsClientAsync("localhost");
            var r = await ServerFixture.SendAsync(ssl, $"GET /api/auth/status HTTP/1.1\r\nHost: localhost:{port}\r\n\r\n");
            Assert.Equal(503, r.Status);
            Assert.Contains("old_copy_installed", r.Body);
            Assert.Contains("delete the folder kiln/mods/kiln-webadmin", r.Body);
            // the port taken (the old copy runs and answers): no page, said in the log
            Assert.Null(LegacyNotice.TryStart(tmp.Path, null, text, log.Add, port));
            Assert.Contains(log, l => l.Contains("the old copy answers there"));
        }
        // the panel's port: settings.json's, else the query port + 100
        Assert.Equal(15737, LegacyNotice.PanelPort(tmp.Path, null));
        Assert.Equal(16100, LegacyNotice.PanelPort(tmp.Path, 16000));
        File.WriteAllText(Path.Combine(tmp.Path, "settings.json"), "{\"port\": 18000}");
        Assert.Equal(18000, LegacyNotice.PanelPort(tmp.Path, 16000));
    }

    private sealed class ManualTime(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }
}
