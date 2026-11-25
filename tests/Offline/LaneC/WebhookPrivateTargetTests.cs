// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Events;
using Kilnwarden.Offline.Support;
using Kilnwarden.Security;
using Kilnwarden.Webhooks;

namespace Kilnwarden.Offline.LaneC;

/// <summary>The owner's private-target opt-in for webhooks (security trade-off policy: off by default, owner only, recent
/// sign-in, audited, a visible warning, switchable back off). It opens RFC 1918 and IPv6 ULA only; loopback, link-local
/// (cloud metadata included), multicast, unspecified and broadcast stay refused, in every IPv4-mapped form, at the URL
/// check and at the address actually connected to.</summary>
[Collection("lane-c")]
public sealed class WebhookPrivateTargetTests
{
    // ---- address classes: (address, allowed when off, allowed when on)
    [Theory]
    [InlineData("8.8.8.8", true, true)]
    [InlineData("2606:4700:4700::1111", true, true)]
    [InlineData("::ffff:8.8.8.8", true, true)]
    // RFC 1918 and ULA: only with the opt-in
    [InlineData("10.0.0.1", false, true)]
    [InlineData("10.255.255.254", false, true)]
    [InlineData("172.16.0.1", false, true)]
    [InlineData("172.31.255.254", false, true)]
    [InlineData("192.168.0.1", false, true)]
    [InlineData("192.168.255.254", false, true)]
    [InlineData("fc00::1", false, true)]
    [InlineData("fd12:3456:789a::1", false, true)]
    [InlineData("::ffff:10.1.2.3", false, true)]
    [InlineData("::ffff:172.20.0.9", false, true)]
    [InlineData("::ffff:192.168.1.20", false, true)]
    // just outside the private ranges
    [InlineData("172.15.255.255", true, true)]
    [InlineData("172.32.0.1", true, true)]
    [InlineData("192.169.0.1", true, true)]
    // never, even with the opt-in
    [InlineData("127.0.0.1", false, false)]
    [InlineData("127.255.255.254", false, false)]
    [InlineData("::1", false, false)]
    [InlineData("::ffff:127.0.0.1", false, false)]
    [InlineData("169.254.169.254", false, false)]
    [InlineData("169.254.0.1", false, false)]
    [InlineData("::ffff:169.254.169.254", false, false)]
    [InlineData("fe80::1", false, false)]
    [InlineData("febf:ffff::1", false, false)]
    [InlineData("224.0.0.1", false, false)]
    [InlineData("239.255.255.250", false, false)]
    [InlineData("ff02::1", false, false)]
    [InlineData("::ffff:224.0.0.251", false, false)]
    [InlineData("0.0.0.0", false, false)]
    [InlineData("0.1.2.3", false, false)]
    [InlineData("::", false, false)]
    [InlineData("::ffff:0.0.0.0", false, false)]
    [InlineData("255.255.255.255", false, false)]
    [InlineData("::ffff:255.255.255.255", false, false)]
    // other non-public space the opt-in does not open either
    [InlineData("100.64.0.1", false, false)]
    [InlineData("fec0::1", false, false)]
    [InlineData("64:ff9b::a00:1", false, false)]       // NAT64 of 10.0.0.1
    [InlineData("2002:0a00:0001::1", false, false)]    // 6to4 of 10.0.0.1
    [InlineData("::a00:1", false, false)]              // IPv4-compatible (deprecated) 10.0.0.1
    public void Address_classes(string ip, bool off, bool on)
    {
        var a = IPAddress.Parse(ip);
        Assert.Equal(off, WebhookGuard.IsAllowed(a, allowPrivate: false));
        Assert.Equal(on, WebhookGuard.IsAllowed(a, allowPrivate: true));
    }

