// SPDX-License-Identifier: MIT
using System.Reflection;

namespace Kilnwarden;

/// <summary>The mod's names, from Directory.Build.props (the one place a rename touches besides namespaces).</summary>
public static class Product
{
    private static readonly Dictionary<string, string> Meta = typeof(Product).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Where(a => a.Value is not null)
        .ToDictionary(a => a.Key, a => a.Value!);

    /// <summary>The Kiln mod id (kiln.mod.json "id"), also the data folder's name under kiln/data.</summary>
    public static string ModId { get; } = Meta.GetValueOrDefault("WebAdmin.ModId", "kilnwarden");

    /// <summary>The name people see (logs, page titles, the certificate's subject).</summary>
    public static string Name { get; } = Meta.GetValueOrDefault("WebAdmin.ProductName", "Kilnwarden");

    public static string Version { get; } = Meta.GetValueOrDefault("WebAdmin.Version", "0.0.0");

    /// <summary>The oldest Kiln the mod works with (kiln.mod.json's "kiln" dependency). Kiln's host does not enforce it
    /// against older dev builds, so the KAP client checks Kiln.Admin's capabilities itself.</summary>
    public static string KilnFloor { get; } = Meta.GetValueOrDefault("WebAdmin.KilnFloor", "0.2.0-dev.30.9");

    /// <summary>The native TLS library's version: its file is kwtls-&lt;version&gt;.dll in the mod folder.</summary>
    public static string KwtlsVersion { get; } = Meta.GetValueOrDefault("WebAdmin.KwtlsVersion", "0.0.0");
}
