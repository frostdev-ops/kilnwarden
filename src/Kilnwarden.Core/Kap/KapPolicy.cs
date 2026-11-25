// SPDX-License-Identifier: MIT
using Kilnwarden.Modules;
using R = Kilnwarden.Modules.BuiltinRole;

namespace Kilnwarden.Kap;

/// <summary>One Kiln.Admin op as the web gate sees it: the module it belongs to (its on/off switch applies), the
/// permission "&lt;module&gt;.&lt;op&gt;" (DESIGN §3: permissions use Kiln.Admin's op names), the lowest built-in role
/// that has it by default, whether it changes anything (audited) and whether it is dangerous (asked again in the UI).
/// <see cref="Permission"/> null: any signed-in user (ping, status).</summary>
public sealed record KapOpRule(string Op, string Module, string? Permission, R DefaultRole, bool Write, bool Sensitive);

/// <summary>The web panel's map of Kiln.Admin's ops and topics (admin-protocol.md §8, Kiln.Admin 0.2.0-dev.31.10; tests/Offline/Kap/kiln-admin-ops.txt lists its ops) to
/// modules and permissions. Unknown operations and topics fail closed, including for owners. Ops in <see cref="KapOps.Never"/> and
/// <see cref="KapOps.PanelOnly"/> are never reachable from a browser, whatever the role.</summary>
public static class KapPolicy
{
    /// <summary>Unlisted ops and topics (a newer Kiln.Admin's).</summary>
    public const string OtherOps = "server.other_ops";

    /// <summary>command_run/command_complete with <c>steamId</c>: run a line as a chosen player (still capped at the
    /// user's level).</summary>
    public const string RunAs = "commands.run_as";

    public sealed record ModuleInfo(string Id, string Title, string Icon, IReadOnlyList<(string Id, string Title, string Permission, int Order)> Pages);

    /// <summary>The KAP-backed modules (their ids are the admin-ui page names where there is a page).</summary>
    public static readonly IReadOnlyList<ModuleInfo> Modules =
    [
        new("server", "Server", "server", []),
        new("players", "Players", "users", [("players", "Players", "players.players", 10), ("map", "Map", "players.players", 11)]),
        new("world", "World", "globe", [("world", "World", "world.time_get", 30)]),
        new("zones", "Zones", "shield", [("zones", "Zones", "zones.zones_list", 12)]),
        new("items", "Items", "package", [("items", "Items", "items.item_search", 20)]),
        new("spawn", "Spawn", "sparkles", [("spawn", "Spawn", "spawn.spawn", 21)]),
        new("combat", "Combat", "swords", [("combat", "Combat", "combat.health", 31)]),
        new("rules", "Rules", "scale", []),
        new("creatures", "Creatures", "bug", [("creatures", "Creatures", "creatures.creature_status", 32)]),
        new("content", "Content", "book-open", [("content", "Content", "content.content_status", 40)]),
        new("gamedata", "Game data", "database", [("gamedata", "Game data", "gamedata.gamedata_status", 41)]),
        new("build", "World editing", "hammer", []),
        new("progression", "Progression", "trending-up", []),
        new("knowledge", "Knowledge and quests", "scroll", []),
        new("saves", "Saves", "save", [("saves", "Saves", "saves.backup_list", 50)]),
        new("configs", "Configs", "sliders", [("configs", "Configs", "configs.config_list", 60)]),
        new("permissions", "Permissions", "key", [("permissions", "Permissions", "permissions.permissions_get", 61)]),
        new("commands", "Commands", "terminal", [("console", "Console", "commands.command_run", 70)]),
        new("mods", "Mods", "puzzle", [("mods", "Mods", "mods.mods", 80), ("crashes", "Crashes", "mods.crash_reports", 81)]),
    ];

    private static readonly List<KapOpRule> Table = [];

    private static void Add(string module, R role, bool write, params string[] ops) => Add(module, role, write, false, ops);