    // ---- the URL check: (kind, url, allowed when off, allowed when on)
    [Theory]
    [InlineData("json", "https://hooks.example.com/in", true, true)]
    [InlineData("json", "https://10.0.0.5/in", false, true)]
    [InlineData("json", "https://172.16.4.4:8443/in", false, true)]
    [InlineData("json", "https://192.168.1.20/in", false, true)]
    [InlineData("json", "https://[fd00::5]/in", false, true)]
    [InlineData("json", "https://[::ffff:192.168.1.20]/in", false, true)]
    [InlineData("json", "http://192.168.1.20:8080/in", false, true)]   // plain http: private targets only, opt-in only
    [InlineData("json", "http://[fd00::5]:8080/in", false, true)]
    [InlineData("json", "https://nas.lan/in", false, true)]            // LAN names: the connect check judges the address
    [InlineData("json", "https://ha.local:8123/in", false, true)]
    [InlineData("json", "https://intranet/in", false, true)]
    [InlineData("json", "http://nas.home.arpa/in", false, true)]
    [InlineData("json", "http://8.8.8.8/in", false, false)]            // plain http to a public address: never
    [InlineData("json", "https://127.0.0.1/in", false, false)]
    [InlineData("json", "http://127.0.0.1:8080/in", false, false)]
    [InlineData("json", "https://127.1.2.3/in", false, false)]
    [InlineData("json", "https://[::1]/in", false, false)]
    [InlineData("json", "https://[::ffff:127.0.0.1]/in", false, false)]
    [InlineData("json", "https://169.254.169.254/latest", false, false)]
    [InlineData("json", "http://169.254.169.254/latest", false, false)]
    [InlineData("json", "https://[::ffff:169.254.169.254]/latest", false, false)]
    [InlineData("json", "https://[fe80::1]/in", false, false)]
    [InlineData("json", "https://224.0.0.1/in", false, false)]
    [InlineData("json", "https://[ff02::1]/in", false, false)]
    [InlineData("json", "https://0.0.0.0/in", false, false)]
    [InlineData("json", "https://[::]/in", false, false)]
    [InlineData("json", "https://255.255.255.255/in", false, false)]
    [InlineData("json", "https://[::ffff:255.255.255.255]/in", false, false)]
    [InlineData("json", "https://localhost/in", false, false)]
    [InlineData("json", "http://localhost:8080/in", false, false)]
    [InlineData("json", "https://kap.localhost/in", false, false)]
    [InlineData("json", "https://localhost.localdomain/in", false, false)]
    [InlineData("json", "https://box.localdomain/in", false, false)]
    [InlineData("json", "https://localhost./in", false, false)]
    [InlineData("json", "https://kap.localhost./in", false, false)]
    [InlineData("json", "https://LOCALHOST/in", false, false)]
    [InlineData("json", "https://Kap.LocalHost/in", false, false)]
    [InlineData("json", "http://box.localdomain./in", false, false)]
    [InlineData("discord", "https://192.168.1.20/api/webhooks/123456789012345678/abcdefghijklmnopqrstuvwxyz", false, false)]
    [InlineData("discord", "http://discord.com/api/webhooks/123456789012345678/abcdefghijklmnopqrstuvwxyz", false, false)]
    [InlineData("json", "ftp://192.168.1.20/in", false, false)]
    public void Urls(string kind, string url, bool off, bool on)
    {
        Assert.Equal(off, WebhookGuard.CheckUrl(kind, url, out _, allowPrivate: false) is null);
        Assert.Equal(on, WebhookGuard.CheckUrl(kind, url, out _, allowPrivate: true) is null);
    }

    // ---- the connect-time check: the real sender with a resolver and a connector the test controls

    private sealed class Net
    {
        public readonly Dictionary<string, Queue<IPAddress[]>> Answers = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<IPAddress> Connected = [];
        public bool AllowPrivate;
        public int Resolves;

        public void Answer(string host, params string[][] answers) =>
            Answers[host] = new Queue<IPAddress[]>(answers.Select(a => a.Select(IPAddress.Parse).ToArray()));

        public HttpWebhookTransport Sender() => new(
            (host, _) =>
            {
                Interlocked.Increment(ref Resolves);
                lock (Answers)
                    return ValueTask.FromResult(Answers.TryGetValue(host, out var q) && q.Count > 0 ? (q.Count > 1 ? q.Dequeue() : q.Peek()) : []);
            },
            () => AllowPrivate,
            (address, port, _) =>
            {
                lock (Connected) Connected.Add(address);
                // never a real connection to that address: a canned HTTP answer
                return ValueTask.FromResult<Stream>(new CannedHttp());
            });
    }

