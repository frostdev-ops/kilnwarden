// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Kilnwarden.Hosting;
using Kilnwarden.Http;

namespace Kilnwarden.Tls.Acme;

/// <summary>The http-01 answerer (RFC 8555 §8.3): plain HTTP on port 80 (acme.json http01Port), open only while an
/// order waits for validation. It answers exactly GET/HEAD /.well-known/acme-challenge/&lt;token&gt; for a published token
/// whose name equals the request's Host, and 404 to everything else; one request per connection, small bounded reads,
/// a short deadline and a connection cap. It never reaches the panel's routes.</summary>
public sealed class Http01Responder : IHttp01Publisher, IDisposable
{
    public const string Prefix = "/.well-known/acme-challenge/";
    public const int MaxConnections = 32;
    public const int MaxHeadBytes = 8192;
    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<string, (string Name, string KeyAuth)> _tokens = new(StringComparer.Ordinal);
    private readonly IPEndPoint _endpoint;
    private readonly IWebAdminLog _log;
    private readonly object _gate = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _accept;
    private int _open;

    public Http01Responder(IPEndPoint endpoint, IWebAdminLog log)
    {
        _endpoint = endpoint;
        _log = log;
    }

    /// <summary>The bound port while listening (0 otherwise).</summary>
    public int Port { get; private set; }

    public long Answered;

    public void Publish(string name, string token, string keyAuthorization)
    {
        _tokens[token] = (name.ToLowerInvariant(), keyAuthorization);
        lock (_gate)
        {
            if (_listener is not null) return;
            var l = new TcpListener(_endpoint);
            l.Start(64);
            _listener = l;
            Port = ((IPEndPoint)l.LocalEndpoint).Port;
            _cts = new CancellationTokenSource();
            _accept = AcceptLoop(l, _cts.Token);
            _log.Info($"ACME: answering http-01 on {_endpoint.Address}:{Port} while the order is validated");
        }
    }

    public void Clear()
    {
        _tokens.Clear();
        Stop();
    }

    private void Stop()
    {
        TcpListener? l;
        CancellationTokenSource? cts;
        lock (_gate)
        {
            l = _listener;
            cts = _cts;
            _listener = null;
            _cts = null;
            Port = 0;
        }
        if (l is null) return;
        cts!.Cancel();
        l.Stop();
        cts.Dispose();
    }

    private async Task AcceptLoop(TcpListener l, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            Socket s;
            try
            {
                s = await l.AcceptSocketAsync(ct).ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }
            if (Interlocked.Increment(ref _open) > MaxConnections)
            {
                Interlocked.Decrement(ref _open);
                s.Dispose();
                continue;
            }
            _ = Task.Run(async () =>
            {
                try
                {
                    await ServeAsync(s, ct).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref _open);
                    s.Dispose();
                }
            }, CancellationToken.None);
        }
    }

    private async Task ServeAsync(Socket s, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Deadline);
        try
        {
            await using var stream = new NetworkStream(s, ownsSocket: false);
            var buf = new byte[MaxHeadBytes];
            var len = 0;
            HttpRequest? req = null;
            while (req is null)
            {
                if (len == buf.Length) return;
                var n = await stream.ReadAsync(buf.AsMemory(len), cts.Token).ConfigureAwait(false);
                if (n == 0) return;
                len += n;
                try
                {
                    if (HttpParser.TryParseHead(buf.AsSpan(0, len), new HttpLimits(MaxHeadBytes, 32, 512), out req, out _) == ParseStatus.Incomplete) req = null;
                }
                catch (HttpError)
                {
                    await WriteAsync(stream, 400, "", cts.Token).ConfigureAwait(false);
                    return;
                }
            }
            var (status, body) = Answer(req);
            if (req.Method == "HEAD") body = "";
            await WriteAsync(stream, status, body, cts.Token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
        {
        }
    }

    /// <summary>The status and body for a request (tests call it directly).</summary>
    public (int, string) Answer(HttpRequest req)
    {
        if (req.Method is not ("GET" or "HEAD") || !req.Path.StartsWith(Prefix, StringComparison.Ordinal)) return (404, "");
        var token = req.Path[Prefix.Length..];
        if (!_tokens.TryGetValue(token, out var t)) return (404, "");
        var host = req.Host;
        var colon = host.LastIndexOf(':');
        if (colon > 0 && !host.EndsWith(']')) host = host[..colon];
        if (!string.Equals(host.TrimEnd('.'), t.Name, StringComparison.OrdinalIgnoreCase)) return (404, "");
        Interlocked.Increment(ref Answered);
        return (200, t.KeyAuth);
    }

    private static async Task WriteAsync(Stream s, int status, string body, CancellationToken ct)
    {
        var b = Encoding.ASCII.GetBytes(body);
        var head = $"HTTP/1.1 {status} {HttpResponse.ReasonPhrase(status)}\r\nContent-Type: text/plain\r\nContent-Length: {b.Length}\r\n" +
                   "Cache-Control: no-store\r\nConnection: close\r\n\r\n";
        await s.WriteAsync(Encoding.ASCII.GetBytes(head), ct).ConfigureAwait(false);
        await s.WriteAsync(b, ct).ConfigureAwait(false);
    }

    public void Dispose() => Clear();
}
