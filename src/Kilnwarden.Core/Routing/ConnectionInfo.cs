// SPDX-License-Identifier: MIT
using System.Net;
using Kilnwarden.Tls;

namespace Kilnwarden.Routing;

/// <summary>The connection a request came in on.</summary>
public sealed class ConnectionInfo
{
    public required long Id { get; init; }

    /// <summary>The TCP peer (a reverse proxy when there is one).</summary>
    public required IPAddress PeerAddress { get; init; }

    public int PeerPort { get; init; }
    public required IPEndPoint LocalEndPoint { get; init; }

    /// <summary>TLS details; null on a plain listener.</summary>
    public TlsConnectionInfo? Tls { get; init; }

    /// <summary>The connection came through the relay (hosted access): its peer is the browser address the relay
    /// announced, not a TCP peer of this machine.</summary>
    public bool Relayed { get; init; }
}
