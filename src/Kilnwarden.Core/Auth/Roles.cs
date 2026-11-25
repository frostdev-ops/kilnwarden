// SPDX-License-Identifier: MIT
using System.Text.Json;
using System.Text.RegularExpressions;
using Kilnwarden.Kap;
using Kilnwarden.Modules;
using Kilnwarden.Routing;
using Kilnwarden.Storage;

namespace Kilnwarden.Auth;

public static class BuiltinRoles
{
    public const string Owner = "owner", Admin = "admin", Moderator = "moderator", Viewer = "viewer";

    public static readonly IReadOnlyList<string> All = [Owner, Admin, Moderator, Viewer];

    /// <summary>The built-in rank of a role id (owner 3 ... viewer 0), null for custom roles.</summary>
    public static BuiltinRole? Rank(string id) => id switch
    {
        Owner => BuiltinRole.Owner,
        Admin => BuiltinRole.Admin,
        Moderator => BuiltinRole.Moderator,
        Viewer => BuiltinRole.Viewer,
        _ => null,
    };

    /// <summary>The highest command level a role may be given (DESIGN §5a: console is the owner's only; a moderator never
    /// reaches admin). Custom roles may go up to admin.</summary>
    public static CommandLevel MaxLevel(string id) => id switch
    {
        Owner => CommandLevel.Console,
        Admin => CommandLevel.Admin,
        Moderator or Viewer => CommandLevel.Everyone,
        _ => CommandLevel.Admin,
    };

    public static CommandLevel DefaultLevel(string id) => id switch
    {
        Owner => CommandLevel.Console,
        Admin => CommandLevel.Admin,
        Moderator => CommandLevel.Everyone,
        _ => CommandLevel.None,
    };
}

public static partial class RoleNames
{
    /// <summary>Role ids: 1 to 32 of a-z 0-9 _ -, starting with a letter.</summary>
    public static bool IsValid(string? id) => id is not null && IdRe().IsMatch(id);

    [GeneratedRegex("^[a-z][a-z0-9_-]{0,31}$")]
    private static partial Regex IdRe();
}

/// <summary>A role (DESIGN §3, §5a). Built-in roles have every permission whose <see cref="PermissionSpec.DefaultRole"/>
/// is at or below their rank, plus <see cref="Grants"/>, minus <see cref="Revokes"/>; custom roles have exactly their
/// grants. Grants and revokes are permission names or prefixes ending in ".*" ("spawn.*"), or "*" (custom roles may
/// not be given "*": the subset rules need it named). The owner role has everything and cannot be changed (except
/// <see cref="Require2fa"/>).</summary>
public sealed record RoleDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public IReadOnlyList<string> Grants { get; init; } = [];
    public IReadOnlyList<string> Revokes { get; init; } = [];

    /// <summary>The highest command level (KAP <c>level</c>) this role's lines run with.</summary>
    public CommandLevel CommandLevel { get; init; }

    /// <summary>Commands this role may not run: a command's name ("kick") or its node, with ".*" prefixes
    /// ("claims.command.*").</summary>
    public IReadOnlyList<string> CommandDeny { get; init; } = [];

    /// <summary>When set, the only commands this role may run (same patterns), each at most at <see cref="CommandLevel"/>.</summary>
    public IReadOnlyList<string>? CommandOnly { get; init; }

    /// <summary>Accounts with this role must use two-factor sign-in (the owner can require it per role).</summary>
    public bool Require2fa { get; init; }

    public bool IsBuiltin => BuiltinRoles.Rank(Id) is not null;
    public bool HasCommandRules => CommandDeny.Count > 0 || CommandOnly is not null;
}

/// <summary>Permission patterns: an exact name, a prefix ending in ".*" (matches that prefix's names at any depth), or "*".</summary>
public static class PermissionPattern
{
    public static bool IsValid(string? p, bool allowStar = true) =>
        p is not null && (p == "*" ? allowStar : p.EndsWith(".*", StringComparison.Ordinal) ? RouteAccess.IsPermissionName(p[..^2]) : RouteAccess.IsPermissionName(p));

