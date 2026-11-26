// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using Kilnwarden.Http;
using Kilnwarden.Routing;
using Kilnwarden.Tls;
using Kilnwarden.WebSockets;

namespace Kilnwarden.Server;

/// <summary>One connection: the TLS handshake (under its deadline), then requests in order (keep-alive, pipelining)
/// until the client or a limit closes it, or a WebSocket takes it over.</summary>
internal sealed class HttpConnection
{
    private readonly Socket? _socket;
    private readonly Stream? _relayed;
    private readonly IPEndPoint _remote;
    private readonly IPEndPoint _local;
    private IPAddress Peer => _remote.Address;
    private readonly ITlsServer? _tls;
    private readonly ServerShared _shared;
    private readonly CancellationTokenSource _cts;
    private volatile int _state; // 0 idle (between requests), 1 busy, 2 WebSocket
    private volatile bool _closing;
    private WebSocketSession? _ws;

    public HttpConnection(long id, Socket socket, IPEndPoint remote, IPEndPoint local, ITlsServer? tls, ServerShared shared,
        CancellationToken serverStop) : this(id, remote, local, tls, shared, serverStop)
    {
        _socket = socket;
    }

    public HttpConnection(long id, Stream transport, IPEndPoint remote, IPEndPoint local, ITlsServer tls, ServerShared shared,
        CancellationToken serverStop) : this(id, remote, local, tls, shared, serverStop)
    {
        _relayed = transport;
    }

    private HttpConnection(long id, IPEndPoint remote, IPEndPoint local, ITlsServer? tls, ServerShared shared, CancellationToken serverStop)
    {
        Id = id;
        _remote = remote;
        _local = local;
        _tls = tls;
        _shared = shared;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(serverStop);
    }

    public long Id { get; }
    public Task? Task { get; set; }

    public async Task RunAsync()
    {
        var ct = _cts.Token;
        Stream? stream = null;
        RequestReader? reader = null;
        try
        {
            if (_socket is not null)
            {
                _socket.NoDelay = true;
                stream = new NetworkStream(_socket, ownsSocket: true);
            }
            else stream = _relayed!;
            TlsConnectionInfo? tlsInfo = null;
            if (_tls is not null)
            {
                var policy0 = _shared.Policy();
                using var hs = CancellationTokenSource.CreateLinkedTokenSource(ct);
                hs.CancelAfter(policy0.Limits.HandshakeTimeoutMs);
                try
                {
                    var t = await _tls.AcceptAsync(stream, hs.Token).ConfigureAwait(false);
                    stream = t;
                    tlsInfo = t.Info;
                    Interlocked.Increment(ref _shared.Counters.HandshakesOk);
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    Interlocked.Increment(ref _shared.Counters.HandshakesFailed);
                    if (hs.IsCancellationRequested && !ct.IsCancellationRequested) Interlocked.Increment(ref _shared.Counters.Timeouts);
                    return;
                }
                // TLS-ALPN-01 (ACME, step 2) only needs the handshake
                if (tlsInfo.Alpn == "acme-tls/1") return;
            }
            var conn = new ConnectionInfo { Id = Id, PeerAddress = Peer, PeerPort = _remote.Port, LocalEndPoint = _local, Tls = tlsInfo, Relayed = _relayed is not null };
            reader = new RequestReader(stream);
            var requests = 0;
            while (!ct.IsCancellationRequested && !_closing)
            {
                var policy = _shared.Policy();
                var limits = policy.Limits;
                _state = 0;
                if (_closing) break;
                HttpRequest? req;
                try
                {
                    req = await reader.ReadHeadAsync(policy.HttpLimits, limits.KeepAliveTimeoutMs, limits.HeaderTimeoutMs, ct).ConfigureAwait(false);
                }
                catch (HttpError e)
                {
                    await WriteErrorAsync(stream, e.Status, e.Message, ct).ConfigureAwait(false);
                    break;
                }
                catch (TimeoutException e)
                {
                    if (e.Message != "idle") Interlocked.Increment(ref _shared.Counters.Timeouts);
                    break;
                }
                if (req is null) break;
                _state = 1;
                requests++;
                Interlocked.Increment(ref _shared.Counters.Requests);
                (IPAddress client, string scheme, string host) resolved;
                try { resolved = policy.Resolve(req, Peer, tlsInfo is not null, allowProxyHeaders: _relayed is null); }
                catch (HttpError e)
                {
                    await WriteErrorAsync(stream, e.Status, e.Message, ct).ConfigureAwait(false);
                    break;
                }
                var (client, scheme, host) = resolved;
                var ctx = new RequestContext
                {
                    Request = req, Connection = conn, Services = _shared.Services, Aborted = ct,
                    ClientAddress = client, Scheme = scheme, Host = host,
                };
                var (res, bodyRead) = await HandleAsync(ctx, stream, reader, policy, ct).ConfigureAwait(false);
                if (res.WebSocket is { } handler)
                {
                    await RunWebSocketAsync(ctx, handler, stream, reader, policy, ct).ConfigureAwait(false);
                    break;
                }
                foreach (var (k, v) in ctx.ResponseHeaders) res.Headers.Add(k, v);
                SecurityHeaders.Apply(res, ctx.IsHttps, ctx.Host, policy.Hsts);
                var keep = bodyRead && req.KeepAlive && !res.Close && !_closing && requests < limits.MaxRequestsPerConnection;
                using (var w = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    w.CancelAfter(limits.WriteTimeoutMs);
                    await ResponseWriter.WriteAsync(stream, res, keep, req.Method == "HEAD", w.Token).ConfigureAwait(false);
                }
                if (!keep) break;
            }
        }
        catch (Exception e) when (e is OperationCanceledException or IOException or SocketException or ObjectDisposedException or EndOfStreamException or WebSocketException or TimeoutException)
        {
            // the peer left, a deadline passed, or Stop() closed us
        }
        catch (Exception e)
        {
            Interlocked.Increment(ref _shared.Counters.Errors);
            _shared.Log.Error($"connection {Id} from {Peer}: unexpected failure", e);
        }
        finally
        {
            reader?.Dispose();
            try
            {
                if (stream is not null) await stream.DisposeAsync().ConfigureAwait(false);
                else _socket?.Dispose();
            }
            catch (Exception)
            {
                // closing anyway
            }
            _cts.Dispose();
        }
    }

