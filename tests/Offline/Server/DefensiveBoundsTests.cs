// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using Kilnwarden.Auth;
using Kilnwarden.Config;
using Kilnwarden.Http;
using Kilnwarden.Offline.Support;
using Kilnwarden.Server;
using Kilnwarden.Static;
using Kilnwarden.Tls;

namespace Kilnwarden.Offline.Server;

public class DefensiveBoundsTests
{
    [Fact]
    public void Forwarded_client_must_pass_allowlist()
    {
        var s = new ServerSettings { IpAllowlist = ["192.0.2.0/24"] };
        s.ReverseProxy.Enabled = true;
        var h = new HttpHeaders();
        h.Set("X-Forwarded-For", "198.51.100.10");
        h.Set("X-Forwarded-Proto", "https");
        h.Set("X-Forwarded-Host", "localhost");
        var req = new HttpRequest { Method = "GET", Target = "/", Path = "/", Headers = h, Host = "localhost" };
        var e = Assert.Throws<HttpError>(() => ServerPolicy.From(s, true).Resolve(req, IPAddress.Loopback, false));
        Assert.Equal(403, e.Status);
    }

    [Fact]
    public void Plain_remote_peer_cannot_bypass_proxy_switch()
    {
        var s = new ServerSettings();
        s.ReverseProxy.Enabled = true;
        s.Tls.Mode = TlsMode.Off;
        var req = new HttpRequest { Method = "GET", Target = "/", Path = "/", Host = "localhost" };
        Assert.Throws<HttpError>(() => ServerPolicy.From(s, true).Resolve(req, IPAddress.Parse("198.51.100.10"), false));
    }

    [Fact]
    public void Static_scan_does_not_follow_links()
    {
        using var root = new TempDir();
        using var outside = new TempDir();
        File.WriteAllText(root.File("index.html"), "fixture");
        File.WriteAllText(outside.File("private.txt"), "private fixture");
        File.CreateSymbolicLink(root.File("private.txt"), outside.File("private.txt"));
        Directory.CreateSymbolicLink(root.File("linked"), outside.Path);
        var files = StaticFiles.Load(root.Path, new TestLog());
        Assert.Equal(1, files.Count);
    }

    [Fact]
    public async Task Body_length_is_checked_before_allocation()
    {
        using var stream = new MemoryStream();
        using var reader = new RequestReader(stream);
        var e = await Assert.ThrowsAsync<HttpError>(() => reader.ReadBodyAsync((long)int.MaxValue + 1, 1000, default).AsTask());
        Assert.Equal(413, e.Status);
    }

    [Fact]
    public void Invalid_enums_and_nonfinite_rates_are_rejected()
    {
        var s = new ServerSettings();
        s.Tls.Mode = (TlsMode)99;
        s.Tls.Engine = (TlsEngine)99;
        s.Limits.RequestsPerSecondPerIp = double.NaN;
        s.Limits.ConnectionsPerSecondPerIp = double.PositiveInfinity;
        Assert.True(SettingsRules.Validate(s).Count >= 3);
    }

    [Fact]
    public async Task Hash_scheduler_has_a_fixed_pending_bound()
    {
        // Reserve the only hashing slot deterministically. No expensive work or timing race is needed.
        using var h = new PasswordHasher(1000, 1, null, TimeSpan.FromSeconds(1), allowWeak: true);
        var slots = (SemaphoreSlim)typeof(PasswordHasher).GetField("_slots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(h)!;
        Assert.True(slots.Wait(0));
        using var cts = new CancellationTokenSource();
        var queued = Enumerable.Range(0, 16).Select(_ => h.HashAsync("fixture", cts.Token)).ToArray();
        try
        {
            var excess = h.HashAsync("fixture", cts.Token);
            Assert.True(excess.IsCompleted, "queue overflow must be refused synchronously rather than adding an unbounded waiter");
            Assert.Equal(503, (await Assert.ThrowsAsync<HttpError>(() => excess)).Status);
        }
        finally
        {
            cts.Cancel();
            foreach (var t in queued) try { await t; } catch (OperationCanceledException) { }
            slots.Release();
        }
    }
}
