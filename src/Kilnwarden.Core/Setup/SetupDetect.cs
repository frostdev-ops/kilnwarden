// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Kilnwarden.Setup;

/// <summary>What the server can tell about where it runs, for the setup wizard's "How will you open your panel?":
/// a container (Docker, Podman, Pterodactyl), a game host's panel, and whether this machine has a public address of its
/// own. Best effort and read-only: every check that fails says "don't know". Under Wine the Linux files are on Z:.</summary>
public static class SetupDetect
{
    public sealed record Facts(bool Container, bool GameHost, bool PublicAddress);

    /// <summary>The checks, made once per call (cheap: a few file probes and the interface list).</summary>
    public static Facts Probe(Func<string, string?>? env = null, Func<string, bool>? exists = null, Func<string, string?>? read = null,
        Func<IEnumerable<IPAddress>>? addresses = null)
    {
        env ??= Environment.GetEnvironmentVariable;
        exists ??= SafeExists;
        read ??= SafeRead;
        addresses ??= InterfaceAddresses;
        return new Facts(InContainer(env, exists, read), OnGameHost(env), addresses().Any(IsPublic));
    }

    private static bool InContainer(Func<string, string?> env, Func<string, bool> exists, Func<string, string?> read)
    {
        if (env("container") is { Length: > 0 }) return true; // podman, systemd-nspawn
        foreach (var root in new[] { "/", "Z:\\" })
        {
            if (exists(root + ".dockerenv") || exists(Path.Combine(root, "run", ".containerenv"))) return true;
            if (read(Path.Combine(root, "proc", "1", "cgroup")) is { } cg &&
                (cg.Contains("docker", StringComparison.Ordinal) || cg.Contains("kubepods", StringComparison.Ordinal) ||
                 cg.Contains("containerd", StringComparison.Ordinal) || cg.Contains("lxc", StringComparison.Ordinal)))
                return true;
        }
        return false;
    }

    /// <summary>Pterodactyl (and the panels built on it) and other game hosts' panels set these for the server.</summary>
    private static bool OnGameHost(Func<string, string?> env) =>
        new[] { "P_SERVER_UUID", "P_SERVER_LOCATION", "P_SERVER_ALLOCATION_LIMIT", "TCADMIN_SERVICE_ID", "NITRADO_SERVICE_ID" }.Any(v => env(v) is { Length: > 0 });

    /// <summary>A public unicast address: not loopback, private, link-local, CGNAT, unique-local or documentation.</summary>
    public static bool IsPublic(IPAddress a)
    {
        if (a.IsIPv4MappedToIPv6) a = a.MapToIPv4();
        return Webhooks.WebhookGuard.IsPublic(a);
    }

    private static IEnumerable<IPAddress> InterfaceAddresses()
    {
        try
        {
            return [.. NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses.Select(u => u.Address))
                .Where(a => a.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)];
        }
        catch (Exception e) when (e is NetworkInformationException or PlatformNotSupportedException or InvalidOperationException)
        {
            return [];
        }
    }

    private static bool SafeExists(string path)
    {
        try { return File.Exists(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException) { return false; }
    }

    private static string? SafeRead(string path)
    {
        try
        {
            var f = new FileInfo(path);
            if (!f.Exists) return null;
            using var s = f.OpenRead();
            var buf = new byte[8192];
            var n = s.Read(buf, 0, buf.Length);
            return System.Text.Encoding.UTF8.GetString(buf, 0, n);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException) { return null; }
    }
}