    private static void Add(string module, R role, bool write, bool sensitive, params string[] ops)
    {
        foreach (var op in ops) Table.Add(new KapOpRule(op, module, $"{module}.{op}", role, write, sensitive));
    }

    static KapPolicy()
    {
        // server: any signed-in user may ping; the rest by role
        Table.Add(new KapOpRule("ping", "server", null, R.Viewer, false, false));
        Table.Add(new KapOpRule("status", "server", null, R.Viewer, false, false));
        Add("server", R.Viewer, false, "admin_info", "world_status", "gameplay_status");
        Add("server", R.Admin, false, "log_tail", "native_status", "network_status", "events_stats", "sync_status");
        Add("server", R.Admin, true, "network_reload");
        // replication diagnostics (Kiln.Replication, dev.31): counters and watches read; a capture writes a file under kiln/logs
        Add("server", R.Admin, false, "repl_stats", "repl_watch", "repl_component");
        Add("server", R.Admin, true, "repl_capture");
        Add("server", R.Admin, true, true, "server_restart", "server_restart_cancel");
        // players
        Add("players", R.Viewer, false, "players");
        Add("players", R.Moderator, false, "inventory", "world_status_player");
        Add("players", R.Moderator, true, "tell", "broadcast");
        // world
        Add("world", R.Viewer, false, "time_get");
        Add("world", R.Moderator, false, "enemies", "world_chests");
        Add("world", R.Moderator, true, "teleport", "teleport_to_player");
        Add("world", R.Admin, true, "time_set", "weather_reset");
        Add("world", R.Admin, false, "settings_get");
        Add("world", R.Admin, true, true, "settings_set", "settings_save");
        // zones
        Add("zones", R.Viewer, false, "zones_list");
        Add("zones", R.Admin, true, "zones_add", "zones_remove");
        // items, spawn
        Add("items", R.Viewer, false, "item_search");
        Add("items", R.Admin, true, "give_item");
        Add("spawn", R.Admin, true, "spawn", "spawn_item", "despawn_spawned");
        // combat, rules
        Add("combat", R.Viewer, false, "health");
        Add("combat", R.Moderator, true, "heal");
        Add("combat", R.Admin, true, "god", "kill", "damage");
        Add("rules", R.Moderator, false, "rules");
        Add("rules", R.Admin, true, "creative", "creative_cycle", "creative_give");
        // creatures
        Add("creatures", R.Moderator, false, "creature_status", "creature_catalog", "creature_near", "creature_inspect", "creature_variants");
        Add("creatures", R.Admin, true, "creature_set", "creature_ai", "creature_reset", "creature_spawn", "creature_command");
        // content, game data
        Add("content", R.Moderator, false, "content_status", "content_get", "content_fields", "content_search", "content_entry", "content_recipe",
            "content_definitions", "content_report");
        Add("content", R.Admin, true, "content_set", "content_reset", "content_reload", "content_dump");
        Add("gamedata", R.Moderator, false, "gamedata_status", "gamedata_configs", "gamedata_definitions", "gamedata_search", "gamedata_get",
            "template_get", "arsenal_get", "sequence_get", "template_fields");
        Add("gamedata", R.Admin, true, "gamedata_set", "gamedata_reset", "gamedata_dump", "template_set", "template_reset", "arsenal_set",
            "arsenal_reset", "sequence_set", "sequence_reset");
        // world editing
        Add("build", R.Moderator, false, "build_status", "build_bases", "build_regions", "build_props", "build_schematics", "build_history",
            "build_placeonce_list", "build_read");
        Add("build", R.Admin, true, "build_release", "build_shape", "build_prop_place", "build_prop_remove", "build_capture", "build_paste",
            "build_undo", "build_placeonce_forget");
        // progression
        Add("progression", R.Moderator, false, "players_progression", "players_progression_get", "progression_status", "skill_tree", "skill_nodes");
        Add("progression", R.Admin, true, "players_progression_set", "give_xp", "give_skill_points", "skill_respec", "skill_lock", "skill_unlock");
        // knowledge and quests
        Add("knowledge", R.Moderator, false, "knowledge_get", "knowledge_list", "knowledge_npcs", "knowledge_resolve", "knowledge_status",
            "knowledge_security", "quests_list", "quest_state", "modquests_list", "modquests_status", "modquest_state");
        Add("knowledge", R.Admin, true, "knowledge_grant", "knowledge_revoke", "knowledge_set", "knowledge_security_set", "modquest_set",
            "modquest_signal", "modquests_reload");
        // saves
        Add("saves", R.Moderator, false, "save_status", "backup_list");
        Add("saves", R.Moderator, true, "save_now");
        Add("saves", R.Admin, true, "backup_create");
        Add("saves", R.Owner, true, true, "backup_restore", "backup_cancel_restore");
        // configs (mod configs include Kiln.Admin's own: KAP port, bridge folder) and in-game permissions
        Add("configs", R.Admin, false, "config_list", "config_get");
        Add("configs", R.Owner, true, true, "config_set", "config_reload");
        Add("permissions", R.Admin, false, "permissions_get", "permissions_check");
        Add("permissions", R.Owner, true, true, "permissions_player_node", "permissions_player_group", "permissions_group", "permissions_group_remove",
            "permissions_map_user_group", "permissions_reload");
        // commands: the level cap comes from the role (KapOps.Stamp); the permission says whether at all
        Add("commands", R.Moderator, false, "commands", "commands_describe", "command_complete");
        Add("commands", R.Moderator, true, "command_run");
        // mods: loading code and staging updates is the owner's
        Add("mods", R.Viewer, false, "mods");
        Add("mods", R.Admin, false, "crash_reports", "update_list");
        Add("mods", R.Owner, true, true, "mod_load", "mod_reload", "mod_unload", "mod_test", "update_stage", "update_cancel", "update_rollback");
        ByOp = Table.ToDictionary(r => r.Op, StringComparer.Ordinal);
        if (ByOp.Keys.Any(op => KapOps.Never.Contains(op) || KapOps.PanelOnly.Contains(op)))
            throw new InvalidOperationException("a blocked op is in the KAP policy table");
    }

