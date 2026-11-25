// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Kilnwarden.Server;

namespace Kilnwarden.Tls;

/// <summary>ACME TLS-ALPN-01 challenges in progress (RFC 8737): per name, a self-signed certificate carrying the
/// critical id-pe-acmeIdentifier extension (the SHA-256 of the key authorization). Published by the ACME manager while it
/// validates an order, served by <see cref="AlpnChallengeServer"/> to a client that offers only "acme-tls/1" for that
/// name, removed right after.</summary>
public sealed class AlpnChallenges
{
    public const string Protocol = "acme-tls/1";
    public const string AcmeIdentifierOid = "1.3.6.1.5.5.7.1.31";

    private readonly ConcurrentDictionary<string, CertificateMaterial> _byName = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byName.Count;

    public CertificateMaterial? Find(string? name) => name is null ? null : _byName.GetValueOrDefault(name);

    /// <summary>Makes and publishes the challenge certificate of <paramref name="name"/>.</summary>
    public void Publish(string name, string keyAuthorization)
    {
        var m = Create(name, keyAuthorization);
        if (_byName.TryGetValue(name, out var old)) old.Dispose();
        _byName[name] = m;
    }

    public void Clear()
    {
        foreach (var k in _byName.Keys.ToList())
            if (_byName.TryRemove(k, out var m)) m.Dispose();
    }

    /// <summary>The RFC 8737 §3 certificate: RSA (both engines serve it), the one name as its only SAN, the acmeIdentifier
    /// extension marked critical; made the Wine-safe way (no CopyWithPrivateKey).</summary>
    public static CertificateMaterial Create(string name, string keyAuthorization)
    {
        var digest = SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(keyAuthorization));
        var w = new AsnWriter(AsnEncodingRules.DER);
        w.WriteOctetString(digest);
        var ext = new X509Extension(new Oid(AcmeIdentifierOid), w.Encode(), critical: true);
        var now = DateTimeOffset.UtcNow;
        var (cert, key) = Pfx.SelfSignedRsa("ACME challenge", [name], now.AddDays(-1), now.AddDays(7), extra: [ext]);
        return new CertificateMaterial(cert, key, CertificateInspector.Inspect(cert, key, "acme-challenge"));
    }
}

/// <summary>The listener's TLS server in tls.mode acme: while a TLS-ALPN-01 challenge is published it reads each
/// connection's ClientHello first; a hello that offers "acme-tls/1" for a name with a challenge gets the challenge
/// certificate (and only the handshake), everything else goes to the panel's own engine with the bytes replayed. With
/// no challenge published, connections go straight through.</summary>
public sealed class AlpnChallengeServer(ITlsServer inner, AlpnChallenges challenges, Func<CertificateMaterial, ITlsServer> engine) : ITlsServer
{
    private readonly ConcurrentDictionary<CertificateMaterial, ITlsServer> _servers = new();

    public string Engine => inner.Engine;
    public CertificateInfo Certificate => inner.Certificate;

    public async ValueTask<TlsStream> AcceptAsync(Stream transport, CancellationToken ct)
    {
        if (challenges.Count == 0) return await inner.AcceptAsync(transport, ct).ConfigureAwait(false);
        var (read, hello) = await ClientHello.PeekAsync(transport, ct).ConfigureAwait(false);
        var replay = new PrefixStream(read, transport);
        if (hello is { } h && h.Alpn.Contains(AlpnChallenges.Protocol) && challenges.Find(h.ServerName) is { } m)
        {
            foreach (var (k, s) in _servers)
                if (!ReferenceEquals(k, m) && _servers.TryRemove(k, out var gone)) gone.Dispose();
            var server = _servers.GetOrAdd(m, engine);
            return await server.AcceptAsync(replay, ct).ConfigureAwait(false);
        }
        return await inner.AcceptAsync(replay, ct).ConfigureAwait(false);
    }

    public void Dispose()
    {
        foreach (var s in _servers.Values) s.Dispose();
        _servers.Clear();
        inner.Dispose();
    }
}

/// <summary>A certificate source that never changes (a challenge certificate).</summary>
public sealed class FixedCertificateSource(CertificateMaterial material, string mode) : ICertificateSource
{
    public string Mode => mode;
    public CertificateMaterial Current => material;
    public event Action<CertificateMaterial>? Changed { add { } remove { } }
    public void Dispose() { }
}
