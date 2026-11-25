// SPDX-License-Identifier: MIT
using System.Net;
using Kilnwarden.Config;
using Kilnwarden.Http;

namespace Kilnwarden.Server;

/// <summary>settings.json, parsed once per change into what each request checks: the client allowlist, the trusted
/// proxies and X-Forwarded-*, the Host check (DNS-rebinding guard), the limits and HSTS.</summary>
public sealed class ServerPolicy
{
    private readonly IpRange[] _allow;
    private readonly IpRange[] _proxies;
    private readonly HashSet<string> _hosts;

    private ServerPolicy(ServerSettings s, bool publicBind)
    {
        Settings = s;
        Limits = s.Limits;
        HttpLimits = new HttpLimits(s.Limits.MaxHeaderBytes, s.Limits.MaxHeaderCount, s.Limits.MaxTargetBytes);
        _allow = [.. s.IpAllowlist.Select(a => IpRange.TryParse(a, out var r) ? r : default).Where(r => r.Network is not null)];
        _proxies = s.ReverseProxy.Enabled
            ? [.. s.ReverseProxy.TrustedProxies.Select(a => IpRange.TryParse(a, out var r) ? r : default).Where(r => r.Network is not null)]
            : [];
        _hosts = new HashSet<string>(s.AllowedHosts.Concat(s.Tls.Hostnames).Select(h => h.ToLowerInvariant()), StringComparer.Ordinal);
    }

    public static ServerPolicy From(ServerSettings settings, bool publicBind) => new(settings, publicBind);

    public ServerSettings Settings { get; }
    public LimitSettings Limits { get; }
    public HttpLimits HttpLimits { get; }
    public bool Hsts => Settings.Hsts;

    /// <summary>The optional allowlist; loopback is always allowed (the owner on the machine, the launcher's tunnel),
    /// except for a peer a relay announced (<paramref name="relayed"/>), which must be on the list itself.</summary>
    public bool ClientAllowed(IPAddress client, bool relayed = false)
    {
        if (_allow.Length == 0 || !relayed && IPAddress.IsLoopback(client)) return true;
        foreach (var r in _allow)
            if (r.Contains(client)) return true;
        return false;
    }

    public bool IsTrustedProxy(IPAddress peer)
    {
        foreach (var r in _proxies)
            if (r.Contains(peer)) return true;
        return false;
    }

    /// <summary>The Host check: "localhost" and IP literals always; DNS names when listed (allowedHosts, tls.hostnames),
    /// on both loopback and public binds.</summary>
    public bool HostAllowed(string host)
    {
        var name = HostName(host);
        if (name.Length == 0) return false;
        if (name == "localhost" || IPAddress.TryParse(name.Trim('[', ']'), out _)) return true;
        return _hosts.Contains(name);
    }

    /// <summary>The client address, scheme and host as the browser sees them. X-Forwarded-For/-Proto/-Host count only
    /// when the reverse-proxy switch is on and the TCP peer is a trusted proxy; the client is the rightmost address in
    /// X-Forwarded-For that is not a trusted proxy; without X-Forwarded-Host the Host header the proxy passed on counts.
    /// A proxy that forwards anything must send one X-Forwarded-For chain and X-Forwarded-Proto https; ambiguous or
    /// unreadable trusted values fail closed.</summary>
    public (IPAddress Client, string Scheme, string Host) Resolve(HttpRequest req, IPAddress peer, bool isTls, bool allowProxyHeaders = true)
    {
        var client = peer.IsIPv4MappedToIPv6 ? peer.MapToIPv4() : peer;
        var scheme = isTls ? "https" : "http";
        var host = req.Host;
        var trusted = allowProxyHeaders && IsTrustedProxy(client);
        if (!isTls && !IPAddress.IsLoopback(client) && !trusted)
            throw new HttpError(403, "plain HTTP requires a trusted reverse proxy");
        if (_proxies.Length == 0 || !trusted)
        {
            if (!ClientAllowed(client)) throw new HttpError(403, "client address is not allowed");
            return (client, scheme, host);
        }
        foreach (var name in new[] { "X-Forwarded-For", "X-Forwarded-Proto", "X-Forwarded-Host" })
            if (req.Headers.CountOf(name) > 1) throw new HttpError(400, "ambiguous proxy headers");
        var forwarded = req.Headers.Get("X-Forwarded-For");
        if (forwarded is not null && ForwardedFor(forwarded) is null) throw new HttpError(400, "invalid proxy client address");
        if (ForwardedFor(forwarded) is { } chain)
        {
            for (var i = chain.Count - 1; i >= 0; i--)
            {
                client = chain[i];
                if (!IsTrustedProxy(client)) break;
            }
        }
        var proto = req.Headers.Get("X-Forwarded-Proto")?.Trim().ToLowerInvariant();
        if (proto is not null && proto is not ("https" or "http")) throw new HttpError(400, "invalid proxy scheme");
        if (proto is "https" or "http") scheme = proto;
        if (req.Headers.Get("X-Forwarded-Host")?.Trim() is { } fh)
        {
            if (!HttpParser.IsHost(fh)) throw new HttpError(400, "invalid proxy host");
            host = fh;
        }
        var forwarding = forwarded is not null || proto is not null || req.Headers.Contains("X-Forwarded-Host");
        if ((!isTls && !IPAddress.IsLoopback(peer) || forwarding) && (forwarded is null || proto != "https"))
            throw new HttpError(400, "proxy must send one X-Forwarded-For chain and X-Forwarded-Proto: https");
        if (!ClientAllowed(client)) throw new HttpError(403, "client address is not allowed");
        return (client, scheme, host);
    }

    /// <summary>X-Forwarded-For as addresses (left = original client); null when any element is unreadable.</summary>
    public static List<IPAddress>? ForwardedFor(string? header)
    {
        if (string.IsNullOrWhiteSpace(header) || header.Length > 1024) return null;
        var list = new List<IPAddress>();
        foreach (var part in header.Split(','))
        {
            var t = part.Trim();
            if (t.Length == 0 || t.Contains('%') || !IPAddress.TryParse(t, out var a)) return null;
            list.Add(a.IsIPv4MappedToIPv6 ? a.MapToIPv4() : a);
            if (list.Count > 32) return null;
        }
        return list;
    }

    /// <summary>The host without its port, lower-case ("[::1]" keeps its brackets).</summary>
    public static string HostName(string host)
    {
        if (host.StartsWith('['))
        {
            var close = host.IndexOf(']');
            return close < 0 ? "" : host[..(close + 1)].ToLowerInvariant();
        }
        var colon = host.LastIndexOf(':');
        return (colon < 0 ? host : host[..colon]).ToLowerInvariant();
    }

    /// <summary>Whether <paramref name="host"/> names this machine or an IP literal (no HSTS for these).</summary>
    public static bool IsLocalOrLiteral(string host)
    {
        var name = HostName(host);
        return name == "localhost" || name.EndsWith(".localhost", StringComparison.Ordinal) || IPAddress.TryParse(name.Trim('[', ']'), out _);
    }
}
