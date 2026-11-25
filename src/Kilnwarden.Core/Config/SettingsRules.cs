// SPDX-License-Identifier: MIT
using System.Net;

namespace Kilnwarden.Config;

/// <summary>What makes settings.json valid. A refused file keeps the last good settings (hot reload) or the defaults
/// (first start), and the problems go to kiln.log.</summary>
public static class SettingsRules
{
    public static IReadOnlyList<string> Validate(ServerSettings s)
    {
        var p = new List<string>();
        // a hand-edited file can null out any part ("tls": null); refuse it instead of failing on it
        if (s.Bind is null) p.Add("bind must be a string");
        if (s.Tls is null) p.Add("tls must be an object");
        if (s.Tls?.CertFile is null || s.Tls?.KeyFile is null || s.Tls?.Hostnames is null || s.Tls.Hostnames.Any(h => h is null))
            p.Add("tls.certFile, tls.keyFile and tls.hostnames must not be null");
        if (s.ReverseProxy?.TrustedProxies is null) p.Add("reverseProxy.trustedProxies must be a list");
        if (s.AllowedHosts is null || s.AllowedHosts.Any(h => h is null)) p.Add("allowedHosts must be a list of names");
        if (s.IpAllowlist is null) p.Add("ipAllowlist must be a list");
        if (s.Limits is null) p.Add("limits must be an object");
        if (p.Count > 0) return p;
        var tls = s.Tls!;
        if (!Enum.IsDefined(tls.Mode)) p.Add("tls.mode must be a known value");
        if (!Enum.IsDefined(tls.Engine)) p.Add("tls.engine must be a known value");
        if (s.Format != 1) p.Add($"format {s.Format} is not supported (1)");
        if (!IPAddress.TryParse(s.Bind, out _)) p.Add($"bind '{s.Bind}' is not an IP address (127.0.0.1, 0.0.0.0, ::)");
        if (s.Port is < 0 or > 65535) p.Add($"port {s.Port} is out of range (0 = query port + 100)");
        if (tls.Mode == TlsMode.File && (tls.CertFile.Length == 0 || tls.KeyFile.Length == 0))
            p.Add("tls.mode file needs tls.certFile and tls.keyFile");
        foreach (var a in s.IpAllowlist!.Concat(s.ReverseProxy!.TrustedProxies!))
            if (!IpRange.TryParse(a, out _)) p.Add($"'{a}' is not an address or CIDR range");
        var l = s.Limits!;
        if (l.MaxConnections is < 1 or > 100_000) p.Add("limits.maxConnections must be 1..100000");
        if (l.MaxConnectionsPerIp < 1) p.Add("limits.maxConnectionsPerIp must be at least 1");
        if (l.MaxHeaderBytes is < 1024 or > 1 << 20) p.Add("limits.maxHeaderBytes must be 1024..1048576");
        if (l.MaxHeaderCount is < 8 or > 1000) p.Add("limits.maxHeaderCount must be 8..1000");
        if (l.MaxTargetBytes is < 256 or > 1 << 16) p.Add("limits.maxTargetBytes must be 256..65536");
        if (l.MaxBodyBytes is < 0 or > 64L << 20) p.Add("limits.maxBodyBytes must be 0..64 MiB (buffered in process)");
        if (l.MaxWebSocketMessageBytes is < 1024 or > 64 << 20) p.Add("limits.maxWebSocketMessageBytes must be 1 KiB..64 MiB");
        foreach (var (name, ms) in new[] { ("handshakeTimeoutMs", l.HandshakeTimeoutMs), ("headerTimeoutMs", l.HeaderTimeoutMs),
                     ("keepAliveTimeoutMs", l.KeepAliveTimeoutMs), ("bodyTimeoutMs", l.BodyTimeoutMs), ("writeTimeoutMs", l.WriteTimeoutMs) })
            if (ms is < 100 or > 600_000) p.Add($"limits.{name} must be 100..600000");
        if (!double.IsFinite(l.RequestsPerSecondPerIp) || !double.IsFinite(l.ConnectionsPerSecondPerIp) || l.RequestsPerSecondPerIp <= 0 || l.ConnectionsPerSecondPerIp <= 0 || l.RequestBurstPerIp < 1) p.Add("limits: rates must be positive");
        return p;
    }
}
