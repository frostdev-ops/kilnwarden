// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Kilnwarden.Server;
using Kilnwarden.Tls;

namespace Kilnwarden.Hosting;

/// <summary>While Kiln Web Admin, the old name, keeps Kilnwarden off (0.1.2): the panel's address answers every request
/// with one page that says exactly what to delete, instead of not answering at all. On this machine only (127.0.0.1),
/// HTTPS with a throwaway self-signed certificate made in memory (or plain HTTP), nothing read and nothing written;
/// when the old copy itself holds the port, there is no page (that copy answers).</summary>
public sealed class LegacyNotice : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly ITlsServer _tls;
    private readonly byte[] _html, _json;

    public int Port { get; }

    private LegacyNotice(TcpListener listener, ITlsServer tls, string message)
    {
        _listener = listener;
        _tls = tls;
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var text = WebUtility.HtmlEncode(message);
        _html = Encoding.UTF8.GetBytes("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\">" +
                                       $"<title>{Product.Name}</title><style>body{{font:16px/1.5 system-ui,sans-serif;max-width:40em;margin:3em auto;padding:0 1em}}</style>" +
                                       $"</head><body><h1>{Product.Name} is off</h1><p>{text}</p></body></html>");
        _json = Encoding.UTF8.GetBytes(new JsonObject { ["error"] = new JsonObject { ["code"] = "old_copy_installed", ["message"] = message } }.ToJsonString());
        _ = Task.Run(AcceptLoop);
    }

    /// <summary>The panel's port: settings.json's (this mod's data folder, or the old one's), else the query port + 100.</summary>
    public static int PanelPort(string dataDirectory, int? queryPort)
    {
        foreach (var dir in new[] { dataDirectory, Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(dataDirectory)) ?? "", LegacyInstall.OldModId) })
        {
            try
            {
                var f = Path.Combine(dir, "settings.json");
                if (File.Exists(f) && new FileInfo(f).Length < 1 << 20 &&
                    JsonNode.Parse(File.ReadAllText(f), documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true })?["port"] is JsonValue v &&
                    v.TryGetValue<int>(out var p) && p is > 0 and < 65536)
                    return p;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException) { }
        }
        var q = queryPort is > 0 and < 65536 ? queryPort.Value : ListenerManager.DefaultQueryPort;
        return q + ListenerManager.PortOffset <= 65535 ? q + ListenerManager.PortOffset : ListenerManager.DefaultQueryPort + ListenerManager.PortOffset;
    }

    /// <summary>Starts the page on 127.0.0.1 at the panel's port, or returns null (the port is taken: the old copy runs).</summary>
    public static LegacyNotice? TryStart(string dataDirectory, int? queryPort, string message, Action<string> log, int? port = null)
    {
        var p = port ?? PanelPort(dataDirectory, queryPort);
        var listener = new TcpListener(IPAddress.Loopback, p);
        try
        {
            listener.Start();
        }
        catch (SocketException e)
        {
            log($"the panel's address 127.0.0.1:{p} is taken ({e.SocketErrorCode}): the old copy answers there");
            return null;
        }
        var now = DateTimeOffset.UtcNow;
        var (cert, key) = Pfx.SelfSignedRsa($"{Product.Name} notice", ["localhost", "127.0.0.1"], now.AddDays(-1), now.AddDays(7));
        var material = new CertificateMaterial(cert, key, CertificateInspector.Inspect(cert, key, "self-signed"));
        var tls = new SslStreamServer(new FixedCertificateSource(material, "self-signed"), ["http/1.1"], new Quiet());
        log($"https://127.0.0.1:{p}/ says what to delete until then");
        return new LegacyNotice(listener, tls, message);
    }

    private async Task AcceptLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            Socket s;
            try { s = await _listener.AcceptSocketAsync(_cts.Token).ConfigureAwait(false); }
            catch (Exception) when (_cts.IsCancellationRequested) { return; }
            catch (SocketException) { continue; }
            _ = Task.Run(() => Serve(s));
        }
    }

    private async Task Serve(Socket socket)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        Stream stream = new NetworkStream(socket, ownsSocket: true);
        try
        {
            var first = new byte[1];
            if (await stream.ReadAsync(first, cts.Token).ConfigureAwait(false) != 1) return;
            stream = new PrefixStream(first, stream);
            if (first[0] == 0x16) stream = await _tls.AcceptAsync(stream, cts.Token).ConfigureAwait(false);
            var head = new byte[8192];
            var n = 0;
            while (n < head.Length)
            {
                var r = await stream.ReadAsync(head.AsMemory(n), cts.Token).ConfigureAwait(false);
                if (r == 0) break;
                n += r;
                if (head.AsSpan(0, n).IndexOf("\r\n\r\n"u8) >= 0) break;
            }
            var api = Encoding.ASCII.GetString(head, 0, Math.Min(n, 64)).Split(' ').ElementAtOrDefault(1)?.StartsWith("/api", StringComparison.Ordinal) == true;
            var body = api ? _json : _html;
            var res = Encoding.ASCII.GetBytes($"HTTP/1.1 503 Service Unavailable\r\nContent-Type: {(api ? "application/json" : "text/html; charset=utf-8")}\r\n" +
                                              $"Content-Length: {body.Length}\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\n" +
                                              "Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(res, cts.Token).ConfigureAwait(false);
            await stream.WriteAsync(body, cts.Token).ConfigureAwait(false);
            await stream.FlushAsync(cts.Token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or System.Security.Authentication.AuthenticationException or SocketException or ObjectDisposedException) { }
        finally
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _tls.Dispose();
        _cts.Dispose();
    }

    private sealed class Quiet : IWebAdminLog
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? error = null) { }
    }
}