    /// <summary>Everything between the parsed head and the response: per-IP rate, Host check, path, route match, the
    /// body (up to the route's cap, under its deadline), then the pipeline. bodyRead false: the body was not consumed, so
    /// the connection cannot be reused.</summary>
    private async Task<(HttpResponse, bool)> HandleAsync(RequestContext ctx, Stream stream, RequestReader reader, ServerPolicy policy, CancellationToken ct)
    {
        var req = ctx.Request;
        var limits = policy.Limits;
        if (!_shared.Limiter.TryRequest(ctx.ClientAddress, limits))
        {
            Interlocked.Increment(ref _shared.Counters.RateLimited);
            var r = HttpResponse.Error(429, "rate_limited", "too many requests");
            r.Headers.Set("Retry-After", "1");
            return (r, req.ContentLength == 0);
        }
        if (!policy.HostAllowed(ctx.Host))
            return (HttpResponse.Error(421, "unknown_host", "this panel does not answer to that host name (settings: allowedHosts)"), req.ContentLength == 0);
        if (!PathSegments.TryParse(req.Path, out var segments))
            return (HttpResponse.Error(400, "bad_path", "bad request path"), req.ContentLength == 0);
        var lookup = _shared.Pipeline.Match(ctx, segments);
        if (req.ContentLength > 0 && lookup.Match is null)
        {
            // no route takes a body here (review I4): never read or allocate one for it
            var r = HttpResponse.Error(404, "not_found", "nothing here takes a request body");
            r.Close = true;
            return (r, false);
        }
        if (req.ContentLength > 0)
        {
            var cap = Math.Min(lookup.Match?.Route.MaxBodyBytes ?? limits.MaxBodyBytes, Math.Min(limits.MaxBodyBytes, Array.MaxLength));
            if (req.ContentLength > cap)
            {
                var r = HttpResponse.Error(413, "too_large", $"the body is over {cap} bytes");
                r.Close = true;
                return (r, false);
            }
            if (HttpParser.ExpectsContinue(req) && !reader.HasBuffered)
            {
                using var interim = CancellationTokenSource.CreateLinkedTokenSource(ct);
                interim.CancelAfter(limits.WriteTimeoutMs);
                await stream.WriteAsync("HTTP/1.1 100 Continue\r\n\r\n"u8.ToArray(), interim.Token).ConfigureAwait(false);
                await stream.FlushAsync(interim.Token).ConfigureAwait(false);
            }
            req.Body = await reader.ReadBodyAsync(req.ContentLength, limits.BodyTimeoutMs + (int)Math.Min(600_000, req.ContentLength / 64), ct).ConfigureAwait(false);
        }
        return (await _shared.Pipeline.RunAsync(ctx).ConfigureAwait(false), true);
    }

