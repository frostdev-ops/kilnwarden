// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using Kilnwarden.Config;
using Kilnwarden.Hosting;
using Kilnwarden.Offline.Support;
using Kilnwarden.Relay;

namespace Kilnwarden.Offline.Hosting;

/// <summary>The first run on a server whose owner has only file access and the host's web console (a rented game
/// server, a container): settings.json remoteSetup, the console's kilnwarden setup and status, and the relay's
/// failures in plain words (docs/hosting-routes.md).</summary>
public class RemoteSetupTests
{
    private static string Token(string text) => Regex.Match(text, @"setup#token=([A-Za-z0-9_-]{43})").Groups[1].Value;

    /// <summary>A public bind (0.0.0.0, plain HTTP behind a "reverse proxy" so the harness's client can talk to it).</summary>
    private static Action<AuthHarness> Public(bool remoteSetup, Action<ServerSettings>? more = null) => h =>
    {
        var data = new Kilnwarden.Storage.DataStore(h.DataDir);
        var s = data.ReadJson<ServerSettings>("settings.json")!;
        s.Bind = "0.0.0.0";
        s.ReverseProxy.Enabled = true;
        s.RemoteSetup = remoteSetup;
        more?.Invoke(s);
        data.WriteJson("settings.json", s);
    };

    [Fact]
    public async Task Without_remoteSetup_a_public_bind_waits_for_the_owner()
    {
        await using var h = await AuthHarness.StartAsync(kap: false, beforeHost: Public(remoteSetup: false));
        Assert.Equal(IPAddress.Loopback, h.Host.Server!.LocalEndPoint.Address);
        Assert.True(h.Log.Has("waits until the owner account exists"));
        Assert.DoesNotContain(h.Log.Lines, l => l.Contains("REMOTE SETUP"));
        await Eventually.True(() => h.Log.Has("On this computer, open "), 5000, "the setup link for this machine");
    }

