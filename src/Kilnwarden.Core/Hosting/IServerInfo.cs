// SPDX-License-Identifier: MIT
using Kilnwarden.Tls;

namespace Kilnwarden.Hosting;

/// <summary>Where the panel can be reached right now: for the setup link in kiln.log, the launcher, the "Open web
/// panel" button.</summary>
public interface IServerInfo
{
    /// <summary>"https://127.0.0.1:15737/" ...; empty while not listening.</summary>
    IReadOnlyList<Uri> Endpoints { get; }

    /// <summary>The TLS certificate in use (null for plain HTTP or not listening).</summary>
    CertificateInfo? Certificate { get; }

    /// <summary>"kwtls 1.0.0 ...", "sslstream", "off".</summary>
    string TlsEngine { get; }

    /// <summary>Fires (any thread) when the endpoints or the certificate change.</summary>
    event Action? Changed;
}
