// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Kilnwarden.Kap;
using Kilnwarden.Modules;

namespace Kilnwarden.Auth;

/// <summary>A gate decision for one KAP op or topic.</summary>
public readonly record struct KapDecision(bool Allowed, string Code, string Message, KapOpRule? Rule)
{
    public static KapDecision Deny(string code, string message, KapOpRule? rule = null) => new(false, code, message, rule);
}

/// <summary>The server-side check of every KAP op and topic a web user asks for (DESIGN §3: "the gate checks every KAP
/// op"): the op must be one the web may use at all (<see cref="KapPolicy.Rule"/>), its module must be on, the user must
/// have its permission; command ops need a command level, and a line run as another player needs
/// <see cref="KapPolicy.RunAs"/>.</summary>
public static class KapGate
{
    public static KapDecision CheckOp(WebPrincipal p, string op, JsonObject? args, ModuleRegistry registry, DateTimeOffset? now = null, bool requireRecent = true)
    {
        if (p.Restriction != Restriction.None) return KapDecision.Deny("forbidden", "finish your account setup first");
        if (KapPolicy.Rule(op) is not { } rule) return KapDecision.Deny("forbidden", $"{op} is not available in the web panel");
        if (!registry.IsEnabled(rule.Module)) return KapDecision.Deny("forbidden", $"the {rule.Module} module is switched off", rule);
        if (rule.Permission is { } perm && !p.Has(perm)) return KapDecision.Deny("forbidden", $"needs permission {perm}", rule);
        if (requireRecent && (rule.Sensitive || op == "command_run" && p.CommandLevel == CommandLevel.Console) &&
            (p.Session.PrivilegeStamp != p.Account.PrivilegeStamp || !p.Session.HasRecentAuthentication(now ?? DateTimeOffset.UtcNow)))
            return KapDecision.Deny("reauth_required", "re-authenticate with your password or enrolled two-factor code, then retry", rule);
        if (KapOps.CommandOps.Contains(op))
        {
            if (p.CommandLevel == CommandLevel.None) return KapDecision.Deny("forbidden", "your roles may not run commands", rule);
            if (args?["steamId"] is not null && !p.Has(KapPolicy.RunAs)) return KapDecision.Deny("forbidden", $"running a line as a player needs permission {KapPolicy.RunAs}", rule);
        }
        return new KapDecision(true, "", "", rule);
    }

    public static KapDecision CheckTopic(WebPrincipal p, string topic, ModuleRegistry registry)
    {
        if (p.Restriction != Restriction.None) return KapDecision.Deny("forbidden", "finish your account setup first");
        if (!KapOps.IsOpName(topic)) return KapDecision.Deny("unknown_topic", "no such topic");
        if (!KapPolicy.TopicPermission.TryGetValue(topic, out var perm)) return KapDecision.Deny("unknown_topic", "no such topic");
        if (!registry.IsEnabled("server")) return KapDecision.Deny("forbidden", "the server module is switched off");
        return p.Has(perm) ? new KapDecision(true, "", "", null) : KapDecision.Deny("forbidden", $"needs permission {perm}");
    }

    /// <summary>What ready.ops/topics show this user: Kiln.Admin's, minus what the gate would refuse.</summary>
    public static (List<string> Ops, List<string> Topics) Visible(WebPrincipal p, KapSession s, ModuleRegistry registry) =>
        ([.. s.Ops.Where(op => CheckOp(p, op, null, registry, requireRecent: false).Allowed).Order(StringComparer.Ordinal)],
         [.. s.Topics.Where(t => CheckTopic(p, t, registry).Allowed)]);
}

