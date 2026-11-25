// SPDX-License-Identifier: MIT
using System.Text.RegularExpressions;

namespace Kilnwarden.Identity;

/// <summary>identity.json in the data folder: sign-in with Steam and Discord (DESIGN §3). Both are off until the owner
/// switches them on, and neither ever creates an account or grants a role: a provider only signs in an account that its
/// own user linked (opt-in, after re-authenticating). Hot-reloaded.</summary>
public sealed partial class IdentitySettings
{
    public int Format { get; set; } = 1;
    public SteamSettings Steam { get; set; } = new();
    public DiscordSettings Discord { get; set; } = new();

    public static IReadOnlyList<string> Validate(IdentitySettings s)
    {
        var p = new List<string>();
        if (s.Steam is null || s.Discord is null) return ["steam and discord are required objects"];
        var d = s.Discord;
        if (d.AllowedRoles is null) p.Add("discord.allowedRoles must be a list");
        if (d.Enabled)
        {
            if (!SnowflakeRe().IsMatch(d.GuildId ?? "")) p.Add("discord.guildId must be the Discord server (guild) id the broker reports roles of");
            if (d.AllowedRoles is { } roles && roles.Any(r => !SnowflakeRe().IsMatch(r ?? ""))) p.Add("discord.allowedRoles holds Discord role ids (digits)");
        }
        if (!IsBrokerOrigin(d.Broker))
            p.Add("discord.broker must be an https origin (\"https://frostdev.io\")");
        foreach (var (name, map, from) in new[] { ("discord.roleMap", d.RoleMap, (Func<string, bool>)(x => SnowflakeRe().IsMatch(x))), ("steam.roleMap", s.Steam.RoleMap, x => GroupRe().IsMatch(x)) })
        {
            if (map is null || map.Count > 32) { p.Add($"{name}: at most 32 mappings"); continue; }
            foreach (var m in map)
            {
                if (m is null || !from(m.From ?? "")) p.Add($"{name}: '{m?.From}' is not a {(name.StartsWith("discord") ? "Discord role id" : "permission group name")}");
                else if (!Auth.RoleNames.IsValid(m.Role)) p.Add($"{name}: '{m.Role}' is not a role id");
                else if (m.Role == Auth.BuiltinRoles.Owner) p.Add($"{name}: the owner role is never given by a mapping");
            }
        }
        return p;
    }

    internal static bool IsBrokerOrigin(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var b) && b.Scheme == "https" && b.AbsolutePath == "/" &&
        b.Query.Length == 0 && b.UserInfo.Length == 0 && b.Fragment.Length == 0 && value!.TrimEnd('/') == b.GetLeftPart(UriPartial.Authority);

    [GeneratedRegex("^[0-9]{5,20}$")]
    internal static partial Regex SnowflakeRe();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex GroupRe();
}

public sealed class SteamSettings
{
    /// <summary>Sign-in with Steam (OpenID 2.0 straight to steamcommunity.com, no broker).</summary>
    public bool Enabled { get; set; }

    /// <summary>Kiln permission groups (kiln/config/permissions.json "players" -> the SteamID's "groups") that give a panel
    /// role to the account linked to that SteamID, checked at each Steam sign-in. Empty: none (the default). Whoever can
    /// edit the server's permission groups can then give the panel role.</summary>
    public List<RoleMapping> RoleMap { get; set; } = [];
}

/// <summary>One mapping: a provider's group (a Discord role id, a Kiln permission group) gives a panel role.</summary>
public sealed class RoleMapping
{
    public string From { get; set; } = "";
    public string Role { get; set; } = "";
}

public sealed class DiscordSettings
{
    /// <summary>Sign-in with Discord through the frostdev.io broker (its Ed25519 key is pinned in the build).</summary>
    public bool Enabled { get; set; }

    /// <summary>The broker's origin. Only an origin whose key the build pins can be used.</summary>
    public string Broker { get; set; } = "https://frostdev.io";

    /// <summary>The Discord server whose roles the broker reports (required when enabled).</summary>
    public string GuildId { get; set; } = "";

    /// <summary>Role ids of that server allowed to sign in. Empty: any member of the server. A role here only lets a
    /// linked account sign in; it never gives a panel role (roleMap does that).</summary>
    public List<string> AllowedRoles { get; set; } = [];

    /// <summary>Discord role ids that give a panel role to the account linked to that Discord user, checked at each
    /// Discord sign-in. Empty: none (the default). Whoever can give that role in the Discord server can then give the
    /// panel role to anyone who linked their Discord.</summary>
    public List<RoleMapping> RoleMap { get; set; } = [];
}
