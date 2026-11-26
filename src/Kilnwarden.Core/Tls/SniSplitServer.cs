// SPDX-License-Identifier: MIT
using System.Net;
using Kilnwarden.Server;

namespace Kilnwarden.Tls;

/// <summary>tls.mode acme (0.1.2): a browser that asks for a DNS name (the panel's hosted name, or its own address)
/// gets the ACME certificate; a connection without one, or for "localhost" (https://127.0.0.1:15737, the Kiln
/// Launcher) keeps the self-signed certificate it had before. So the page the owner set things up on, on the server's
/// own machine, keeps working when the Let's Encrypt certificate arrives (it is for another name and would break that
/// page's connection), and the launcher's pin stays the same. Both engines are the panel's own; the ClientHello is read
/// first and handed on unchanged (as for TLS-ALPN-01).</summary>
public sealed class SniSplitServer(ITlsServer named, ITlsServer loopback) : ITlsServer
{
    public string Engine => named.Engine;

    /// <summary>The certificate the panel's names get (the ACME one once issued).</summary>
    public CertificateInfo Certificate => named.Certificate;

    public CertificateInfo LoopbackCertificate => loopback.Certificate;

    /// <summary>A server name that gets the ACME certificate: a DNS name other than localhost.</summary>
    public static bool IsNamed(string? serverName) =>
        serverName is { Length: > 0 } n && !n.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
        !n.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) && !IPAddress.TryParse(n, out _);

    public async ValueTask<TlsStream> AcceptAsync(Stream transport, CancellationToken ct)
    {
        var (read, hello) = await ClientHello.PeekAsync(transport, ct).ConfigureAwait(false);
        var replay = new PrefixStream(read, transport);
        return await (IsNamed(hello?.ServerName) ? named : loopback).AcceptAsync(replay, ct).ConfigureAwait(false);
    }

    public void Dispose()
    {
        named.Dispose();
        loopback.Dispose();
    }
}