    public static readonly IReadOnlyDictionary<string, KapOpRule> ByOp;

    /// <summary>Topics and their permissions.</summary>
    public static readonly IReadOnlyDictionary<string, string> TopicPermission = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["state"] = "server.state",
        ["metrics"] = "server.metrics",
        ["log"] = "server.log",
    };

    /// <summary>The explicit rule, or null for unknown or blocked operations.</summary>
    public static KapOpRule? Rule(string op)
    {
        if (KapOps.Never.Contains(op) || KapOps.PanelOnly.Contains(op) || !KapOps.IsOpName(op)) return null;
        return ByOp.GetValueOrDefault(op);
    }

    /// <summary>The permission specs each KAP module declares (registered by the auth component).</summary>
    public static IReadOnlyList<PermissionSpec> PermissionsOf(string module)
    {
        var list = Table.Where(r => r.Module == module && r.Permission is not null)
            .Select(r => new PermissionSpec(r.Permission!, Title(r.Op)) { DefaultRole = r.DefaultRole, Sensitive = r.Sensitive })
            .ToList();
        switch (module)
        {
            case "server":
                list.Add(new PermissionSpec("server.state", "Live server state (players, clock, mods)") { DefaultRole = R.Viewer });
                list.Add(new PermissionSpec("server.metrics", "Live metrics (tick time, memory, CPU)") { DefaultRole = R.Viewer });
                list.Add(new PermissionSpec("server.log", "The live kiln.log") { DefaultRole = R.Admin });
                break;
            case "commands":
                list.Add(new PermissionSpec(RunAs, "Run a command as a chosen player") { DefaultRole = R.Admin, Sensitive = true });
                break;
        }
        return list;
    }

    private static string Title(string op) => char.ToUpperInvariant(op[0]) + op[1..].Replace('_', ' ');
}
