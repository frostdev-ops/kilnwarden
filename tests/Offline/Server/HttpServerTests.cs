// SPDX-License-Identifier: MIT
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using Kilnwarden.Http;
using Kilnwarden.Offline.Support;
using Kilnwarden.Routing;

namespace Kilnwarden.Offline.Server;

public class HttpServerTests
{
    private static string Get(string path, string extra = "") => $"GET {path} HTTP/1.1\r\nHost: 127.0.0.1\r\n{extra}\r\n";

    [Fact]
    public async Task Keep_alive_serves_several_requests_with_security_headers()
    {
        using var f = new ServerFixture();
        using var c = f.Connect();
        var s = c.GetStream();
        for (var i = 0; i < 3; i++)
        {
            var r = await ServerFixture.SendAsync(s, Get("/api/t/hello"));
            Assert.Equal(200, r.Status);
            Assert.Equal("hello", r.Body);
            Assert.Equal("keep-alive", r.Headers["Connection"]);
            Assert.Equal("nosniff", r.Headers["X-Content-Type-Options"]);
            Assert.Contains("default-src 'self'", r.Headers["Content-Security-Policy"]);
            Assert.True(r.Headers.ContainsKey("Date"));
            Assert.False(r.Headers.ContainsKey("Server"));
            Assert.False(r.Headers.ContainsKey("Strict-Transport-Security"));
        }
    }

    [Fact]
    public async Task Pipelined_requests_answer_in_order()
    {
        using var f = new ServerFixture();
        using var c = f.Connect();
        var s = c.GetStream();
        await s.WriteAsync(Encoding.ASCII.GetBytes(Get("/api/t/slow") + Get("/api/t/hello") + "POST /api/t/echo HTTP/1.1\r\nHost: h\r\nContent-Length: 3\r\n\r\nabc"));
        Assert.Equal("slow", (await RawResponse.ReadAsync(s)).Body);
        Assert.Equal("hello", (await RawResponse.ReadAsync(s)).Body);
        Assert.Equal("abc", (await RawResponse.ReadAsync(s)).Body);
    }

    [Fact]
    public async Task Post_body_and_expect_continue()
    {
        using var f = new ServerFixture();
        using var c = f.Connect();
        var s = c.GetStream();
        await s.WriteAsync("POST /api/t/echo HTTP/1.1\r\nHost: h\r\nContent-Length: 5\r\nExpect: 100-continue\r\n\r\n"u8.ToArray());
        Assert.Equal(100, (await RawResponse.ReadAsync(s)).Status);
        await s.WriteAsync("hello"u8.ToArray());
        var r = await RawResponse.ReadAsync(s);
        Assert.Equal((200, "hello"), (r.Status, r.Body));
    }

    [Fact]
    public async Task Body_over_the_route_cap_is_413_and_closes_without_reading_it()
    {
        using var f = new ServerFixture();
        using var c = f.Connect();
        var s = c.GetStream();
        var r = await ServerFixture.SendAsync(s, "POST /api/t/small HTTP/1.1\r\nHost: h\r\nContent-Length: 11\r\n\r\n");
        Assert.Equal(413, r.Status);
        Assert.Equal("close", r.Headers["Connection"]);
        Assert.True(await s.ClosedWithinAsync(2000));
        using var c2 = f.Connect();
        Assert.Equal("10", (await ServerFixture.SendAsync(c2.GetStream(), "POST /api/t/small HTTP/1.1\r\nHost: h\r\nContent-Length: 10\r\n\r\n0123456789")).Body);
    }

    /// <summary>Security review I4: a body for a path no route serves is not read (nor allocated): 404 and close.</summary>
    [Fact]
    public async Task A_body_for_an_unknown_route_is_not_read()
    {
        using var f = new ServerFixture(s => s.Limits.MaxBodyBytes = 64 << 20);
        using var c = f.Connect();
        var s = c.GetStream();
        var r = await ServerFixture.SendAsync(s, "POST /api/t/nothing-here HTTP/1.1\r\nHost: h\r\nContent-Length: 60000000\r\n\r\n");
        Assert.Equal(404, r.Status);
        Assert.Equal("close", r.Headers["Connection"]);
        Assert.True(await s.ClosedWithinAsync(2000));
    }