    public static bool Matches(string pattern, string name) =>
        pattern == "*" || (pattern.EndsWith(".*", StringComparison.Ordinal)
            ? name.StartsWith(pattern.AsSpan(0, pattern.Length - 1), StringComparison.Ordinal)
            : string.Equals(pattern, name, StringComparison.Ordinal));

    public static bool Any(IEnumerable<string> patterns, string name)
    {
        foreach (var p in patterns)
            if (Matches(p, name)) return true;
        return false;
    }
}

/// <summary>roles.json: the built-in roles' changes and the custom roles. Reads are snapshots; changes are validated,
/// written atomically and bump <see cref="Version"/>.</summary>
public sealed class RoleStore
{
    public const string File = "roles.json";

    private readonly DataStore _data;
    private readonly object _lock = new();
    private IReadOnlyList<RoleDef> _all;
    private long _version;

    public RoleStore(DataStore data)
    {
        _data = data;
        RolesFile? f;
        try
        {
            f = data.ReadJson<RolesFile>(File);
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new InvalidDataException($"{File} is unreadable ({e.Message}): fix or restore it; the panel stays off until then", e);
        }
        if (f is not null && f.Format != 1) throw new InvalidDataException($"{File}: format {f.Format} is not supported");
        var stored = f?.Roles ?? [];
        foreach (var r in stored)
            if (Validate(r) is { } problem) throw new InvalidDataException($"{File}: {problem}; the panel stays off until it is fixed");
        if (stored.GroupBy(r => r.Id).Any(g => g.Count() > 1)) throw new InvalidDataException($"{File}: a role id twice");
        _all = Complete(stored);
    }

    public IReadOnlyList<RoleDef> All => Volatile.Read(ref _all);
    public long Version => Interlocked.Read(ref _version);
    public event Action? Changed;

    public RoleDef? Find(string? id) => id is null ? null : All.FirstOrDefault(r => r.Id == id);

    public static RoleDef Default(string id) => new()
    {
        Id = id,
        Name = id switch
        {
            BuiltinRoles.Owner => "Owner",
            BuiltinRoles.Admin => "Admin",
            BuiltinRoles.Moderator => "Moderator",
            BuiltinRoles.Viewer => "Viewer",
            _ => id,
        },
        CommandLevel = BuiltinRoles.DefaultLevel(id),
    };

    /// <summary>Adds or replaces a role. Throws ArgumentException with the problem when it is invalid.</summary>
    public RoleDef Put(RoleDef role)
    {
        if (Validate(role) is { } problem) throw new ArgumentException(problem);
        lock (_lock)
        {
            var list = All.Where(r => r.Id != role.Id).Append(role).ToList();
            Save(list);
        }
        return role;
    }

    /// <summary>Removes a custom role (the caller has taken it off every account).</summary>
    public bool Remove(string id)
    {
        if (BuiltinRoles.Rank(id) is not null) throw new ArgumentException("built-in roles cannot be removed");
        lock (_lock)
        {
            if (All.All(r => r.Id != id)) return false;
            Save([.. All.Where(r => r.Id != id)]);
        }
        return true;
    }

    private void Save(List<RoleDef> list)
    {
        _data.WriteJson(File, new RolesFile { Roles = list });
        Volatile.Write(ref _all, Complete(list));
        Interlocked.Increment(ref _version);
        try
        {
            Changed?.Invoke();
        }
        catch (Exception)
        {
            // a listener's bug never undoes a saved change
        }
    }