    /// <summary>A keep-alive connection that answers one request with 204 and then stays open (a read waits until the
    /// sender disposes it): if the sender reused it, the second request would get no answer and time out.</summary>
    private sealed class CannedHttp : Stream
    {
        private readonly MemoryStream _answer = new("HTTP/1.1 204 No Content\r\nContent-Length: 0\r\n\r\n"u8.ToArray());
        private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            var n = _answer.Read(buffer.Span);
            if (n > 0 || buffer.Length == 0) return n;
            await _closed.Task.WaitAsync(ct).ConfigureAwait(false);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) => ValueTask.CompletedTask;

        protected override void Dispose(bool disposing)
        {
            _closed.TrySetResult();
            base.Dispose(disposing);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => _answer.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) { }
    }

    /// <summary>Whether the send got past the checks to a connection (an https send then fails its TLS handshake on the
    /// canned stream, which is fine: the address was reached); false when it was refused.</summary>
    private static async Task<bool> Sends(Net net, HttpWebhookTransport s, string url)
    {
        int before;
        lock (net.Connected) before = net.Connected.Count;
        try
        {
            Assert.Equal(204, await s.SendAsync(new WebhookRequest(new Uri(url), "{}"u8.ToArray()), CancellationToken.None));
        }
        catch (Exception e) when (e is WebhookRefusedException || e.InnerException is WebhookRefusedException)
        {
            lock (net.Connected) Assert.Equal(before, net.Connected.Count);
            return false;
        }
        catch (HttpRequestException e) when (e.InnerException is System.Security.Authentication.AuthenticationException && url.StartsWith("https:", StringComparison.Ordinal))
        {
        }
        lock (net.Connected) return net.Connected.Count == before + 1;
    }

    [Theory]
    [InlineData("8.8.8.8", true, true)]
    [InlineData("10.0.0.5", false, true)]
    [InlineData("172.16.0.5", false, true)]
    [InlineData("192.168.1.20", false, true)]
    [InlineData("fd00::5", false, true)]
    [InlineData("::ffff:192.168.1.20", false, true)]
    [InlineData("127.0.0.1", false, false)]
    [InlineData("::1", false, false)]
    [InlineData("::ffff:127.0.0.1", false, false)]
    [InlineData("169.254.169.254", false, false)]
    [InlineData("::ffff:169.254.169.254", false, false)]
    [InlineData("fe80::1", false, false)]
    [InlineData("224.0.0.1", false, false)]
    [InlineData("ff02::1", false, false)]
    [InlineData("0.0.0.0", false, false)]
    [InlineData("::", false, false)]
    [InlineData("255.255.255.255", false, false)]
    [InlineData("100.64.0.1", false, false)]
    public async Task Connect_time_check_by_address_class(string ip, bool off, bool on)
    {
        var net = new Net();
        net.Answer("hook.example", [ip]);
        using var sender = net.Sender();
        net.AllowPrivate = false;
        Assert.Equal(off, await Sends(net, sender, "https://hook.example/in"));
        net.AllowPrivate = true;
        Assert.Equal(on, await Sends(net, sender, "https://hook.example/in"));
        // what it connected to is the address it checked
        Assert.All(net.Connected, a => Assert.Equal(IPAddress.Parse(ip), a));
        // a literal in the URL goes through the same check (no resolver involved)
        var literal = IPAddress.Parse(ip).AddressFamily == AddressFamily.InterNetworkV6 ? $"[{ip}]" : ip;
        net.AllowPrivate = false;
        Assert.Equal(off, await Sends(net, sender, $"https://{literal}/in"));
        net.AllowPrivate = true;
        Assert.Equal(on, await Sends(net, sender, $"https://{literal}/in"));
    }

    [Fact]
    public async Task Plain_http_only_reaches_private_addresses_with_the_opt_in()
    {
        var net = new Net();
        net.Answer("nas.lan", ["192.168.1.20"]);
        net.Answer("public.example", ["8.8.8.8"]);
        net.Answer("loop.example", ["127.0.0.1"]);
        using var sender = net.Sender();
        Assert.False(await Sends(net, sender, "http://nas.lan:8080/in"));          // off
        net.AllowPrivate = true;
        Assert.True(await Sends(net, sender, "http://nas.lan:8080/in"));
        Assert.False(await Sends(net, sender, "http://public.example/in"));       // cleartext over the internet: never
        Assert.False(await Sends(net, sender, "http://loop.example:8080/in"));
        Assert.Equal([IPAddress.Parse("192.168.1.20")], net.Connected);
    }

    [Fact]
    public async Task Dns_rebinding_cannot_get_past_the_connect_check()
    {
        var net = new Net();
        // the name looks public when the URL is checked (here: the first answer), then turns local
        net.Answer("rebind.example", ["8.8.8.8"], ["127.0.0.1"]);
        net.Answer("rebind2.example", ["8.8.8.8"], ["192.168.1.20"]);
        net.Answer("mixed.example", ["8.8.8.8", "127.0.0.1"]);
        net.Answer("mixed2.example", ["192.168.1.20", "169.254.169.254"]);
        using var sender = net.Sender();
        net.AllowPrivate = true;
        Assert.Null(WebhookGuard.CheckUrl("json", "https://rebind.example/in", out _, allowPrivate: true));
        Assert.True(await Sends(net, sender, "https://rebind.example/in"));          // public answer
        Assert.False(await Sends(net, sender, "https://rebind.example/in"));         // loopback answer: refused even with the opt-in
        Assert.True(await Sends(net, sender, "https://rebind2.example/in"));
        net.AllowPrivate = false;
        Assert.False(await Sends(net, sender, "https://rebind2.example/in"));        // private answer with the opt-in off
        net.AllowPrivate = true;
        Assert.False(await Sends(net, sender, "https://mixed.example/in"));          // any refused address refuses the name
        Assert.False(await Sends(net, sender, "https://mixed2.example/in"));
        Assert.Equal([IPAddress.Parse("8.8.8.8"), IPAddress.Parse("8.8.8.8")], net.Connected);
    }

    [Fact]
    public async Task Every_send_is_checked_again_no_pooled_connection_outlives_the_switch()
    {
        var net = new Net { AllowPrivate = true };
        net.Answer("nas.lan", ["192.168.1.20"]);
        using var sender = net.Sender();
        // plain http, so the canned keep-alive connection really answers (and would be reused if the sender pooled)
        Assert.True(await Sends(net, sender, "http://nas.lan/in"));
        Assert.True(await Sends(net, sender, "http://nas.lan/in"));
        Assert.Equal(2, net.Connected.Count);
        net.AllowPrivate = false;
        Assert.False(await Sends(net, sender, "http://nas.lan/in"));
        Assert.Equal(2, net.Connected.Count);
    }

    // ---- the switch: owner only, recent sign-in, audited, a warning in the answer, back off

    [Fact]
    public async Task Switch_is_owner_only_needs_a_recent_sign_in_is_audited_and_goes_back_off()
    {
        await using var c = await LaneC.StartAsync();
        var audit = new List<AuditEntry>();
        using var tap = c.H.Host.Services.Get<AuditLog>().Tap(e => { lock (audit) audit.Add(e); });
        using var owner = await c.UserAsync("olive", BuiltinRoles.Owner);
        using var admin = await c.UserAsync("adam", BuiltinRoles.Admin);
        using var viewer = await c.UserAsync("vic", BuiltinRoles.Viewer);
        var hook = new { name = "NAS", kind = "json", url = "https://192.168.1.20:8443/in", events = new[] { "admin.action" } };

        // off by default; admins (webhooks.view) see the state, viewers do not
        var s = await LaneC.JsonAsync(await admin.GetAsync("/api/webhooks/settings"));
        Assert.False(s!["allowPrivateTargets"]!.GetValue<bool>());
        Assert.False(s["canChange"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/webhooks/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await LaneC.PutAsync(viewer, "/api/webhooks/settings", new { allowPrivateTargets = true })).StatusCode);
        await LaneC.ReauthAsync(owner);
        Assert.Equal(HttpStatusCode.UnprocessableContent, (await owner.PostAsync("/api/webhooks/hooks", hook)).StatusCode);

        // not an owner: refused even after a recent sign-in
        await LaneC.ReauthAsync(admin);
        Assert.Equal(HttpStatusCode.Forbidden, (await LaneC.PutAsync(admin, "/api/webhooks/settings", new { allowPrivateTargets = true })).StatusCode);
        // an owner without a recent sign-in
        using var owner2 = await c.UserAsync("oscar", BuiltinRoles.Owner);
        Assert.Equal("reauth_required", await LaneC.CodeAsync(await LaneC.PutAsync(owner2, "/api/webhooks/settings", new { allowPrivateTargets = true })));
        Assert.Equal(HttpStatusCode.UnprocessableContent, (await LaneC.PutAsync(owner, "/api/webhooks/settings", new { allowPrivateTargets = "yes" })).StatusCode);
        Assert.False(c.Webhooks.AllowPrivateTargets);

        // on
        var r = await LaneC.PutAsync(owner, "/api/webhooks/settings", new { allowPrivateTargets = true });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.True(c.Webhooks.AllowPrivateTargets);
        s = await LaneC.JsonAsync(await owner.GetAsync("/api/webhooks/settings"));
        Assert.True(s!["allowPrivateTargets"]!.GetValue<bool>());
        Assert.True(s["canChange"]!.GetValue<bool>());
        Assert.Contains("private", s["warning"]!.GetValue<string>());
        Assert.Contains(c.H.Log.Lines, l => l.Contains("private", StringComparison.OrdinalIgnoreCase) && l.Contains("webhook", StringComparison.OrdinalIgnoreCase));
        r = await owner.PostAsync("/api/webhooks/hooks", hook);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var id = (await LaneC.JsonAsync(r))!["hook"]!["id"]!.GetValue<string>();
        Assert.Equal(HttpStatusCode.UnprocessableContent, (await owner.PostAsync("/api/webhooks/hooks", hook with { url = "https://127.0.0.1:8443/in" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableContent, (await owner.PostAsync("/api/webhooks/hooks", hook with { url = "http://localhost:8080/in" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableContent, (await owner.PostAsync("/api/webhooks/hooks", hook with { url = "https://169.254.169.254/latest" })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await owner.PostAsync($"/api/webhooks/hooks/{id}/test", null)).StatusCode);
        await Eventually.True(() => c.Transport.Count >= 1, 5000, "the private delivery");

        // off again: the stored hook stays, its deliveries are refused
        Assert.Equal(HttpStatusCode.OK, (await LaneC.PutAsync(owner, "/api/webhooks/settings", new { allowPrivateTargets = false })).StatusCode);
        Assert.False(c.Webhooks.AllowPrivateTargets);
        var before = c.Transport.Count;
        Assert.Equal(HttpStatusCode.Accepted, (await owner.PostAsync($"/api/webhooks/hooks/{id}/test", null)).StatusCode);
        await Eventually.True(() => c.Webhooks.Dispatcher!.Deliveries().Count >= 2, 5000, "the second delivery");
        Assert.Equal(before, c.Transport.Count);
        Assert.StartsWith("refused:", c.Webhooks.Dispatcher!.Deliveries()[0]!["error"]!.GetValue<string>());

        // both switches audited with the account and the new value
        List<AuditEntry> seen;
        lock (audit) seen = [.. audit.Where(e => e.Op == "webhooks.private_targets")];
        Assert.Contains(seen, e => e.Ok && e.UserName == "olive" && e.Args!["allowPrivateTargets"]!.GetValue<bool>());
        Assert.Contains(seen, e => e.Ok && e.UserName == "olive" && !e.Args!["allowPrivateTargets"]!.GetValue<bool>());
        Assert.Contains(seen, e => !e.Ok && e.UserName == "adam");
        // and the file that keeps it
        Assert.Contains("\"allowPrivateTargets\": false", File.ReadAllText(Path.Combine(c.H.DataDir, WebhooksComponent.SettingsFileName)));
    }
}