    private async Task RunWebSocketAsync(RequestContext ctx, WebSocketHandler handler, Stream stream, RequestReader reader, ServerPolicy policy, CancellationToken ct)
    {
        var check = WebSocketUpgrade.Check(ctx.Request, ctx.Origin);
        if (!check.Ok)
        {
            var r = HttpResponse.Error(check.Status, "bad_upgrade", check.Message);
            if (check.Status == 426) r.Headers.Set("Sec-WebSocket-Version", "13");
            SecurityHeaders.Apply(r, ctx.IsHttps, ctx.Host, policy.Hsts);
            await ResponseWriter.WriteAsync(stream, r, keepAlive: false, headRequest: false, ct).ConfigureAwait(false);
            return;
        }
        var head = new StringBuilder("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: ")
            .Append(check.Accept).Append("\r\n");
        foreach (var (k, v) in ctx.ResponseHeaders)
            if (k.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase) && !v.AsSpan().ContainsAny('\r', '\n'))
                head.Append(k).Append(": ").Append(v).Append("\r\n");
        head.Append("\r\n");
        using (var upgradeWrite = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            upgradeWrite.CancelAfter(policy.Limits.WriteTimeoutMs);
            await stream.WriteAsync(Encoding.Latin1.GetBytes(head.ToString()), upgradeWrite.Token).ConfigureAwait(false);
            await stream.FlushAsync(upgradeWrite.Token).ConfigureAwait(false);
        }
        var leftover = reader.TakeLeftover();
        var wsStream = leftover.IsEmpty ? stream : new PrefixStream(leftover, stream);
        using var socket = WebSocket.CreateFromStream(wsStream, new WebSocketCreationOptions
        {
            IsServer = true,
            KeepAliveInterval = TimeSpan.FromSeconds(20),
            KeepAliveTimeout = TimeSpan.FromSeconds(20),
        });
        var ws = new WebSocketSession { Request = ctx, Socket = socket, Stopping = ct, MaxMessageBytes = policy.Limits.MaxWebSocketMessageBytes };
        _ws = ws;
        _state = 2;
        Interlocked.Increment(ref _shared.Counters.WebSockets);
        try
        {
            await handler(ws).ConfigureAwait(false);
        }
        catch (Exception e) when (e is OperationCanceledException or WebSocketException or IOException or ObjectDisposedException)
        {
            // closed under it
        }
        catch (Exception e)
        {
            _shared.Log.Error($"WebSocket {ctx.Request.Path}: handler failed", e);
            await ws.CloseAsync(WebSocketCloseStatus.InternalServerError, "server error").ConfigureAwait(false);
        }
        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "").ConfigureAwait(false);
        // wait briefly for the peer's close frame so the TCP close is clean
        if (socket.State == WebSocketState.CloseSent)
        {
            try
            {
                using var t = new CancellationTokenSource(1000);
                var buf = new byte[256];
                while (socket.State == WebSocketState.CloseSent)
                    await socket.ReceiveAsync(buf.AsMemory(), t.Token).ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException or WebSocketException or IOException or ObjectDisposedException)
            {
            }
        }
    }

    private async Task WriteErrorAsync(Stream s, int status, string message, CancellationToken ct)
    {
        try
        {
            using var w = CancellationTokenSource.CreateLinkedTokenSource(ct);
            w.CancelAfter(2000);
            await ResponseWriter.WriteErrorAsync(s, status, message, keepAlive: false, w.Token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException or SocketException)
        {
        }
    }

    /// <summary>Stop: an idle connection closes now, a busy one after its response, a WebSocket gets close 1001.</summary>
    public void BeginClose()
    {
        _closing = true;
        switch (_state)
        {
            case 0:
                try
                {
                    _cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
                break;
            case 2 when _ws is { } ws:
                _ = System.Threading.Tasks.Task.Run(() => ws.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "server stopping"));
                break;
        }
    }

    public void Abort()
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        try
        {
            if (_socket is not null)
            {
                _socket.LingerState = new LingerOption(true, 0);
                _socket.Close();
            }
            else _relayed?.Dispose();
        }
        catch (Exception)
        {
            // closed already
        }
    }
}
