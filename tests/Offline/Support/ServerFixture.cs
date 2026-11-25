// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Sockets;
using System.Text;
using Kilnwarden.Config;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Modules;
using Kilnwarden.Routing;
using Kilnwarden.Security;
using Kilnwarden.Server;
using Kilnwarden.Tls;

namespace Kilnwarden.Offline.Support;

/// <summary>One HttpServer on 127.0.0.1:0 with a "t" module of test routes.</summary>
public sealed class ServerFixture : IDisposable
{
    public readonly TestLog Log = new();
    public readonly ModuleRegistry Registry = new();
    public readonly WebAdminServices Services = new();
    public readonly ServerSettings Settings = new();
    public readonly HttpServer Server;
    public readonly ServerShared Shared;
    public readonly List<IHttpMiddleware> Middleware = [];
    public volatile int HandlerCalls;

    public ServerFixture(Action<ServerSettings>? edit = null, ITlsServer? tls = null, Action<ModuleRegistration>? routes = null, bool publicBind = false,
        IAccessGate? gate = null)
    {
        Settings.AllowedHosts = ["h"];
        Settings.Limits.HeaderTimeoutMs = 2000;
        Settings.Limits.KeepAliveTimeoutMs = 3000;
        Settings.Limits.BodyTimeoutMs = 2000;
        Settings.Limits.HandshakeTimeoutMs = 2000;
        edit?.Invoke(Settings);
        var m = Registry.Register(new ModuleSpec { Id = "t", Title = "t", Permissions = [new PermissionSpec("t.secret", "s")] }, "test");
        m.MapGet("/hello", RouteAccess.Public, _ =>
        {
            Interlocked.Increment(ref HandlerCalls);
            return ValueTask.FromResult(HttpResponse.Text(200, "hello"));
        });
        m.MapPost("/echo", RouteAccess.Public, ctx => ValueTask.FromResult(HttpResponse.Bytes(200, "application/octet-stream", ctx.Request.Body)));
        m.MapPost("/small", RouteAccess.Public, ctx => ValueTask.FromResult(HttpResponse.Text(200, ctx.Request.Body.Length.ToString())), maxBodyBytes: 10);
        m.MapGet("/me", RouteAccess.SignedIn, ctx => ValueTask.FromResult(HttpResponse.Text(200, ctx.User!.Id)));
        m.MapGet("/boom", RouteAccess.Public, _ => throw new InvalidOperationException("boom"));
        m.MapGet("/refuse", RouteAccess.Public, _ => throw new HttpError(409, "nope"));
        m.MapGet("/slow", RouteAccess.Public, async ctx =>
        {
            await Task.Delay(400, ctx.Aborted);
            return HttpResponse.Text(200, "slow");
        });
        m.MapGet("/client", RouteAccess.Public, ctx => ValueTask.FromResult(HttpResponse.Text(200, $"{ctx.ClientAddress} {ctx.Scheme} {ctx.Host}")));
        m.MapWebSocket("/ws", RouteAccess.Public, async ws =>
        {
            while (await ws.ReceiveAsync() is { } msg) await ws.SendAsync(msg.Data, msg.Type);
        });
        routes?.Invoke(m);
        var policy = ServerPolicy.From(Settings, publicBind);
        var pipeline = new RequestPipeline(Registry, Middleware, gate ?? new DefaultAccessGate(),
            _ => ValueTask.FromResult(HttpResponse.Text(404, "fallback")), Log);
        Shared = new ServerShared(pipeline, Services, Log, new ClientLimiter(TimeProvider.System), () => policy);
        Server = new HttpServer("test", new IPEndPoint(IPAddress.Loopback, 0), tls, Shared);
        Server.Start();
    }

    public int Port => Server.LocalEndPoint.Port;
    public string Origin => $"http://127.0.0.1:{Port}";

    public TcpClient Connect()
    {
        var c = new TcpClient();
        c.Connect(IPAddress.Loopback, Port);
        c.ReceiveTimeout = 5000;
        return c;
    }

    /// <summary>Sends raw bytes and reads one response (status, headers, body) with Content-Length framing.</summary>
    public static async Task<RawResponse> SendAsync(Stream s, string raw)
    {
        await s.WriteAsync(Encoding.Latin1.GetBytes(raw));
        return await RawResponse.ReadAsync(s);
    }

    public void Dispose() => Server.Stop(1000);
}

public sealed record RawResponse(int Status, Dictionary<string, string> Headers, string Body)
{
    public static async Task<RawResponse> ReadAsync(Stream s, bool headOnly = false)
    {
        var line = await ReadLineAsync(s) ?? throw new EndOfStreamException("no response");
        var status = int.Parse(line.Split(' ')[1]);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (await ReadLineAsync(s) is { Length: > 0 } h)
        {
            var c = h.IndexOf(':');
            headers[h[..c]] = h[(c + 1)..].Trim();
        }
        var len = headOnly || status is 100 or 101 or 204 or 304 ? 0 : headers.TryGetValue("Content-Length", out var cl) ? int.Parse(cl) : 0;
        var body = new byte[len];
        var off = 0;
        while (off < len)
        {
            var n = await s.ReadAsync(body.AsMemory(off));
            if (n == 0) throw new EndOfStreamException();
            off += n;
        }
        return new RawResponse(status, headers, Encoding.UTF8.GetString(body));
    }

    private static async Task<string?> ReadLineAsync(Stream s)
    {
        var sb = new StringBuilder();
        var one = new byte[1];
        while (true)
        {
            var n = await s.ReadAsync(one);
            if (n == 0) return sb.Length == 0 ? null : sb.ToString();
            if (one[0] == '\n') return sb.ToString().TrimEnd('\r');
            sb.Append((char)one[0]);
        }
    }
}

public static class StreamExt
{
    /// <summary>True when the peer closed (EOF or reset) within <paramref name="ms"/>.</summary>
    public static async Task<bool> ClosedWithinAsync(this Stream s, int ms)
    {
        using var cts = new CancellationTokenSource(ms);
        var buf = new byte[256];
        try
        {
            while (true)
            {
                var n = await s.ReadAsync(buf, cts.Token);
                if (n == 0) return true;
            }
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }
}
