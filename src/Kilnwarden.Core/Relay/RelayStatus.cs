// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;

namespace Kilnwarden.Relay;

/// <summary>What the relay client is doing, for the console's <c>kilnwarden status</c>.</summary>
public sealed class RelayStatus(Func<RelayStatus.Info> describe)
{
    public sealed record Info(bool Enabled, string Host, string State, string? LastError);

    public Info Describe() => describe();
}

/// <summary>Claim links for the relay's Discord claim pages (Panel settings > Hosted access > Claim this name, and the
/// console's <c>kilnwarden claim</c>): docs/protocol-relay-broker.md §1.1.</summary>
public sealed class RelayClaims(Func<string, RelayClaims.Link> make)
{
    /// <summary>A claim link (or why there is none: <see cref="Error"/>). <see cref="Warnings"/> name what keeps the server
    /// from connecting afterwards (a claim lapses after an hour without it).</summary>
    public sealed record Link(string? Url, string Host, string? Fingerprint, DateTimeOffset? Expires, IReadOnlyList<string> Warnings, string? Error);

    /// <summary>A new link (<paramref name="source"/>: "settings" or "console"; the caller writes the audit entry).</summary>
    public Link Make(string source) => make(source);
}

/// <summary>The relay refused the WebSocket upgrade with this HTTP status.</summary>
internal sealed class RelayRefusedException(int status, Exception inner) : IOException($"the relay answered HTTP {status}", inner)
{
    public int Status { get; } = status;
}

/// <summary>A failed relay attempt in plain words: the log line and Panel settings' "last error". Each names what the
/// owner can do; the relay's answers are in docs/protocol-relay-broker.md.</summary>
internal static class RelayErrors
{
    public static string Explain(Exception e, RelaySettings s)
    {
        var origin = Uri.TryCreate(s.Origin, UriKind.Absolute, out var u) ? $"{u.Host}:{u.Port}" : s.Origin;
        switch (e)
        {
            case RelayRefusedException { Status: 403 }:
                return $"the relay does not know {s.Host} for this server's key: claim the name (Panel settings > Hosted access > Claim this name, " +
                       "or the console command kilnwarden claim; or send relay-public.pem to the relay's operator), or it was released or banned (relay HTTP 403)";
            case RelayRefusedException { Status: 401 }:
                return "the relay refused this server's signature: the server's clock is off by more than two minutes, or the relay restarted a " +
                       "moment ago (retrying) (relay HTTP 401)";
            case RelayRefusedException { Status: 429 }:
                return "the relay is limiting this address: too many attempts or too many servers behind one address (retrying) (relay HTTP 429)";
            case RelayRefusedException { Status: 404 }:
                return $"there is no relay at {s.Origin} (relay.json origin) (HTTP 404)";
            case RelayRefusedException { Status: 503 or 502 }:
                return "the relay is full or restarting (retrying) (relay HTTP " + ((RelayRefusedException)e).Status + ")";
            case RelayRefusedException r:
                return $"the relay answered HTTP {r.Status}";
            case TimeoutException:
                return $"no answer from {origin} within 10 s (retrying)";
        }
        for (var x = e; x is not null; x = x.InnerException)
        {
            if (x is SocketException se)
                return se.SocketErrorCode switch
                {
                    SocketError.HostNotFound or SocketError.TryAgain or SocketError.NoData =>
                        $"cannot look up {origin}: this server's DNS does not answer for it",
                    SocketError.ConnectionRefused or SocketError.TimedOut or SocketError.NetworkUnreachable or SocketError.HostUnreachable
                        or SocketError.AccessDenied =>
                        $"cannot connect to {origin}: outbound HTTPS is blocked here or the network is down (the relay needs outbound TCP 443 only, " +
                        "no inbound port)",
                    _ => $"network error {se.SocketErrorCode} reaching {origin}",
                };
            // SocketsHttpHandler's 8 s connect timeout: a firewall that drops (rather than refuses) the connection
            if (x is TimeoutException && x != e)
                return $"cannot connect to {origin} within 8 s: outbound HTTPS is blocked (dropped) here or the network is down (the relay " +
                       "needs outbound TCP 443 only, no inbound port)";
            if (x is AuthenticationException)
                return $"TLS to {origin} failed: something between this server and the relay intercepts HTTPS, or this server's clock or " +
                       "certificate store is wrong";
        }
        return $"{e.GetType().Name}: {(e.Message.Length > 200 ? e.Message[..200] : e.Message)}";
    }
}
