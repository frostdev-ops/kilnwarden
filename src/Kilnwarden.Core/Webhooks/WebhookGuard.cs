// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Kilnwarden.Webhooks;

/// <summary>Where webhooks may go (lane C, docs/step2-lane-c.md C3): public internet addresses only. A webhook is a
/// server-side request the panel makes for an owner, so it must never reach the server's loopback, LAN, link-local or
/// cloud metadata addresses, through a literal, a name, a redirect or a DNS answer that changes between check and use.
/// The owner's private-target opt-in (webhook-settings.json allowPrivateTargets, off by default; a security trade-off the
/// owner chooses, with a warning) adds RFC 1918 and IPv6 ULA addresses, and plain http:// to those only. Loopback (KAP
/// listens there), link-local and cloud metadata, multicast, unspecified and broadcast stay refused in every form.</summary>
public static partial class WebhookGuard
{
    public const int MaxUrl = 512;

    public static readonly IReadOnlySet<string> DiscordHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "discord.com", "discordapp.com", "ptb.discord.com", "canary.discord.com",
    };

    /// <summary>Whether <paramref name="a"/> is a global unicast address (not loopback, private, CGNAT, link-local,
    /// multicast, documentation, benchmarking, reserved; IPv4-mapped, NAT64, 6to4 and Teredo forms are judged by the
    /// IPv4 address they carry or refused).</summary>
    public static bool IsPublic(IPAddress a)
    {
        if (a.IsIPv4MappedToIPv6) return IsPublic(a.MapToIPv4());
        if (a.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = a.GetAddressBytes();
            return !(b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224 ||
                     b[0] == 100 && (b[1] & 0xC0) == 64 ||          // 100.64/10 CGNAT
                     b[0] == 169 && b[1] == 254 ||                  // link-local, cloud metadata
                     b[0] == 172 && (b[1] & 0xF0) == 16 ||
                     b[0] == 192 && b[1] == 168 ||
                     b[0] == 192 && b[1] == 0 && (b[2] == 0 || b[2] == 2) ||
                     b[0] == 192 && b[1] == 88 && b[2] == 99 ||     // 6to4 relay anycast
                     b[0] == 198 && (b[1] & 0xFE) == 18 ||          // benchmarking
                     b[0] == 198 && b[1] == 51 && b[2] == 100 ||
                     b[0] == 203 && b[1] == 0 && b[2] == 113);
        }
        if (a.AddressFamily != AddressFamily.InterNetworkV6) return false;
        if (IPAddress.IPv6Loopback.Equals(a) || IPAddress.IPv6None.Equals(a) || IPAddress.IPv6Any.Equals(a)) return false;
        if (a.IsIPv6LinkLocal || a.IsIPv6SiteLocal || a.IsIPv6Multicast || a.IsIPv6UniqueLocal || a.IsIPv6Teredo) return false;
        var x = a.GetAddressBytes();
        if ((x[0] & 0xE0) != 0x20) return false;                     // only 2000::/3 is global unicast
        if (x[0] == 0x20 && x[1] == 0x01 && x[2] == 0x0d && x[3] == 0xb8) return false; // 2001:db8::/32 documentation
        if (x[0] == 0x20 && x[1] == 0x01 && x[2] == 0x00 && x[3] == 0x00) return false; // Teredo 2001::/32
        if (x[0] == 0x20 && x[1] == 0x02) return IsPublic(new IPAddress(x.AsSpan(2, 4))); // 6to4 carries an IPv4
        return true;
    }

    /// <summary>Whether &lt;paramref name="a"/&gt; is in a private LAN range: RFC 1918 (10/8, 172.16/12, 192.168/16) or IPv6
    /// unique local (fc00::/7), also as IPv4-mapped IPv6. Nothing else: not loopback, link-local, CGNAT, NAT64, 6to4.</summary>
    public static bool IsPrivateLan(IPAddress a)
    {
        if (a.IsIPv4MappedToIPv6) a = a.MapToIPv4();
        var b = a.GetAddressBytes();
        if (a.AddressFamily == AddressFamily.InterNetwork)
            return b[0] == 10 || b[0] == 172 && (b[1] & 0xF0) == 16 || b[0] == 192 && b[1] == 168;
        return a.AddressFamily == AddressFamily.InterNetworkV6 && (b[0] & 0xFE) == 0xFC;
    }

    /// <summary>Whether a webhook may connect to &lt;paramref name="a"/&gt;: a public address, or with the owner's opt-in a
    /// private LAN one (&lt;see cref="IsPrivateLan"/&gt;). Loopback, link-local, multicast, unspecified and broadcast never.</summary>
    public static bool IsAllowed(IPAddress a, bool allowPrivate) => IsPublic(a) || allowPrivate && IsPrivateLan(a);

    /// <summary>The connect-time check, on the addresses the sender resolved itself (or the URL's literal): every one must
    /// be allowed (a name with one refused address is refused: no picking around a rebinding answer), and plain http only
    /// to private LAN addresses. Returns the address to connect to (the first, the one checked).</summary>
    public static IPAddress CheckConnect(IReadOnlyList<IPAddress> addresses, bool allowPrivate, bool plainHttp)
    {
        if (addresses.Count == 0) throw new WebhookRefusedException("the host has no address");
        if (addresses.Any(a => !IsAllowed(a, allowPrivate)))
            throw new WebhookRefusedException(allowPrivate
                ? "the host resolves to an address webhooks never reach (loopback, link-local, multicast, unspecified or broadcast)"
                : "the host resolves to a non-public address");
        if (plainHttp && addresses.Any(a => !IsPrivateLan(a))) throw new WebhookRefusedException("plain http:// only to private LAN addresses");
        return addresses[0];
    }

    /// <summary>NAT64 (64:ff9b::/96) is outside 2000::/3 and refused by &lt;see cref="IsPublic"/&gt; already.
    /// &lt;paramref name="allowPrivate"/&gt;: the owner's opt-in (private LAN literals and names, plain http:// for json hooks;
    /// the connect-time check holds http to private addresses).</summary>
    public static string? CheckUrl(string kind, string? url, out Uri? uri, bool allowPrivate = false)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(url) || url.Length > MaxUrl) return $"url: 1 to {MaxUrl} characters";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return "url: an absolute https:// URL";
        var plain = u.Scheme == Uri.UriSchemeHttp;
        if (!(u.Scheme == Uri.UriSchemeHttps || plain && allowPrivate && kind == "json"))
            return allowPrivate ? "url: an absolute https:// URL (http:// only for a JSON hook to a private LAN address)" : "url: an absolute https:// URL";
        if (u.UserInfo.Length > 0) return "url: no user name or password in the URL";
        if (u.Fragment.Length > 0) return "url: no #fragment";
        var host = u.IdnHost.TrimEnd('.').ToLowerInvariant();
        if (u.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
        {
            if (!IPAddress.TryParse(host.Trim('[', ']'), out var ip) || !IsAllowed(ip, allowPrivate))
                return allowPrivate ? "url: not a public or private LAN address (loopback, link-local, multicast and broadcast are never reached)"
                                    : "url: the address is not a public internet address";
            if (plain && !IsPrivateLan(ip)) return "url: plain http:// only to a private LAN address";
        }
        else if (u.HostNameType != UriHostNameType.Dns || LoopbackName(host)) return "url: a public internet host name";
        else if (!allowPrivate && (!host.Contains('.') || LocalName(host))) return "url: a public internet host name";
        if (kind == "discord")
        {
            if (!DiscordHosts.Contains(host) || !u.IsDefaultPort) return "url: a Discord webhook URL (https://discord.com/api/webhooks/...)";
            if (!DiscordPath().IsMatch(u.AbsolutePath) || u.Query.Length > 0) return "url: a Discord webhook URL (https://discord.com/api/webhooks/<id>/<token>)";
        }
        else if (kind != "json") return "kind: discord or json";
        uri = u;
        return null;
    }

    /// <summary>Names of this machine, refused even with the private-target opt-in.</summary>
    private static bool LoopbackName(string host)
    {
        // a trailing root dot ("localhost.") names the same host; any case does too
        host = host.TrimEnd('.').ToLowerInvariant();
        return host is "localhost" || host.EndsWith(".localhost", StringComparison.Ordinal) || host is "localdomain" ||
               host.EndsWith(".localdomain", StringComparison.Ordinal);
    }

    private static bool LocalName(string host) =>
        LoopbackName(host) || host.EndsWith(".local", StringComparison.Ordinal) ||
        host.EndsWith(".internal", StringComparison.Ordinal) || host.EndsWith(".lan", StringComparison.Ordinal) ||
        host.EndsWith(".home.arpa", StringComparison.Ordinal) || host.EndsWith(".intranet", StringComparison.Ordinal) ||
        host.EndsWith(".corp", StringComparison.Ordinal);

    /// <summary>The URL as answers and logs show it: scheme, host and (Discord) the webhook id; never the token or path.</summary>
    public static string Mask(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return "***";
        var m = DiscordPath().Match(u.AbsolutePath);
        return m.Success ? $"https://{u.Host}/api/webhooks/{m.Groups[1].Value}/***" : $"{u.Scheme}://{u.Authority}/***";
    }

    [GeneratedRegex(@"^/api/(?:v\d{1,2}/)?webhooks/(\d{5,25})/[A-Za-z0-9_-]{20,200}$")]
    private static partial Regex DiscordPath();
}