    /// <summary>The stored roles plus any built-in role missing from them, built-ins first (by rank, highest first).</summary>
    private static IReadOnlyList<RoleDef> Complete(IEnumerable<RoleDef> stored)
    {
        var byId = stored.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var builtins = BuiltinRoles.All.Select(id => byId.GetValueOrDefault(id) ?? Default(id));
        var custom = byId.Values.Where(r => !r.IsBuiltin).OrderBy(r => r.Id, StringComparer.Ordinal);
        return [.. builtins, .. custom];
    }

    /// <summary>What makes a role valid; null when it is.</summary>
    public static string? Validate(RoleDef r)
    {
        if (r is null) return "an empty role";
        if (!RoleNames.IsValid(r.Id)) return $"bad role id '{r.Id}' (a-z 0-9 _ -, at most 32, starting with a letter)";
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 64 || r.Name.Any(char.IsControl)) return $"role {r.Id}: a name of 1 to 64 printable characters";
        if (r.Grants is null || r.Revokes is null || r.CommandDeny is null) return $"role {r.Id}: missing lists";
        if (r.Grants.Count + r.Revokes.Count > 1000 || r.CommandDeny.Count + (r.CommandOnly?.Count ?? 0) > 1000) return $"role {r.Id}: too many entries";
        var custom = !r.IsBuiltin;
        foreach (var g in r.Grants)
            if (!PermissionPattern.IsValid(g, allowStar: false)) return $"role {r.Id}: bad grant '{g}'";
        foreach (var g in r.Revokes)
            if (!PermissionPattern.IsValid(g)) return $"role {r.Id}: bad revoke '{g}'";
        if (custom && r.Revokes.Count > 0) return $"role {r.Id}: only built-in roles have revokes (a custom role has exactly its grants)";
        foreach (var c in r.CommandDeny.Concat(r.CommandOnly ?? []))
            if (!CommandPattern.IsValid(c)) return $"role {r.Id}: bad command pattern '{c}'";
        if (!Enum.IsDefined(r.CommandLevel)) return $"role {r.Id}: bad command level";
        if (r.CommandLevel > BuiltinRoles.MaxLevel(r.Id))
            return $"role {r.Id}: command level {CommandLevels.Name(r.CommandLevel)} is above its limit {CommandLevels.Name(BuiltinRoles.MaxLevel(r.Id))}";
        if (r.Id == BuiltinRoles.Owner)
        {
            var d = Default(BuiltinRoles.Owner);
            if (r.Grants.Count > 0 || r.Revokes.Count > 0 || r.CommandLevel != d.CommandLevel || r.HasCommandRules)
                return "the owner role has every permission and console commands; only its two-factor rule can change";
        }
        return null;
    }

    private sealed class RolesFile
    {
        public int Format { get; set; } = 1;
        public List<RoleDef>? Roles { get; set; } = [];
    }
}

/// <summary>Command patterns for <see cref="RoleDef.CommandDeny"/>/<see cref="RoleDef.CommandOnly"/>: a command name
/// ("kick") or a permission node, optionally a ".*" prefix ("claims.command.*").</summary>
public static partial class CommandPattern
{
    public static bool IsValid(string? p) => p is { Length: > 0 and <= 128 } && PatternRe().IsMatch(p);

    /// <summary>Whether <paramref name="pattern"/> covers the command <paramref name="name"/> (or one of its aliases)
    /// with permission node <paramref name="node"/>.</summary>
    public static bool Matches(string pattern, string name, IEnumerable<string> aliases, string? node)
    {
        if (pattern.EndsWith(".*", StringComparison.Ordinal))
            return node is not null && node.StartsWith(pattern.AsSpan(0, pattern.Length - 1), StringComparison.Ordinal);
        if (pattern.Contains('.')) return string.Equals(pattern, node, StringComparison.Ordinal);
        return string.Equals(pattern, name, StringComparison.OrdinalIgnoreCase) || aliases.Any(a => string.Equals(pattern, a, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"^[a-z0-9_-]+(\.[a-z0-9_-]+)*(\.\*)?$")]
    private static partial Regex PatternRe();
}
