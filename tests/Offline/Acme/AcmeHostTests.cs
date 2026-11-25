// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Kilnwarden.Auth;
using Kilnwarden.Config;
using Kilnwarden.Offline.Support;
using Kilnwarden.Tls.Acme;

namespace Kilnwarden.Offline.Acme;

/// <summary>tls.mode acme through the real host: the listener serves the self-signed fallback, then the ACME
/// certificate once issued (no restart), and /api/acme is gated (renew: owner + recent proof).</summary>
public sealed class AcmeHostTests
{
    private const string Name = "panel.example.test";

    private static async Task<string> ServedSha(int port)
    {
        using var c = new TcpClient();
        await c.ConnectAsync(IPAddress.Loopback, port);
        string? sha = null;
        await using var ssl = new SslStream(c.GetStream(), false, (_, cert, _, _) =>
        {
            sha = Convert.ToHexString(SHA256.HashData(cert!.GetRawCertData()));
            return true;
        });
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = Name });
        return sha!;
    }

    /// <summary>The certificate the listener serves once it has taken up &lt;paramref name="want"/&gt; (the engines swap the
    /// certificate asynchronously after the manager announces it; under a loaded test run that takes a moment).</summary>
    private static async Task<string> ServedShaOnce(int port, string want)
    {
        var sha = await ServedSha(port);
        for (var i = 0; i < 100 && sha != want; i++)
        {
            await Task.Delay(50);
            sha = await ServedSha(port);
        }
        return sha;
    }

    [Fact]
    public async Task Listener_serves_the_acme_certificate_once_issued_and_renew_needs_recent_proof()
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
        var fallback = await ServedSha(h.Port);
        Assert.Equal("no owner account yet (settings.json remoteSetup orders before it)", m.Status()["blocked"]!.GetValue<string>());

        await h.AccountAsync("boss", BuiltinRoles.Owner);
        await h.AccountAsync("val", BuiltinRoles.Viewer);
        await Eventually.True(() => h.Host.Services.Get<Kilnwarden.Hosting.ISetupState>().OwnerExists, 2000, "owner");
        Assert.True(await m.CheckAsync(false, CancellationToken.None), h.Log.ToString());
        await Eventually.True(() => h.Host.Server is not null, 2000, "listener");
        var served = await ServedShaOnce(h.Port, m.Good!.Info.Sha256.Replace(":", ""));
        Assert.NotEqual(fallback, served);
        Assert.Equal(m.Good!.Info.Sha256.Replace(":", ""), served);

        using var val = h.NewBrowser();
        await val.SignInAsync("val");
        Assert.Equal(HttpStatusCode.Forbidden, (await val.GetAsync("/api/acme/status")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await val.PostAsync("/api/acme/renew")).StatusCode);

        using var boss = h.NewBrowser();
        await boss.SignInAsync("boss");
        var st = await LaneHost.Browser.JsonAsync(await boss.GetAsync("/api/acme/status"));
        Assert.Equal(m.Good.Info.Sha256, st!["certificate"]!["sha256"]!.GetValue<string>());
        var r = await boss.PostAsync("/api/acme/renew");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Equal("reauth_required", (await LaneHost.Browser.JsonAsync(r))!["error"]!["code"]!.GetValue<string>());
        await boss.ReauthAsync();
        Assert.Equal(HttpStatusCode.Accepted, (await boss.PostAsync("/api/acme/renew")).StatusCode);
        await Eventually.True(() => m.Good!.Info.Sha256.Replace(":", "") != served, 5000, "the renewal");
        Assert.Equal(2, ca.Issued);
        var renewed = m.Good!.Info.Sha256.Replace(":", "");
        Assert.Equal(renewed, await ServedShaOnce(h.Port, renewed));
        Assert.NotEqual(served, await ServedSha(h.Port));
    }
}
