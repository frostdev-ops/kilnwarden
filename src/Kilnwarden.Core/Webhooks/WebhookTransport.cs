// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;

namespace Kilnwarden.Webhooks;

/// <summary>A webhook POST: where, and the JSON body.</summary>
public sealed record WebhookRequest(Uri Url, byte[] Body);

/// <summary>A send the guard refused (not a public address).</summary>
public sealed class WebhookRefusedException(string message) : Exception(message);

/// <summary>Sends one webhook; returns the HTTP status (throws on a network failure or a refusal). Tests use a fake.</summary>
public interface IWebhookTransport
{
    Task<int> SendAsync(WebhookRequest request, CancellationToken ct);
}

/// <summary>The real sender: no proxy, no cookies, no redirects, and a connect callback that resolves the host itself
/// and connects only when every address is allowed (&lt;see cref="WebhookGuard.CheckConnect"/&gt;: public, or private LAN
/// with the owner's opt-in), to the address it checked, so a DNS answer cannot change between the check and the
/// connection. No connection is reused (every send passes the check again, so turning the opt-in off holds at once).
/// 5 s to connect, 10 s in all, 4 KiB of the answer read and dropped.</summary>
public sealed class HttpWebhookTransport : IWebhookTransport, IDisposable
{
    public const int MaxResponseBytes = 4096;

    private readonly HttpClient _http;

    /// <param name="resolve">Name lookup (tests: a fake).</param>
    /// <param name="allowPrivate">The owner's private-target opt-in, read at every connection.</param>
    /// <param name="connect">Opens the connection to the checked address (tests: a fake; default a TCP socket).</param>
    public HttpWebhookTransport(Func<string, CancellationToken, ValueTask<IPAddress[]>>? resolve = null, Func<bool>? allowPrivate = null,
        Func<IPAddress, int, CancellationToken, ValueTask<Stream>>? connect = null)
    {
        resolve ??= (host, ct) => new ValueTask<IPAddress[]>(Dns.GetHostAddressesAsync(host, ct));
        allowPrivate ??= () => false;
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            PooledConnectionLifetime = TimeSpan.Zero,
            MaxResponseHeadersLength = 16,
            ConnectCallback = async (ctx, ct) =>
            {
                var host = ctx.DnsEndPoint.Host;
                var addresses = IPAddress.TryParse(host.Trim('[', ']'), out var literal) ? [literal] : await resolve(host, ct).ConfigureAwait(false);
                var plain = ctx.InitialRequestMessage.RequestUri?.Scheme != Uri.UriSchemeHttps;
                var target = WebhookGuard.CheckConnect(addresses, allowPrivate(), plain);
                if (connect is not null) return await connect(target, ctx.DnsEndPoint.Port, ct).ConfigureAwait(false);
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(target, ctx.DnsEndPoint.Port, ct).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Kilnwarden", Product.Version));
    }

    public async Task<int> SendAsync(WebhookRequest request, CancellationToken ct)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Post, request.Url) { Content = new ByteArrayContent(request.Body) };
        msg.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        using var res = await _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        await using var body = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var buf = new byte[1024];
        var read = 0;
        while (read < MaxResponseBytes)
        {
            var n = await body.ReadAsync(buf, ct).ConfigureAwait(false);
            if (n == 0) break;
            read += n;
        }
        return (int)res.StatusCode;
    }

    public void Dispose() => _http.Dispose();
}