    [Fact]
    public async Task Global_body_cap()
    {
        using var f = new ServerFixture(s => s.Limits.MaxBodyBytes = 100);
        using var c = f.Connect();
        var r = await ServerFixture.SendAsync(c.GetStream(), "POST /api/t/echo HTTP/1.1\r\nHost: h\r\nContent-Length: 101\r\n\r\n");
        Assert.Equal(413, r.Status);
    }

    [Fact]
    public async Task Bad_request_is_answered_and_closed()
    {
        using var f = new ServerFixture();
        using var c = f.Connect();
        var s = c.GetStream();
        var r = await ServerFixture.SendAsync(s, "GET / HTTP/1.1\r\nHost: h\r\nTransfer-Encoding: chunked\r\n\r\n");
        Assert.Equal(501, r.Status);
        Assert.Equal("close", r.Headers["Connection"]);
        Assert.True(await s.ClosedWithinAsync(2000));
    }

    [Theory]
    [InlineData("/api/t/../t/hello")]
    [InlineData("/api/t/%2e%2e/x")]
    [InlineData("/a//b")]
    public async Task Bad_paths_are_400(string path)
    {
        using var f = new ServerFixture();
        using var c = f.Connect();
        Assert.Equal(400, (await ServerFixture.SendAsync(c.GetStream(), Get(path))).Status);
    }

    [Fact]
    public async Task Routing_errors_gate_and_handler_faults()
    {
        using var f = new ServerFixture();
        using var c = f.Connect();
        var s = c.GetStream();
        var notFound = await ServerFixture.SendAsync(s, Get("/nothing"));
        Assert.Equal((404, "fallback"), (notFound.Status, notFound.Body));
        var m405 = await ServerFixture.SendAsync(s, "DELETE /api/t/hello HTTP/1.1\r\nHost: h\r\n\r\n");
        Assert.Equal(405, m405.Status);
        Assert.Equal("GET, HEAD", m405.Headers["Allow"]);
        var unauth = await ServerFixture.SendAsync(s, Get("/api/t/me"));
        Assert.Equal(401, unauth.Status);
        Assert.Contains("\"code\":\"unauthenticated\"", unauth.Body);
        var boom = await ServerFixture.SendAsync(s, Get("/api/t/boom"));
        Assert.Equal(500, boom.Status);
        Assert.DoesNotContain("boom", boom.Body);
        Assert.True(f.Log.Has("boom"));
        var refused = await ServerFixture.SendAsync(s, Get("/api/t/refuse"));
        Assert.Equal(409, refused.Status);
        Assert.Contains("nope", refused.Body);
        Assert.Equal(200, (await ServerFixture.SendAsync(s, Get("/api/t/hello"))).Status); // still usable
    }

    [Fact]
    public async Task Head_has_length_but_no_body()
    {
        using var f = new ServerFixture();
        using var c = f.Connect();
        var s = c.GetStream();
        await s.WriteAsync(Encoding.ASCII.GetBytes("HEAD /api/t/hello HTTP/1.1\r\nHost: h\r\n\r\n"));
        var r = await RawResponse.ReadAsync(s, headOnly: true);
        Assert.Equal("5", r.Headers["Content-Length"]);
        Assert.Equal(200, (await ServerFixture.SendAsync(s, Get("/api/t/hello"))).Status);
    }

    [Fact]
    public async Task Slowloris_headers_are_cut_at_the_header_deadline()
    {
        using var f = new ServerFixture(s => s.Limits.HeaderTimeoutMs = 400);
        using var c = f.Connect();
        var s = c.GetStream();
        var sw = Stopwatch.StartNew();
        var closed = false;
        foreach (var b in Encoding.ASCII.GetBytes("GET /api/t/hello HTTP/1.1\r\nHost: h\r\nX-Slow: aaaaaaaaaaaaaaaaaaaaaaaaaaaa"))
        {
            try
            {
                await s.WriteAsync(new[] { b });
            }
            catch (IOException)
            {
                closed = true;
                break;
            }
            await Task.Delay(50);
            if (sw.ElapsedMilliseconds > 3000) break;
        }
        Assert.True(closed || await s.ClosedWithinAsync(1000));
        Assert.InRange(sw.ElapsedMilliseconds, 300, 2500);
    }