    [Fact]
    public async Task With_remoteSetup_the_panel_listens_before_the_owner_and_the_setup_link_says_so()
    {
        await using var h = await AuthHarness.StartAsync(kap: false, beforeHost: Public(remoteSetup: true));
        Assert.Equal(IPAddress.Any, h.Host.Server!.LocalEndPoint.Address);
        Assert.True(h.Log.Has("REMOTE SETUP"), string.Join("\n", h.Log.Lines));
        await Eventually.True(() => h.Log.Has("From any browser, open "), 5000, "the remote setup link");
        // still only with the one-time token
        using var b = h.NewClient();
        var bad = await b.PostAsync("/api/auth/setup", new { token = new string('A', 43), name = "owner", password = AuthHarness.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        var ok = await b.PostAsync("/api/auth/setup", new { token = Token(string.Join("\n", h.Log.Lines)), name = "owner", password = AuthHarness.Password });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        // the owner exists: the listener stays as it is (no restart under the browser that just made the owner)
        await Task.Delay(300);
        Assert.Equal(IPAddress.Any, h.Host.Server!.LocalEndPoint.Address);
        Assert.Single(h.Log.Lines, l => l.Contains("panel at "));
    }

    [Fact]
    public async Task On_a_Kiln_too_old_remoteSetup_changes_nothing()
    {
        await using var h = await AuthHarness.StartAsync(setup: k => k.BeOld(), waitKap: false, beforeHost: Public(remoteSetup: true));
        await Eventually.True(() => h.KapClient.MissingCapabilities is not null, 5000, "the verdict");
        await Eventually.True(() => h.Host.Server?.LocalEndPoint.Address.Equals(IPAddress.Loopback) == true, 5000, "back to loopback");
        Assert.False(h.Host.Services.Get<ISetupState>().SetupOpen);
    }

    [Fact]
    public async Task The_console_command_makes_a_working_setup_link_and_says_where_the_panel_is()
    {
        await using var h = await AuthHarness.StartAsync(kap: false);
        await Eventually.True(() => h.Host.Endpoints.Count > 0, 5000, "listening");
        var status = ConsoleCommands.Status(h.Host);
        Assert.Contains($"[x] Running: http://127.0.0.1:{h.Port}/", status);
        Assert.Contains("[ ] Owner account", status);
        var answer = ConsoleCommands.Setup(h.Host);
        Assert.Contains("On this computer, open ", answer);
        Assert.Contains("kilnwarden setup hosted", answer); // the way for a server without a browser of its own
        var token = Token(answer);
        Assert.Equal(43, token.Length);
        Assert.NotEqual(Token(string.Join("\n", h.Log.Lines)), token);
        Assert.DoesNotContain(token, string.Join("\n", h.Log.Lines)); // the answer file only, never kiln.log
        await Eventually.True(() => h.Auth.Audit.Read(20).Any(e => e.GetProperty("op").GetString() == "auth.setup_link" && e.GetRawText().Contains("console")),
            5000, "the console's setup link in the audit log (written in the background)");
        using var b = h.NewClient();
        var ok = await b.PostAsync("/api/auth/setup", new { token, name = "owner", password = AuthHarness.Password });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.StartsWith("Set up. Sign in at", ConsoleCommands.Setup(h.Host));
        Assert.Contains("[x] Owner account", ConsoleCommands.Status(h.Host));
        Assert.Contains("not running", ConsoleCommands.Setup(null));
    }

    [Fact]
    public async Task With_remoteSetup_and_the_relay_the_link_names_the_relay_address_and_the_relay_connects_before_the_owner()
    {
        // a relay nobody listens at: the client tries before any owner exists (remoteSetup) and says why it fails
        var dead = new TcpListener(IPAddress.Loopback, 0);
        dead.Start();
        var port = ((IPEndPoint)dead.LocalEndpoint).Port;
        dead.Stop();
        await using var h = await AuthHarness.StartAsync(kap: false, beforeHost: h =>
        {
            Public(remoteSetup: true, s =>
            {
                s.ReverseProxy.Enabled = false;
                s.Tls.Mode = TlsMode.SelfSigned;
                s.Tls.Hostnames = ["alpha.kiln.nexus"];
            })(h);
            new Kilnwarden.Storage.DataStore(h.DataDir).WriteJson("relay.json",
                new RelaySettings { Enabled = true, Origin = $"https://127.0.0.1:{port}", Host = "alpha.kiln.nexus" });
        }, extra: [new RelayComponent(), new Kilnwarden.Setup.SetupComponent()]);
        await Eventually.True(() => h.Log.Has("https://alpha.kiln.nexus/setup#token="), 8000, "the relay's address in the setup link");
        var relay = h.Host.Services.Get<RelayStatus>();
        await Eventually.True(() => relay.Describe().State == "error", 8000, "a relay attempt before the owner exists");
        Assert.Contains($"cannot connect to 127.0.0.1:{port}: outbound HTTPS is blocked here", relay.Describe().LastError);
        Assert.Single(h.Log.Lines, l => l.Contains("relay for alpha.kiln.nexus: cannot connect")); // once, not every 4 s
        Assert.StartsWith("Kilnwarden: set up your panel (about 2 minutes).\n  From any browser, open https://alpha.kiln.nexus/setup#token=", ConsoleCommands.Setup(h.Host));
        Assert.Contains($"[!] Connected to hosted access: This server can't reach 127.0.0.1.", ConsoleCommands.Status(h.Host));
    }

    [Fact]
    public async Task Without_remoteSetup_the_relay_waits_for_the_owner()
    {
        await using var h = await AuthHarness.StartAsync(kap: false, beforeHost: h =>
        {
            var data = new Kilnwarden.Storage.DataStore(h.DataDir);
            var s = data.ReadJson<ServerSettings>("settings.json")!;
            s.Tls.Mode = TlsMode.SelfSigned;
            s.Tls.Hostnames = ["alpha.kiln.nexus"];
            data.WriteJson("settings.json", s);
            data.WriteJson("relay.json", new RelaySettings { Enabled = true, Origin = "https://127.0.0.1:9", Host = "alpha.kiln.nexus" });
        }, extra: [new RelayComponent()]);
        await Task.Delay(1500);
        Assert.Equal("waiting", h.Host.Services.Get<RelayStatus>().Describe().State);
        Assert.DoesNotContain(h.Log.Lines, l => l.Contains("relay for alpha.kiln.nexus:"));
    }

    [Theory]
    [InlineData(403, "the relay does not know alpha.kiln.nexus for this server's key: claim the name (Panel settings > Hosted access > Claim this name, or the console command kilnwarden claim")]
    [InlineData(401, "the server's clock is off by more than two minutes")]
    [InlineData(429, "the relay is limiting this address")]
    [InlineData(404, "there is no relay at https://frostdev.io (relay.json origin)")]
    [InlineData(503, "the relay is full or restarting")]
    [InlineData(418, "the relay answered HTTP 418")]
    public void Relay_refusals_in_plain_words(int status, string expected)
    {
        var s = new RelaySettings { Enabled = true, Host = "alpha.kiln.nexus" };
        Assert.Contains(expected, RelayErrors.Explain(new RelayRefusedException(status, new System.Net.WebSockets.WebSocketException()), s));
    }

    [Fact]
    public void Network_failures_in_plain_words()
    {
        var s = new RelaySettings { Enabled = true, Host = "alpha.kiln.nexus" };
        Assert.Contains("cannot look up frostdev.io:443", RelayErrors.Explain(new HttpRequestException("x", new SocketException((int)SocketError.HostNotFound)), s));
        Assert.Contains("outbound HTTPS is blocked here", RelayErrors.Explain(new HttpRequestException("x", new SocketException((int)SocketError.ConnectionRefused)), s));
        Assert.Contains("outbound HTTPS is blocked here", RelayErrors.Explain(new System.Net.WebSockets.WebSocketException("x", new HttpRequestException("y", new SocketException((int)SocketError.TimedOut))), s));
        // what .NET throws when a firewall drops the connection: SocketsHttpHandler's ConnectTimeout, as seen from a
        // container whose host firewall drops its traffic (our end-to-end routes test)
        Assert.Contains("within 8 s: outbound HTTPS is blocked (dropped) here", RelayErrors.Explain(new System.Net.WebSockets.WebSocketException("Unable to connect to the remote server",
            new HttpRequestException("x", new TimeoutException("A connection could not be established within the configured ConnectTimeout."))), s));
        Assert.Contains("intercepts HTTPS", RelayErrors.Explain(new HttpRequestException("x", new AuthenticationException("bad cert")), s));
        Assert.Contains("no answer from frostdev.io:443 within 10 s", RelayErrors.Explain(new TimeoutException(), s));
    }
}
