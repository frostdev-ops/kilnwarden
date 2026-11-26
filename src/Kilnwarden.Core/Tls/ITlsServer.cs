// SPDX-License-Identifier: MIT
namespace Kilnwarden.Tls;

/// <summary>The TLS seam (owner decision 2026-11-23): kwtls (rustls in a native DLL) is the engine, SslStream the
/// fallback. A server takes an accepted socket's stream, runs the handshake under the caller's deadline and returns the
/// plaintext stream. Certificates come from an <see cref="ICertificateSource"/> and can change while it runs (file
/// reload, ACME renewals in step 2): new handshakes use the new one, open connections keep theirs.</summary>
public interface ITlsServer : IDisposable
{
    /// <summary>"kwtls 1.0.0 (rustls 0.23.46)" or "sslstream (schannel)".</summary>
    string Engine { get; }

    /// <summary>The certificate handshakes use now.</summary>
    CertificateInfo Certificate { get; }

    /// <summary>The certificate a connection without a server name gets (an IP address such as 127.0.0.1, or
    /// "localhost"): the Kiln Launcher pins it (launcher-endpoint.json). The same as <see cref="Certificate"/> except in
    /// tls.mode acme, where those connections keep the self-signed one (0.1.2, <see cref="SniSplitServer"/>).</summary>
    CertificateInfo LoopbackCertificate => Certificate;

    /// <summary>Runs the server handshake. Throws (IOException, AuthenticationException, OperationCanceledException) on
    /// failure; the caller closes the socket.</summary>
    ValueTask<TlsStream> AcceptAsync(Stream transport, CancellationToken ct);
}

/// <summary>The plaintext side of a TLS connection.</summary>
public abstract class TlsStream : Stream
{
    public abstract TlsConnectionInfo Info { get; }
}

/// <param name="Protocol">"TLSv1.3", "TLSv1.2".</param>
/// <param name="CipherSuite">"TLS13_AES_256_GCM_SHA384" ("" when the engine cannot tell: SChannel under Wine).</param>
/// <param name="Alpn">The negotiated protocol ("http/1.1", "acme-tls/1") or null.</param>
/// <param name="ServerName">SNI from the client, or null.</param>
public sealed record TlsConnectionInfo(string Protocol, string CipherSuite, string? Alpn, string? ServerName, string Engine);

/// <summary>A certificate chain and its key as PEM (the form both engines and every mode share). The key bytes are
/// secret: never logged, zeroed by <see cref="Dispose"/>.</summary>
public sealed class CertificateMaterial(byte[] chainPem, byte[] keyPem, CertificateInfo info) : IDisposable
{
    /// <summary>Leaf first, then intermediates (fullchain.pem).</summary>
    public byte[] ChainPem { get; } = chainPem;

    public byte[] KeyPem { get; } = keyPem;
    public CertificateInfo Info { get; } = info;

    public void Dispose() => System.Security.Cryptography.CryptographicOperations.ZeroMemory(KeyPem);
}

/// <param name="Sha256">The leaf's SHA-256 fingerprint as upper-case hex pairs "AB:CD:..." (what browsers show).</param>
/// <param name="Source">"self-signed", "file", "acme".</param>
/// <param name="KeyType">"ecdsa-p256", "rsa-2048"...</param>
public sealed record CertificateInfo(string Sha256, string Subject, IReadOnlyList<string> Names, DateTimeOffset NotAfter, string KeyType,
    string Source, int ChainLength);

/// <summary>Where a listener's certificate comes from: self-signed (made once, kept on disk), file (PEM, reloaded on
/// change), and in step 2 ACME. <see cref="Changed"/> fires on any thread with the new material.</summary>
public interface ICertificateSource : IDisposable
{
    string Mode { get; }
    CertificateMaterial Current { get; }
    event Action<CertificateMaterial>? Changed;
}
