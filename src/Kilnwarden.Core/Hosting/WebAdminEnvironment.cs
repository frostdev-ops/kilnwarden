// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;

namespace Kilnwarden.Hosting;

/// <summary>What the host gets from its surroundings: folders, versions, the log, the clock and the game thread. The mod
/// project fills it from Kiln's ModContext; tests and the console harness fill it by hand.</summary>
public sealed class WebAdminEnvironment
{
    /// <summary>kiln/data/kilnwarden: settings, TLS keys, accounts, audit (everything the mod writes).</summary>
    public required string DataDirectory { get; init; }

    /// <summary>kiln/mods/kilnwarden: the mod's own files (web/ app, kwtls-&lt;v&gt;.dll). Read only.</summary>
    public required string ModDirectory { get; init; }

    /// <summary>The server's root folder (enshrouded_server.json, kiln/).</summary>
    public required string ServerDirectory { get; init; }

    public string ModVersion { get; init; } = Product.Version;
    public string KilnVersion { get; init; } = "";
    public required IWebAdminLog Log { get; init; }
    public TimeProvider Time { get; init; } = TimeProvider.System;
    public IGameThread? GameThread { get; init; }

    /// <summary>The game's UDP query port from enshrouded_server.json ("queryPort"), or null.</summary>
    public int? QueryPort() => QueryPortOf(ServerDirectory);

    /// <summary>The query port of the server in <paramref name="serverDirectory"/>, or null.</summary>
    public static int? QueryPortOf(string serverDirectory)
    {
        try
        {
            var cfg = Path.Combine(serverDirectory, "enshrouded_server.json");
            if (File.Exists(cfg) && JsonNode.Parse(File.ReadAllText(cfg))?["queryPort"] is JsonValue v &&
                v.TryGetValue<int>(out var q) && q is > 0 and < 65536)
                return q;
        }
        catch (Exception)
        {
            // unreadable: the caller falls back to the default port
        }
        return null;
    }

    /// <summary>kiln/data/kiln-admin: Kiln.Admin's folder (admin-token, endpoint.json) for the KAP client.</summary>
    public string KilnAdminDataDirectory => Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(DataDirectory))!, "kiln-admin");
}