    [Fact]
    public async Task Idle_keep_alive_closes_after_the_timeout()
    {
        using var f = new ServerFixture(s => s.Limits.KeepAliveTimeoutMs = 300);
        using var c = f.Connect();
        var s = c.GetStream();
        Assert.Equal(200, (await ServerFixture.SendAsync(s, Get("/api/t/hello"))).Status);
        var sw = Stopwatch.StartNew();
        Assert.True(await s.ClosedWithinAsync(2000));
        Assert.InRange(sw.ElapsedMilliseconds, 200, 1500);
    }

    [Fact]
    public async Task Slow_body_is_cut()
    {
        using var f = new ServerFixture(s => s.Limits.BodyTimeoutMs = 300);
        using var c = f.Connect();
        var s = c.GetStream();
        await s.WriteAsync("POST /api/t/echo HTTP/1.1\r\nHost: h\r\nContent-Length: 100\r\n\r\nabc"u8.ToArray());
        Assert.True(await s.ClosedWithinAsync(2500));
    }

    [Fact]
    public async Task Connection_caps()
    {
        using var f = new ServerFixture(s => { s.Limits.MaxConnections = 2; s.Limits.MaxConnectionsPerIp = 10; });
        using var a = f.Connect();
        using var b = f.Connect();
        Assert.Equal(200, (await ServerFixture.SendAsync(a.GetStream(), Get("/api/t/hello"))).Status);
        Assert.Equal(200, (await ServerFixture.SendAsync(b.GetStream(), Get("/api/t/hello"))).Status);
        // The admission rejection can reset TCP during Connect, or close it immediately afterwards. Both reject
        // this third client; a reset must not escape the test just because it raced the connect handshake.
        using var third = new TcpClient();
        var reset = Record.Exception(() => third.Connect(IPAddress.Loopback, f.Port));
        if (reset is null) Assert.True(await third.GetStream().ClosedWithinAsync(2000));
        else Assert.Equal(SocketError.ConnectionReset, Assert.IsType<SocketException>(reset).SocketErrorCode);
        a.Dispose();
        await Eventually.True(() => Volatile.Read(ref f.Shared.OpenConnections) == 1, 5000, "the first connection releases its admission slot");
        using var fourth = f.Connect();
        Assert.Equal(200, (await ServerFixture.SendAsync(fourth.GetStream(), Get("/api/t/hello"))).Status);
    }

    [Fact]
    public async Task Per_ip_request_rate_gives_429()
    {
        using var f = new ServerFixture(s => { s.Limits.RequestsPerSecondPerIp = 1; s.Limits.RequestBurstPerIp = 3; });
        using var c = f.Connect();
        var s = c.GetStream();
        var statuses = new List<int>();
        for (var i = 0; i < 5; i++) statuses.Add((await ServerFixture.SendAsync(s, Get("/api/t/hello"))).Status);
        Assert.Equal([200, 200, 200, 429, 429], statuses);
    }

    [Fact]
    public async Task Unknown_host_names_are_refused()
    {
        using var f = new ServerFixture();
        using var c = f.Connect();
        var r = await ServerFixture.SendAsync(c.GetStream(), "GET /api/t/hello HTTP/1.1\r\nHost: rebind.attacker.example\r\n\r\n");
        Assert.Equal(421, r.Status);
        Assert.Equal(0, f.HandlerCalls);
    }

    [Fact]
    public async Task Max_requests_per_connection()
    {
        using var f = new ServerFixture(s => s.Limits.MaxRequestsPerConnection = 2);
        using var c = f.Connect();
        var s = c.GetStream();
        Assert.Equal("keep-alive", (await ServerFixture.SendAsync(s, Get("/api/t/hello"))).Headers["Connection"]);
        Assert.Equal("close", (await ServerFixture.SendAsync(s, Get("/api/t/hello"))).Headers["Connection"]);
        Assert.True(await s.ClosedWithinAsync(2000));
    }

    [Fact]
    public async Task Forwarded_client_behind_a_trusted_proxy()
    {
        using var f = new ServerFixture(s => s.ReverseProxy.Enabled = true);
        using var c = f.Connect();
        var r = await ServerFixture.SendAsync(c.GetStream(),
            Get("/api/t/client", "X-Forwarded-For: 203.0.113.7\r\nX-Forwarded-Proto: https\r\nX-Forwarded-Host: localhost:443\r\n"));
        Assert.Equal("203.0.113.7 https localhost:443", r.Body);
    }

