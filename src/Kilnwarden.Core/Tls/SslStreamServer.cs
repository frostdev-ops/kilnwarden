// SPDX-License-Identifier: MIT
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Kilnwarden.Tls;

/// <summary>The fallback engine: .NET's SslStream (SChannel on Windows, GnuTLS under Wine). Under Wine (step 0) it only
/// works with RSA keys over TLS 1.2, sends the leaf without intermediates and ignores protocol limits, so: RSA only, the
/// certificate imported through a PKCS#12 built in memory (the Wine-safe path), and a post-handshake check that
/// refuses anything below TLS 1.2.</summary>
public sealed class SslStreamServer : ITlsServer
{
    private readonly ICertificateSource _source;
    private readonly List<SslApplicationProtocol> _alpn;
    private readonly Hosting.IWebAdminLog _log;
    private readonly object _gate = new();
    private (SslStreamCertificateContext Context, X509Certificate2 Leaf, CertificateInfo Info)? _current;

    public SslStreamServer(ICertificateSource source, IReadOnlyList<string> alpn, Hosting.IWebAdminLog log)
    {
        _source = source;
        _log = log;
        _alpn = [.. alpn.Select(a => a == "http/1.1" ? SslApplicationProtocol.Http11 : new SslApplicationProtocol(a))];
        _current = Build(source.Current);
        source.Changed += OnChanged;
    }

    public string Engine => "sslstream";
    public CertificateInfo Certificate => _current!.Value.Info;

    private static (SslStreamCertificateContext, X509Certificate2, CertificateInfo) Build(CertificateMaterial m)
    {
        if (Pem.KeyKind(m.KeyPem) != "rsa")
            throw new NotSupportedException("the SslStream fallback needs an RSA key (Wine's SChannel has no ECDSA); install kwtls or use an RSA certificate");
        var pfx = Pfx.FromPem(Encoding.ASCII.GetString(m.ChainPem), Encoding.ASCII.GetString(m.KeyPem), withIntermediates: false);
        // Windows' SChannel cannot serve with an ephemeral (in-memory) key: the handshake dies with an EOF on native
        // Windows (found by a smoke test on Windows). The default key set works there and under Wine (step 0's matrix); its
        // temporary key container is removed when the certificate is released.
        var leaf = X509CertificateLoader.LoadPkcs12(pfx, null, OperatingSystem.IsWindows() ? X509KeyStorageFlags.DefaultKeySet : X509KeyStorageFlags.EphemeralKeySet);
        var extra = new X509Certificate2Collection();
        foreach (var b in Pem.Decode(m.ChainPem).Where(b => b.Label == "CERTIFICATE").Skip(1))
            extra.Add(X509CertificateLoader.LoadCertificate(b.Der));
        return (SslStreamCertificateContext.Create(leaf, extra, offline: true), leaf, m.Info);
    }

    private void OnChanged(CertificateMaterial m)
    {
        try
        {
            var next = Build(m);
            X509Certificate2? old;
            lock (_gate)
            {
                old = _current?.Leaf;
                _current = next;
            }
            _log.Info($"TLS: now serving {m.Info.Source} certificate {m.Info.Sha256}");
            _ = old; // connections may still use it; the GC releases it
        }
        catch (Exception e)
        {
            _log.Error($"TLS: the new {m.Info.Source} certificate was refused, keeping the previous one: {e.Message}");
        }
    }

    public async ValueTask<TlsStream> AcceptAsync(Stream transport, CancellationToken ct)
    {
        var ctx = _current!.Value.Context;
        var ssl = new SslStream(transport, leaveInnerStreamOpen: false);
        try
        {
            await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificateContext = ctx,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                ApplicationProtocols = _alpn,
                ClientCertificateRequired = false,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            }, ct).ConfigureAwait(false);
            // Wine's SChannel ignores EnabledSslProtocols: refuse TLS 1.0/1.1 after the fact
            if (ssl.SslProtocol is not (SslProtocols.Tls12 or SslProtocols.Tls13))
                throw new AuthenticationException($"refused {ssl.SslProtocol}: TLS 1.2 is the minimum");
            return new SslTlsStream(ssl);
        }
        catch
        {
            await ssl.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public void Dispose() => _source.Changed -= OnChanged;
}

/// <summary>An SslStream as a <see cref="TlsStream"/>.</summary>
internal sealed class SslTlsStream(SslStream ssl) : TlsStream
{
    public override TlsConnectionInfo Info { get; } = new(
        ssl.SslProtocol switch { SslProtocols.Tls13 => "TLSv1.3", SslProtocols.Tls12 => "TLSv1.2", var p => p.ToString() },
        ssl.NegotiatedCipherSuite == TlsCipherSuite.TLS_NULL_WITH_NULL_NULL ? "" : ssl.NegotiatedCipherSuite.ToString(),
        ssl.NegotiatedApplicationProtocol.Protocol.IsEmpty ? null : ssl.NegotiatedApplicationProtocol.ToString(),
        string.IsNullOrEmpty(ssl.TargetHostName) ? null : ssl.TargetHostName,
        "sslstream");

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => ssl.Flush();
    public override Task FlushAsync(CancellationToken ct) => ssl.FlushAsync(ct);
    public override int Read(byte[] b, int o, int c) => ssl.Read(b, o, c);
    public override ValueTask<int> ReadAsync(Memory<byte> b, CancellationToken ct = default) => ssl.ReadAsync(b, ct);
    public override Task<int> ReadAsync(byte[] b, int o, int c, CancellationToken ct) => ssl.ReadAsync(b, o, c, ct);
    public override void Write(byte[] b, int o, int c) => ssl.Write(b, o, c);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> b, CancellationToken ct = default) => ssl.WriteAsync(b, ct);
    public override Task WriteAsync(byte[] b, int o, int c, CancellationToken ct) => ssl.WriteAsync(b, o, c, ct);
    public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
    public override void SetLength(long v) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) ssl.Dispose();
        base.Dispose(disposing);
    }
}
