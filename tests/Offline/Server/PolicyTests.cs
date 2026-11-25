// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using Kilnwarden.Config;
using Kilnwarden.Http;
using Kilnwarden.Server;
using Kilnwarden.WebSockets;

namespace Kilnwarden.Offline.Server;

public class PolicyTests
{
    private static HttpRequest Req(string raw) =>
        HttpParser.TryParseHead(Encoding.Latin1.GetBytes(raw), HttpLimits.Default, out var r, out _) == ParseStatus.Complete ? r! : throw new Exception("incomplete");

    private static ServerPolicy Policy(Action<ServerSettings>? edit = null, bool publicBind = false)
    {
        var s = new ServerSettings();
        edit?.Invoke(s);
        return ServerPolicy.From(s, publicBind);
    }

    [Theory]
    [InlineData("localhost:15737", true)]
    [InlineData("LOCALHOST", true)]
    [InlineData("127.0.0.1:15737", true)]
    [InlineData("[::1]:15737", true)]
    [InlineData("192.168.1.20", true)]
    [InlineData("rebind.attacker.example", false)]
    [InlineData("panel.example.com:15737", true)]
    [InlineData("other.example.com", false)]
    public void Host_check_on_loopback(string host, bool ok) =>
        Assert.Equal(ok, Policy(s => s.Tls.Hostnames = ["panel.example.com"]).HostAllowed(host));

    [Fact]
    public void Host_check_on_public_bind()
    {
        Assert.False(Policy(publicBind: true).HostAllowed("anything.example"));
        var strict = Policy(s => s.AllowedHosts = ["panel.example.com"], publicBind: true);
        Assert.True(strict.HostAllowed("panel.example.com"));
        Assert.True(strict.HostAllowed("127.0.0.1"));
        Assert.False(strict.HostAllowed("anything.example"));
    }

    [Fact]
    public void Forwarded_headers_only_from_trusted_proxies()
    {
        var req = Req("GET / HTTP/1.1\r\nHost: 127.0.0.1:15737\r\nX-Forwarded-For: 203.0.113.9, 10.0.0.5\r\nX-Forwarded-Proto: https\r\nX-Forwarded-Host: panel.example.com\r\n\r\n");
        var off = Policy();
        var a = off.Resolve(req, IPAddress.Loopback, isTls: false);
        Assert.Equal((IPAddress.Loopback, "http", "127.0.0.1:15737"), (a.Client, a.Scheme, a.Host));

        var on = Policy(s => { s.ReverseProxy.Enabled = true; s.ReverseProxy.TrustedProxies = ["127.0.0.1", "10.0.0.0/8"]; });
        var b = on.Resolve(req, IPAddress.Loopback, isTls: false);
        Assert.Equal((IPAddress.Parse("203.0.113.9"), "https", "panel.example.com"), (b.Client, b.Scheme, b.Host));

        var untrustedPeer = on.Resolve(req, IPAddress.Parse("198.51.100.1"), isTls: true);
        Assert.Equal(IPAddress.Parse("198.51.100.1"), untrustedPeer.Client);

        var onlyLocal = Policy(s => s.ReverseProxy.Enabled = true);
        Assert.Equal(IPAddress.Parse("10.0.0.5"), onlyLocal.Resolve(req, IPAddress.Loopback, false).Client);  // rightmost untrusted
    }

    [Theory]
    [InlineData("X-Forwarded-For: garbage")]
    [InlineData("X-Forwarded-For: 1.2.3.4, ,")]
    [InlineData("X-Forwarded-Proto: gopher")]
    [InlineData("X-Forwarded-Host: a/b")]
    public void Bad_forwarded_values_fail_closed(string header)
    {
        var on = Policy(s => s.ReverseProxy.Enabled = true);
        Assert.Throws<HttpError>(() => on.Resolve(Req($"GET / HTTP/1.1\r\nHost: h\r\n{header}\r\n\r\n"), IPAddress.Loopback, false));
    }

