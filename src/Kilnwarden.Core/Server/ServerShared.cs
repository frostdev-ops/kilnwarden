// SPDX-License-Identifier: MIT
using Kilnwarden.Hosting;

namespace Kilnwarden.Server;

/// <summary>What every listener of one host shares: the pipeline, services, log, the per-IP limiter, the current policy
/// (swapped on a settings reload) and the counters.</summary>
public sealed class ServerShared(RequestPipeline pipeline, WebAdminServices services, IWebAdminLog log, ClientLimiter limiter, Func<ServerPolicy> policy)
{
    public RequestPipeline Pipeline { get; } = pipeline;
    public WebAdminServices Services { get; } = services;
    public IWebAdminLog Log { get; } = log;
    public ClientLimiter Limiter { get; } = limiter;
    public Func<ServerPolicy> Policy { get; } = policy;
    public ServerCounters Counters { get; } = new();

    /// <summary>Open connections over all listeners (limits.maxConnections).</summary>
    internal int OpenConnections;
}

/// <summary>Totals since start, for the metrics module and the lab checks.</summary>
public sealed class ServerCounters
{
    public long Accepted, Refused, Requests, WebSockets, HandshakesOk, HandshakesFailed, Timeouts, Errors, RateLimited;
}

/// <param name="Connections">Open when the stop began.</param>
/// <param name="Graceful">Ended within the grace time (WebSockets closed with 1001, requests finished).</param>
/// <param name="Aborted">Still running after the grace time: cancelled and their sockets reset.</param>
public readonly record struct StopReport(int Connections, int Graceful, int Aborted, bool AcceptEnded);