/// <summary>Per-command role rules (DESIGN §5a, <see cref="RoleDef.CommandDeny"/>/<see cref="RoleDef.CommandOnly"/>).
/// A line may run when one of the user's roles permits its command at the command's level (roles without rules permit
/// everything up to their level); it runs at the highest level among those roles. Kiln.Admin caps the line at that
/// level and refuses subcommands above it. The command's node and level come from commands_describe, asked as the
/// user (cached 30 s).</summary>
public sealed class CommandRules(KapClient kap, TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly ConcurrentDictionary<(string, CommandLevel), (DateTimeOffset At, JsonArray Catalog)> _cache = new();

    public sealed record Entry(string Name, IReadOnlyList<string> Aliases, string? Node, CommandLevel Level);

    /// <summary>The caller to run (or complete) <paramref name="line"/> with, or a refusal.</summary>
    public async Task<(KapCaller? Caller, string? Refusal)> ForLineAsync(WebPrincipal p, string? line, CancellationToken ct)
    {
        if (!p.Roles.Any(r => r.HasCommandRules)) return (p.Caller(), null);
        var name = FirstWord(line);
        if (name.Length == 0) return (p.Caller(), null);
        var entry = Find(await CatalogAsync(p, ct).ConfigureAwait(false), name);
        var permitted = p.Roles.Where(r => Permits(r, entry, name)).ToList();
        if (permitted.Count == 0) return (null, $"your roles may not run the command '{name}'");
        return (p.Caller(permitted.Max(r => r.CommandLevel)), null);
    }

    /// <summary>commands_describe's list without the commands the user's roles do not permit.</summary>
    public JsonNode? FilterDescribe(WebPrincipal p, JsonNode? data)
    {
        if (!p.Roles.Any(r => r.HasCommandRules) || data is not JsonArray arr) return data;
        var kept = new JsonArray();
        foreach (var n in arr)
            if (n is JsonObject o && Parse(o) is { } e && p.Roles.Any(r => Permits(r, e, e.Name))) kept.Add(o.DeepClone());
        return kept;
    }

    /// <summary>command_complete's suggestions for a command name, minus names the user's roles do not permit.</summary>
    public async Task<JsonNode?> FilterCompleteAsync(WebPrincipal p, string? text, JsonNode? data, CancellationToken ct)
    {
        if (!p.Roles.Any(r => r.HasCommandRules) || data is not JsonObject o || o["suggestions"] is not JsonArray sugg) return data;
        if (text is not null && text.TrimStart().TrimStart('/').Any(char.IsWhiteSpace)) return data; // arguments: the line's command was checked
        var catalog = await CatalogAsync(p, ct).ConfigureAwait(false);
        var copy = (JsonObject)o.DeepClone();
        var kept = new JsonArray();
        foreach (var s in sugg)
            if (s is JsonValue v && v.TryGetValue<string>(out var word) && p.Roles.Any(r => Permits(r, Find(catalog, FirstWord(word)), FirstWord(word))))
                kept.Add(word);
        copy["suggestions"] = kept;
        return copy;
    }

    public static bool Permits(RoleDef r, Entry? e, string name)
    {
        if (r.CommandLevel == CommandLevel.None) return false;
        var aliases = e?.Aliases ?? [];
        var cmdName = e?.Name ?? name;
        if (r.CommandDeny.Any(d => CommandPattern.Matches(d, cmdName, aliases, e?.Node))) return false;
        if (r.CommandOnly is { } only && !only.Any(d => CommandPattern.Matches(d, cmdName, aliases, e?.Node))) return false;
        // an unknown command (not in the user's catalog): Kiln.Admin answers it; a role with an allow-list never runs it
        return e is null ? r.CommandOnly is null : r.CommandLevel >= e.Level;
    }

    public static string FirstWord(string? line)
    {
        var s = (line ?? "").TrimStart();
        if (s.StartsWith('/')) s = s[1..];
        var end = 0;
        while (end < s.Length && !char.IsWhiteSpace(s[end])) end++;
        return s[..Math.Min(end, 64)].ToLowerInvariant();
    }

    private async Task<JsonArray> CatalogAsync(WebPrincipal p, CancellationToken ct)
    {
        var key = (p.Name, p.CommandLevel);
        var now = _time.GetUtcNow();
        if (_cache.TryGetValue(key, out var c) && now - c.At < TimeSpan.FromSeconds(30)) return c.Catalog;
        var r = await kap.CallAsync(p.Caller(), "commands_describe", null, ct).ConfigureAwait(false);
        var catalog = r.Ok && r.Data is JsonArray a ? (JsonArray)a.DeepClone() : [];
        if (_cache.Count > 1000) _cache.Clear();
        _cache[key] = (now, catalog);
        return catalog;
    }

    private static Entry? Find(JsonArray catalog, string name)
    {
        foreach (var n in catalog)
            if (n is JsonObject o && Parse(o) is { } e && (e.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || e.Aliases.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase))))
                return e;
        return null;
    }

    private static Entry? Parse(JsonObject o)
    {
        if (KapFrames.Str(o, "name") is not { } name) return null;
        var aliases = o["aliases"] is JsonArray aa ? aa.Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).OfType<string>().ToList() : [];
        // an unreadable level counts as the highest: never run something at a level below its own by mistake
        var level = CommandLevels.TryParse(KapFrames.Str(o, "permission"), out var l) && l != CommandLevel.None ? l : CommandLevel.Console;
        return new Entry(name, aliases, KapFrames.Str(o, "node"), level);
    }
}
