// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;

namespace Kilnwarden.Relay;

/// <summary>What the relay client is doing, for the console's <c>kilnwarden status</c>.</summary>
public sealed class RelayStatus(Func<RelayStatus.Info> describe)
{
    /// <param name="Problem">What the last failure was, for the setup wizard's plain-words fixes (0.1.2):
    /// <see cref="RelayErrors.Kind"/>; null when connected or never tried.</param>
    public sealed record Info(bool Enabled, string Host, string State, string? LastError, string? Problem = null);

    public Info Describe() => describe();

    /// <summary>The relay link came up (any thread): a claim went through, or the link is back.</summary>
    public event Action? Connected;

    internal void RaiseConnected()
    {
        try { Connected?.Invoke(); }
        catch (Exception) { /* a listener's failure never takes the relay loop down */ }
    }
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

/// <summary>What the relay told this panel about its name (lane L1b): the move or release scheduled for it, and when
/// this server's key takes a name over that is moving to it. For the console's <c>kilnwarden status</c>.</summary>
public sealed class RelayMoves
{
    private readonly Func<object?> _scheduled;
    private readonly Func<DateTimeOffset?> _movingHere;

    internal RelayMoves(Func<RelayControl.Scheduled?> scheduled, Func<DateTimeOffset?> movingHere)
    {
        _scheduled = () => scheduled();
        _movingHere = movingHere;
    }

    /// <summary>A sentence for the console, or null when nothing is scheduled either way.</summary>
    public string? Describe()
    {
        if (_scheduled() is RelayControl.Scheduled c)
            return $"{(c.IsRelease ? "a release" : "a move to another server's key")} is scheduled for {c.At.UtcDateTime:yyyy-MM-dd HH:mm} UTC " +
                   "(not you? Panel settings > Hosted access > Undo)";
        return _movingHere() is { } at ? $"this server's key takes the name over on {at.UtcDateTime:yyyy-MM-dd HH:mm} UTC (a move's 24 hours)" : null;
    }
}

/// <summary>The relay refused the WebSocket upgrade with this HTTP status.</summary>
internal sealed class RelayRefusedException(int status, Exception inner) : IOException($"the relay answered HTTP {status}", inner)
{
    public int Status { get; } = status;

    /// <summary>When the name moves to this server's key (the relay's <c>X-Kwa-Moving-At</c>, lane L1b).</summary>
    public DateTimeOffset? MovingAt { get; init; }
}

/// <summary>A failed relay attempt in plain words: the log line and Panel settings' "last error". Each names what the
/// owner can do; the relay's answers are in docs/protocol-relay-broker.md.</summary>
internal static class RelayErrors
{
    /// <summary>The failure as one word, for the setup wizard and <c>kilnwarden status</c> (0.1.2): "unclaimed" (the relay
    /// does not know the name for this key), "moving" (the name moves here later), "moved" (it moved away or was
    /// released), "clock", "limited", "busy", "no-relay", "dns", "blocked" (outbound HTTPS), "tls", "timeout" or "other".</summary>
    public static string Kind(Exception e, RelayControl.Scheduled? past = null)
    {
        switch (e)
        {
            case RelayRefusedException { Status: 403, MovingAt: not null }: return "moving";
            case RelayRefusedException { Status: 401 or 403 } when past is not null: return "moved";
            case RelayRefusedException { Status: 403 }: return "unclaimed";
            case RelayRefusedException { Status: 401 }: return "clock";
            case RelayRefusedException { Status: 429 }: return "limited";
            case RelayRefusedException { Status: 404 }: return "no-relay";
            case RelayRefusedException { Status: >= 500 }: return "busy";
            case RelayRefusedException: return "other";
            case TimeoutException: return "timeout";
        }
        for (var x = e; x is not null; x = x.InnerException)
        {
            if (x is SocketException se)
                return se.SocketErrorCode is SocketError.HostNotFound or SocketError.TryAgain or SocketError.NoData ? "dns" : "blocked";
            if (x is TimeoutException && x != e) return "blocked";
            if (x is AuthenticationException) return "tls";
        }
        return "other";
    }

    /// <param name="past">A move or release the relay had scheduled for the name whose time has passed (lane L1b).</param>
    public static string Explain(Exception e, RelaySettings s, RelayControl.Scheduled? past = null)
    {
        var origin = Uri.TryCreate(s.Origin, UriKind.Absolute, out var u) ? $"{u.Host}:{u.Port}" : s.Origin;
        switch (e)
        {
            case RelayRefusedException { Status: 403, MovingAt: { } at }:
                return $"{s.Host} is moving to this server's key: it switches on {at.UtcDateTime:yyyy-MM-dd HH:mm} UTC, after the 24 hours in which the " +
                       "server it leaves keeps it and can undo the move; this server connects then (relay HTTP 403)";
            case RelayRefusedException { Status: 401 } when past is { IsRelease: false }:
                return $"{s.Host} moved to another server's key on {past.At.UtcDateTime:yyyy-MM-dd HH:mm} UTC (a move asked on the claim pages): " +
                       "this server's key no longer reaches it. If you did not ask for that, ask the relay's operator on the Kiln Discord (relay HTTP 401)";
            case RelayRefusedException { Status: 403 } when past is { IsRelease: true }:
                return $"{s.Host} was released on {past.At.UtcDateTime:yyyy-MM-dd HH:mm} UTC (asked on the claim pages); claim it again with a new " +
                       "claim link if it is still free (relay HTTP 403)";
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
