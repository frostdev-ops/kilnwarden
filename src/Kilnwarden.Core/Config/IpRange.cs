// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Sockets;

namespace Kilnwarden.Config;

/// <summary>An address or a CIDR range ("10.0.0.0/8", "fd00::/8", "192.168.1.7"). IPv4-mapped IPv6 addresses match
/// their IPv4 ranges.</summary>
public readonly record struct IpRange(IPAddress Network, int PrefixLength)
{
    public static bool TryParse(string? s, out IpRange range)
    {
        range = default;
        if (string.IsNullOrWhiteSpace(s) || s.Length > 64 || s.Contains('%')) return false;
        var slash = s.IndexOf('/');
        var addrText = slash < 0 ? s : s[..slash];
        if (!IPAddress.TryParse(addrText, out var addr)) return false;
        var max = addr.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        var prefix = max;
        if (slash >= 0 && (!int.TryParse(s.AsSpan(slash + 1), System.Globalization.NumberStyles.None, null, out prefix) || prefix > max))
            return false;
        range = new IpRange(Mask(addr, prefix), prefix);
        return true;
    }

    public bool Contains(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily != Network.AddressFamily) return false;
        return Mask(address, PrefixLength).Equals(Network);
    }

    private static IPAddress Mask(IPAddress a, int prefix)
    {
        var bytes = a.GetAddressBytes();
        for (var i = 0; i < bytes.Length; i++)
        {
            var bits = Math.Clamp(prefix - i * 8, 0, 8);
            bytes[i] &= (byte)(0xFF << (8 - bits));
        }
        return new IPAddress(bytes);
    }

    public override string ToString() => $"{Network}/{PrefixLength}";
}
