// SPDX-License-Identifier: MIT
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;

namespace Kilnwarden.Setup;

/// <summary>The setup wizard's last check (0.1.2): this server opens its own public address the way a browser would
/// (the name in DNS, port 443 through the relay for hosted access, the certificate checked against the machine's trust
/// store) and makes sure the panel that answers is this one (the same certificate it serves) and answers HTTP. Read-only:
/// one GET of /api/core/health.</summary>
internal sealed class SelfProbe
{
    /// <summary>Opens a TCP stream to host:port (a test points it at its relay's browser side).</summary>
    public Func<string, int, CancellationToken, Task<Stream>> Connect { get; init; } = DefaultConnect;

    /// <summary>The trust check of the certificate (tests trust their CA; the default is the machine's store).</summary>
    public RemoteCertificateValidationCallback? Validate { get; init; }

    public sealed record Result(bool Ok, string Kind, string? Detail, DateTimeOffset At);

    private static async Task<Stream> DefaultConnect(string host, int port, CancellationToken ct)
    {
        var tcp = new TcpClient();
        try
        {
            await tcp.ConnectAsync(host, port, ct).ConfigureAwait(false);
            return tcp.GetStream();
        }
        catch
        {
            tcp.Dispose();
            throw;
        }
    }

    /// <param name="ours">The SHA-256 fingerprints ("AB:CD:...") of the certificates this panel serves.</param>
    /// <param name="trusted">The certificate must check out (hosted access, an own address once it has its certificate);
    /// false: any certificate of ours will do (an own address before Let's Encrypt).</param>
    public async Task<Result> RunAsync(string host, int port, IReadOnlyCollection<string> ours, bool trusted, TimeProvider time, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(12));
        Stream? stream = null;
        try
        {
            try
            {
                stream = await Connect(host, port, deadline.Token).ConfigureAwait(false);
            }
            catch (SocketException e) when (e.SocketErrorCode is SocketError.HostNotFound or SocketError.TryAgain or SocketError.NoData)
            {
                return new Result(false, "dns", $"{host} does not resolve from this server ({e.SocketErrorCode})", time.GetUtcNow());
            }
            catch (Exception e) when (e is SocketException or IOException)
            {
                return new Result(false, "connect", $"cannot connect to {host}:{port} ({(e as SocketException)?.SocketErrorCode.ToString() ?? e.Message})", time.GetUtcNow());
            }
            string? seen = null;
            var errors = SslPolicyErrors.None;
            await using var ssl = new SslStream(stream, leaveInnerStreamOpen: false, (sender, cert, chain, e) =>
            {
                if (cert is not null) seen = Convert.ToHexString(SHA256.HashData(cert.GetRawCertData()));
                errors = Validate is { } v ? (v(sender, cert, chain, e) ? SslPolicyErrors.None : e == SslPolicyErrors.None ? SslPolicyErrors.RemoteCertificateChainErrors : e) : e;
                return true; // decided below, with a reason in plain words
            });
            stream = null;
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = host, ApplicationProtocols = [SslApplicationProtocol.Http11],
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            }, deadline.Token).ConfigureAwait(false);
            var mine = ours.Select(o => o.Replace(":", "", StringComparison.Ordinal)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (seen is null || !mine.Contains(seen))
                return new Result(false, "other-panel", $"another server answers at {host}:{port} (certificate SHA-256 {seen ?? "none"})", time.GetUtcNow());
            if (trusted && errors != SslPolicyErrors.None)
                return new Result(false, "untrusted", $"the certificate at {host} is not trusted yet ({errors})", time.GetUtcNow());
            var req = Encoding.ASCII.GetBytes($"GET /api/core/health HTTP/1.1\r\nHost: {(port == 443 ? host : $"{host}:{port}")}\r\nUser-Agent: {Product.Name}-selfcheck\r\nConnection: close\r\n\r\n");
            await ssl.WriteAsync(req, deadline.Token).ConfigureAwait(false);
            await ssl.FlushAsync(deadline.Token).ConfigureAwait(false);
            var buf = new byte[512];
            var n = 0;
            while (n < 12)
            {
                var r = await ssl.ReadAsync(buf.AsMemory(n), deadline.Token).ConfigureAwait(false);
                if (r == 0) break;
                n += r;
            }
            var head = Encoding.ASCII.GetString(buf, 0, n);
            return head.StartsWith("HTTP/1.1 200", StringComparison.Ordinal)
                ? new Result(true, "ok", null, time.GetUtcNow())
                : new Result(false, "http", $"the panel at {host} answered: {head.Split('\r')[0]}", time.GetUtcNow());
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new Result(false, "timeout", $"no answer from {host}:{port} within 12 s", time.GetUtcNow());
        }
        catch (Exception e) when (e is IOException or AuthenticationException or SocketException or InvalidOperationException)
        {
            return new Result(false, "connect", $"{host}:{port}: {e.GetType().Name}: {e.Message}", time.GetUtcNow());
        }
        finally
        {
            if (stream is not null) await stream.DisposeAsync().ConfigureAwait(false);
        }
    }
}