    [Fact]
    public void Trusted_proxy_may_pass_the_host_header_instead_of_x_forwarded_host()
    {
        // nginx's usual recipe: proxy_set_header Host $host, X-Forwarded-For and X-Forwarded-Proto, no X-Forwarded-Host
        var on = Policy(s => s.ReverseProxy.Enabled = true);
        var r = on.Resolve(Req("GET / HTTP/1.1\r\nHost: panel.example.com\r\nX-Forwarded-For: 203.0.113.9\r\nX-Forwarded-Proto: https\r\n\r\n"), IPAddress.Loopback, false);
        Assert.Equal((IPAddress.Parse("203.0.113.9"), "https", "panel.example.com"), (r.Client, r.Scheme, r.Host));
        // the client chain and the HTTPS scheme stay required once the proxy forwards anything
        foreach (var partial in new[] { "X-Forwarded-For: 203.0.113.9\r\n", "X-Forwarded-Proto: https\r\n",
                     "X-Forwarded-For: 203.0.113.9\r\nX-Forwarded-Proto: http\r\n", "X-Forwarded-Host: panel.example.com\r\n" })
            Assert.Throws<HttpError>(() => on.Resolve(Req($"GET / HTTP/1.1\r\nHost: panel.example.com\r\n{partial}\r\n"), IPAddress.Loopback, false));
    }

    [Fact]
    public void Allowlist_always_lets_loopback_in()
    {
        var p = Policy(s => s.IpAllowlist = ["192.168.1.0/24"]);
        Assert.True(p.ClientAllowed(IPAddress.Loopback));
        Assert.True(p.ClientAllowed(IPAddress.Parse("192.168.1.4")));
        Assert.False(p.ClientAllowed(IPAddress.Parse("8.8.8.8")));
        Assert.True(Policy().ClientAllowed(IPAddress.Parse("8.8.8.8")));
    }

    [Fact]
    public void Limiter_caps_connections_per_ip_and_rates()
    {
        var time = new ManualTime();
        var l = new ClientLimiter(time);
        var limits = new LimitSettings { MaxConnectionsPerIp = 2, ConnectionsPerSecondPerIp = 1000, RequestsPerSecondPerIp = 10, RequestBurstPerIp = 3 };
        var ip = IPAddress.Parse("203.0.113.1");
        Assert.True(l.TryOpen(ip, limits, exempt: false));
        Assert.True(l.TryOpen(ip, limits, exempt: false));
        Assert.False(l.TryOpen(ip, limits, exempt: false));
        Assert.True(l.TryOpen(ip, limits, exempt: true));   // counts, but is not capped
        l.Close(ip);
        Assert.False(l.TryOpen(ip, limits, exempt: false));
        l.Close(ip);
        Assert.True(l.TryOpen(ip, limits, exempt: false));
        Assert.True(l.TryRequest(ip, limits) && l.TryRequest(ip, limits) && l.TryRequest(ip, limits));
        Assert.False(l.TryRequest(ip, limits));
        time.Advance(TimeSpan.FromMilliseconds(200)); // 2 tokens back
        Assert.True(l.TryRequest(ip, limits) && l.TryRequest(ip, limits));
        Assert.False(l.TryRequest(ip, limits));
        Assert.True(l.TryRequest(IPAddress.Parse("203.0.113.2"), limits));
    }

    [Fact]
    public void Limiter_caps_new_connections_per_second()
    {
        var time = new ManualTime();
        var l = new ClientLimiter(time);
        var limits = new LimitSettings { MaxConnectionsPerIp = 1000, ConnectionsPerSecondPerIp = 5 };
        var ip = IPAddress.Parse("203.0.113.1");
        var opened = 0;
        for (var i = 0; i < 50; i++)
            if (l.TryOpen(ip, limits, exempt: false)) opened++;
        Assert.InRange(opened, 5, 10);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(l.TryOpen(ip, limits, exempt: false));
    }