    [Fact]
    public async Task WebSocket_echo_and_close_1001_on_stop()
    {
        var f = new ServerFixture();
        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Origin", f.Origin);
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{f.Port}/api/t/ws"), CancellationToken.None);
        await ws.SendAsync("ping"u8.ToArray(), WebSocketMessageType.Text, true, CancellationToken.None);
        var buf = new byte[64];
        var r = await ws.ReceiveAsync(buf, CancellationToken.None);
        Assert.Equal("ping", Encoding.UTF8.GetString(buf, 0, r.Count));

        var stopping = Task.Run(() => f.Server.Stop(1000));
        r = await ws.ReceiveAsync(buf, new CancellationTokenSource(3000).Token);
        Assert.Equal(WebSocketMessageType.Close, r.MessageType);
        Assert.Equal(WebSocketCloseStatus.EndpointUnavailable, ws.CloseStatus);
        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        var report = await stopping;
        Assert.Equal(1, report.Connections);
        Assert.Equal(0, report.Aborted);
        Assert.True(report.AcceptEnded);
    }

    [Fact]
    public async Task WebSocket_from_another_origin_is_403()
    {
        using var f = new ServerFixture();
        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Origin", "http://evil.example");
        var e = await Assert.ThrowsAsync<WebSocketException>(() => ws.ConnectAsync(new Uri($"ws://127.0.0.1:{f.Port}/api/t/ws"), CancellationToken.None));
        Assert.Contains("403", e.Message);
    }

    [Fact]
    public async Task WebSocket_route_without_upgrade_is_426()
    {
        using var f = new ServerFixture();
        using var c = f.Connect();
        Assert.Equal(426, (await ServerFixture.SendAsync(c.GetStream(), Get("/api/t/ws"))).Status);
    }

    [Fact]
    public async Task WebSocket_message_cap_closes_1009()
    {
        using var f = new ServerFixture(s => s.Limits.MaxWebSocketMessageBytes = 1024);
        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Origin", f.Origin);
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{f.Port}/api/t/ws"), CancellationToken.None);
        await ws.SendAsync(new byte[5000], WebSocketMessageType.Binary, true, CancellationToken.None);
        var r = await ws.ReceiveAsync(new byte[64], new CancellationTokenSource(3000).Token);
        Assert.Equal(WebSocketMessageType.Close, r.MessageType);
        Assert.Equal(WebSocketCloseStatus.MessageTooBig, ws.CloseStatus);
    }

    [Fact]
    public async Task Stop_releases_the_port_and_closes_idle_and_busy_connections()
    {
        var f = new ServerFixture();
        var port = f.Port;
        using var idle = f.Connect();
        Assert.Equal(200, (await ServerFixture.SendAsync(idle.GetStream(), Get("/api/t/hello"))).Status);
        using var busy = f.Connect();
        await busy.GetStream().WriteAsync(Encoding.ASCII.GetBytes(Get("/api/t/slow")));
        await Task.Delay(100);
        var sw = Stopwatch.StartNew();
        var report = f.Server.Stop(1000);
        Assert.InRange(sw.ElapsedMilliseconds, 0, 1600);
        Assert.Equal(2, report.Connections);
        Assert.True(report.AcceptEnded);
        Assert.True(await idle.GetStream().ClosedWithinAsync(1000));
        // the busy request finished (graceful) before its connection closed
        var r = await RawResponse.ReadAsync(busy.GetStream());
        Assert.Equal("slow", r.Body);
        Assert.Equal("close", r.Headers["Connection"]);
        using var again = new TcpListener(IPAddress.Loopback, port);
        again.ExclusiveAddressUse = true;
        again.Start();
        again.Stop();
    }

    [Fact]
    public async Task Many_parallel_clients()
    {
        using var f = new ServerFixture(s => { s.Limits.RequestsPerSecondPerIp = 100000; s.Limits.RequestBurstPerIp = 100000; s.Limits.ConnectionsPerSecondPerIp = 1000; s.Limits.MaxConnectionsPerIp = 1000; });
        using var http = new HttpClient();
        var tasks = Enumerable.Range(0, 200).Select(_ => http.GetStringAsync($"http://127.0.0.1:{f.Port}/api/t/hello"));
        var results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.Equal("hello", r));
    }
}
