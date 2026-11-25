// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Sockets;
using Kilnwarden.Config;
using Kilnwarden.Offline.Support;
using Kilnwarden.Relay;
using Kilnwarden.Tls;

namespace Kilnwarden.Offline.RelayClient;

/// <summary>The relay client's server side and its key (security review S3, S8, I6).</summary>
public class RelayTests
{
    private static SslStreamServer Tls()
    {
        var now = DateTimeOffset.UtcNow;
        var (cert, key) = Pfx.SelfSignedRsa("relay test", ["panel.example.test"], now.AddDays(-1), now.AddDays(10));
        return new SslStreamServer(new FixedCertificateSource(new CertificateMaterial(cert, key, CertificateInspector.Inspect(cert, key, "self-signed")), "self-signed"),
            ["http/1.1"], new TestLog());
    }

    /// <summary>A connected socket pair: the server end goes to the panel as a relayed stream.</summary>
    private static async Task<(TcpClient Client, NetworkStream ServerEnd, TcpListener L)> PairAsync()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var c = new TcpClient();
        var accept = l.AcceptTcpClientAsync();
        await c.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)l.LocalEndpoint).Port);
        return (c, (await accept).GetStream(), l);
    }

    private static async Task<bool> ClosedAsync(TcpClient c)
    {
        using var cts = new CancellationTokenSource(3000);
        try
        {
            return await c.GetStream().ReadAsync(new byte[1], cts.Token) == 0;
        }
        catch (IOException)
        {
            return true;
        }
    }

    /// <summary>S3: an honest relay only announces browsers on public addresses. Loopback, private, link-local and
    /// unspecified announcements are refused, and the IP allowlist applies to relayed peers without the loopback
    /// exemption.</summary>
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::1")]
    [InlineData("10.1.2.3")]
    [InlineData("192.168.1.10")]
    [InlineData("169.254.169.254")]
    [InlineData("0.0.0.0")]
    [InlineData("8.8.4.4")] // public, but not on the allowlist
    public async Task Relayed_peers_must_be_public_and_on_the_allowlist(string announced)
    {
        using var tls = Tls();
        using var f = new ServerFixture(s => s.IpAllowlist = ["8.8.8.0/24"], tls: tls);
        var (client, serverEnd, l) = await PairAsync();
        using var _c = client;
        try
        {
            var refusedBefore = Interlocked.Read(ref f.Shared.Counters.Refused);
            await f.Server.AdmitRelayAsync(serverEnd, new IPEndPoint(IPAddress.Parse(announced), 40000));
            Assert.Equal(refusedBefore + 1, Interlocked.Read(ref f.Shared.Counters.Refused));
            Assert.True(await ClosedAsync(client));
        }
        finally
        {
            l.Stop();
        }
    }

    [Fact]
    public async Task A_public_allowed_relayed_peer_is_served()
    {
        using var tls = Tls();
        using var f = new ServerFixture(s => s.IpAllowlist = ["8.8.8.0/24"], tls: tls);
        var (client, serverEnd, l) = await PairAsync();
        using var _c = client;
        try
        {
            var refusedBefore = Interlocked.Read(ref f.Shared.Counters.Refused);
            var run = f.Server.AdmitRelayAsync(serverEnd, new IPEndPoint(IPAddress.Parse("8.8.8.8"), 40000));
            Assert.False(run.IsCompleted); // waiting for the TLS handshake
            Assert.Equal(refusedBefore, Interlocked.Read(ref f.Shared.Counters.Refused));
            client.Close();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            l.Stop();
        }
    }

    [Fact]
    public void Relay_announcements_are_checked_before_a_stream_is_opened()
    {
        Assert.True(RelayComponent.AcceptableAnnouncement(IPAddress.Parse("8.8.8.8")));
        Assert.True(RelayComponent.AcceptableAnnouncement(IPAddress.Parse("2001:4860:4860::8888")));
        foreach (var bad in new[] { "127.0.0.1", "::ffff:127.0.0.1", "10.0.0.1", "fd00::1", "fe80::1", "0.0.0.0", "100.64.0.1", "224.0.0.1" })
            Assert.False(RelayComponent.AcceptableAnnouncement(IPAddress.Parse(bad)), bad);
    }

    /// <summary>S8: a relay key file the panel cannot read is reported and retried, never a silent dead relay.</summary>
    [Fact]
    public async Task A_bad_relay_key_is_logged_and_the_relay_keeps_trying()
    {
        await using var h = await AuthHarness.StartAsync(kap: false, beforeHost: h =>
        {
            File.WriteAllText(Path.Combine(h.DataDir, "relay-key.pem"), "-----BEGIN PRIVATE KEY-----\ngarbage\n-----END PRIVATE KEY-----\n");
            var data = new Kilnwarden.Storage.DataStore(h.DataDir);
            data.WriteJson("relay.json", new RelaySettings { Enabled = true, Origin = "https://relay.example.test", Host = "panel.example.test" });
        }, extra: [new RelayComponent()]);
        await Eventually.True(() => h.Log.Lines.Any(l => l.Contains("relay-key.pem")), 8000, "a warning naming the key file");
        Assert.Contains(h.Log.Lines, l => l.Contains("delete relay-key.pem and relay-public.pem"));
    }
}
