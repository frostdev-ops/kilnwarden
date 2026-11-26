// SPDX-License-Identifier: MIT
// 0.1.2's changes of behaviour, written against the API 0.1.1 already had, so they ran red on 0.1.1 first
// (out/setup-wizard/logs/red-first-*.log): the certificate 127.0.0.1 gets in tls.mode acme, the dns-01 order that waits
// for the relay link, the three-line first-start block, and kilnwarden status as a checklist.
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using Kilnwarden.Auth;
using Kilnwarden.Config;
using Kilnwarden.Hosting;
using Kilnwarden.Offline.Acme;
using Kilnwarden.Offline.Support;
using Kilnwarden.Relay;
using Kilnwarden.Tls.Acme;

namespace Kilnwarden.Offline.Setup;

public sealed class BehaviourChangeTests
{
    private const string Name = "panel.example.test";

    private static async Task<string> ServedSha(int port, string? sni)
    {
        using var c = new TcpClient();
        await c.ConnectAsync(IPAddress.Loopback, port);
        string? sha = null;
        await using var ssl = new SslStream(c.GetStream(), false, (_, cert, _, _) =>
        {
            sha = Convert.ToHexString(SHA256.HashData(cert!.GetRawCertData()));
            return true;
        });
        // an IP address as the target sends no server name, as a browser at https://127.0.0.1:<port>/ does
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = sni ?? "127.0.0.1" });
        return sha!;
    }

    /// <summary>The page the owner set things up on (https://127.0.0.1:15737) keeps its certificate when the Let's
    /// Encrypt one arrives: that one is for the panel's name, so serving it to 127.0.0.1 broke the page's connection. The
    /// panel's name gets the new certificate, and launcher-endpoint.json pins what 127.0.0.1 gets.</summary>
    [Fact]
    public async Task In_acme_mode_this_machine_keeps_the_self_signed_certificate_and_the_name_gets_the_new_one()
    {
        AcmeManager? m = null;
        var ca = new MockAcmeIssuer(() => m?.Responder?.Port ?? 0);
        await using var h = await LaneHost.StartAsync(() => [new AcmeComponent(new AcmeOptions
        {
            Time = TimeProvider.System, Http = new HttpMessageInvoker(ca), Polling = new AcmePolling(TimeSpan.FromMilliseconds(20), 50, TimeSpan.FromSeconds(10)),
            FirstCheck = TimeSpan.FromHours(1), Period = TimeSpan.FromHours(1),
        })], (h, s) =>
        {
            s.Tls.Mode = TlsMode.Acme;
            s.Tls.Engine = TlsEngine.SslStream;
            s.AllowedHosts = [Name];
            h.Data.WriteJson("acme.json", new AcmeSettings
            {
                Directory = MockAcmeIssuer.Directory, AcceptTerms = true, Names = [Name], Http01Bind = "127.0.0.1", Http01Port = 0,
            });
        });
        m = h.Host.Services.Get<AcmeManager>();
        var before = await ServedSha(h.Port, null);
        await h.AccountAsync("boss", BuiltinRoles.Owner);
        await Eventually.True(() => h.Host.Services.Get<ISetupState>().OwnerExists, 2000, "owner");
        Assert.True(await m.CheckAsync(false, CancellationToken.None), h.Log.ToString());
        var acme = m.Good!.Info.Sha256.Replace(":", "");
        string named = "";
        for (var i = 0; i < 100 && named != acme; i++)
        {
            named = await ServedSha(h.Port, Name);
            if (named != acme) await Task.Delay(50);
        }
        Assert.Equal(acme, named);
        Assert.Equal(before, await ServedSha(h.Port, null));
        Assert.Equal(before, await ServedSha(h.Port, "localhost"));
        var endpoint = h.Data.ReadJson<System.Text.Json.Nodes.JsonObject>("launcher-endpoint.json")!;
        Assert.Equal(before, endpoint["sha256"]!.GetValue<string>().Replace(":", ""));
    }

    /// <summary>A dns-01 order through the relay waits for the relay link (the name claimed and the server connected)
    /// instead of failing: before 0.1.2 the first check ordered at once, the relay refused the record, and the order
    /// counted against the week and waited an hour.</summary>
    [Fact]
    public async Task A_dns01_order_waits_for_the_relay_link_instead_of_failing_and_backing_off()
    {
        var dead = new TcpListener(IPAddress.Loopback, 0);
        dead.Start();
        var port = ((IPEndPoint)dead.LocalEndpoint).Port;
        dead.Stop();
        var ca = new MockAcmeIssuer(() => 0);
        await using var h = await LaneHost.StartAsync(() => [new AcmeComponent(new AcmeOptions
        {
            Time = TimeProvider.System, Http = new HttpMessageInvoker(ca), Polling = new AcmePolling(TimeSpan.FromMilliseconds(20), 50, TimeSpan.FromSeconds(10)),
            FirstCheck = TimeSpan.FromHours(1), Period = TimeSpan.FromHours(1),
        }), new RelayComponent()], (h, s) =>
        {
            s.Tls.Mode = TlsMode.Acme;
            s.Tls.Engine = TlsEngine.SslStream;
            s.AllowedHosts = [Name];
            h.Data.WriteJson("acme.json", new AcmeSettings { Directory = MockAcmeIssuer.Directory, AcceptTerms = true, Names = [Name], Challenge = "dns-01", Dns01PropagationSeconds = 5 });
            h.Data.WriteJson("relay.json", new RelaySettings { Enabled = true, Origin = $"https://127.0.0.1:{port}", Host = Name });
        });
        await h.AccountAsync("boss", BuiltinRoles.Owner);
        await Eventually.True(() => h.Host.Services.Get<ISetupState>().OwnerExists, 2000, "owner");
        await Eventually.True(() => File.Exists(Path.Combine(h.DataDir, "relay-key.pem")), 8000, "the relay key");
        var m = h.Host.Services.Get<AcmeManager>();
        Assert.False(await m.CheckAsync(false, CancellationToken.None));
        var st = m.Status();
        Assert.Equal(0, st["ordersThisWeek"]!.GetValue<int>());
        Assert.Equal(0, st["failures"]!.GetValue<int>());
        Assert.Null(st["nextAttempt"]);
        Assert.Contains("waiting for hosted access", st["blocked"]!.GetValue<string>());
        Assert.Equal(0, ca.Requests);
    }

    /// <summary>The first start says one thing, in three lines: what to do, the link for this computer, and the console
    /// command for a server without a browser.</summary>
    [Fact]
    public async Task The_first_start_says_what_to_do_in_three_lines()
    {
        await using var h = await AuthHarness.StartAsync(kap: false);
        await Eventually.True(() => h.Log.Has("setup#token="), 5000, "the setup link");
        var lines = h.Log.Lines.ToList();
        var at = lines.FindIndex(l => l.Contains("set up your panel"));
        Assert.True(at >= 0, h.Log.ToString());
        Assert.Contains("(about 2 minutes)", lines[at]);
        Assert.Matches(@"On this computer, open http://127\.0\.0\.1:\d+/setup#token=[A-Za-z0-9_-]{43}$", lines[at + 1]);
        Assert.Contains("In the server console, type: kilnwarden setup hosted <a name for your panel>", lines[at + 2]);
        Assert.Single(lines, l => l.Contains("setup#token=")); // once, not twice
        // the console says the same three lines, with a link of its own
        var console = ConsoleCommands.Setup(h.Host);
        Assert.Equal(3, console.Split('\n').Length);
        Assert.Contains("On this computer, open http://127.0.0.1:", console);
    }

    /// <summary>kilnwarden status is a checklist with the next step's exact action.</summary>
    [Fact]
    public async Task Status_is_a_checklist_with_the_next_step()
    {
        await using var h = await AuthHarness.StartAsync(kap: false);
        await Eventually.True(() => h.Host.Endpoints.Count > 0, 5000, "listening");
        var s = ConsoleCommands.Status(h.Host);
        Assert.Contains($"[x] Running: http://127.0.0.1:{h.Port}/", s);
        Assert.Contains("[ ] Owner account", s);
        Assert.EndsWith("Next: type kilnwarden setup for your setup link.", s);
    }
}