    [Fact]
    public void Security_headers_defaults()
    {
        var r = HttpResponse.Text(200, "x");
        SecurityHeaders.Apply(r, https: true, host: "panel.example.com", hsts: true);
        Assert.Equal("nosniff", r.Headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", r.Headers["X-Frame-Options"]);
        Assert.Equal("no-referrer", r.Headers["Referrer-Policy"]);
        Assert.Equal("same-origin", r.Headers["Cross-Origin-Opener-Policy"]);
        Assert.Contains("default-src 'self'", r.Headers["Content-Security-Policy"]);
        Assert.Contains("frame-ancestors 'none'", r.Headers["Content-Security-Policy"]);
        Assert.DoesNotContain("unsafe", r.Headers["Content-Security-Policy"]);
        Assert.Equal("no-store", r.Headers["Cache-Control"]);
        Assert.StartsWith("max-age=", r.Headers["Strict-Transport-Security"]);
    }

    [Theory]
    [InlineData(false, "panel.example.com")]
    [InlineData(true, "localhost:15737")]
    [InlineData(true, "127.0.0.1")]
    [InlineData(true, "[::1]:1")]
    public void No_hsts_on_http_localhost_or_ip_literals(bool https, string host)
    {
        var r = HttpResponse.Text(200, "x");
        SecurityHeaders.Apply(r, https, host, hsts: true);
        Assert.Null(r.Headers["Strict-Transport-Security"]);
    }

    [Fact]
    public void Security_headers_keep_what_the_handler_set()
    {
        var r = HttpResponse.Text(200, "x");
        r.Headers.Set("Cache-Control", "public, max-age=31536000, immutable");
        r.Headers.Set("Content-Security-Policy", "sandbox");
        SecurityHeaders.Apply(r, true, "localhost", true);
        Assert.Equal("public, max-age=31536000, immutable", r.Headers["Cache-Control"]);
        Assert.Equal("sandbox", r.Headers["Content-Security-Policy"]);
    }

    private const string Ws = "GET /api/core/ws HTTP/1.1\r\nHost: localhost:15737\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Version: 13\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n";

    [Fact]
    public void WebSocket_upgrade_accept_key_rfc6455_sample()
    {
        var r = WebSocketUpgrade.Check(Req(Ws + "Origin: https://localhost:15737\r\n\r\n"), "https://localhost:15737");
        Assert.True(r.Ok, r.Message);
        Assert.Equal("s3pPLMBiTxaQ9kYGzzhZRbK+xOo=", r.Accept);
    }

    [Theory]
    [InlineData("Origin: https://evil.example\r\n", 403)]
    [InlineData("Origin: null\r\n", 403)]
    [InlineData("", 403)]
    [InlineData("Origin: http://localhost:15737\r\n", 403)]
    public void WebSocket_upgrade_needs_the_same_origin(string origin, int status)
    {
        var r = WebSocketUpgrade.Check(Req(Ws + origin + "\r\n"), "https://localhost:15737");
        Assert.False(r.Ok);
        Assert.Equal(status, r.Status);
    }

    [Theory]
    [InlineData("Sec-WebSocket-Version: 13", "Sec-WebSocket-Version: 8", 426)]
    [InlineData("Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==", "Sec-WebSocket-Key: short", 400)]
    [InlineData("Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==", "Sec-WebSocket-Key: !!!!!!!!!!!!!!!!!!!!!!!!", 400)]
    [InlineData("Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n", "", 400)]
    public void WebSocket_upgrade_checks_version_and_key(string from, string to, int status)
    {
        var r = WebSocketUpgrade.Check(Req((Ws + "Origin: https://localhost:15737\r\n\r\n").Replace(from, to)), "https://localhost:15737");
        Assert.False(r.Ok);
        Assert.Equal(status, r.Status);
    }

    [Fact]
    public void WebSocket_origin_compare_is_case_insensitive_on_scheme_and_host()
    {
        var r = WebSocketUpgrade.Check(Req(Ws + "Origin: HTTPS://LocalHost:15737\r\n\r\n"), "https://localhost:15737");
        Assert.True(r.Ok, r.Message);
    }
}

public sealed class ManualTime : TimeProvider
{
    private long _ticks = 1_000_000_000;
    public override long GetTimestamp() => _ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero).AddTicks(_ticks);
    public void Advance(TimeSpan by) => _ticks += by.Ticks;
}
