// SPDX-License-Identifier: MIT
using System.Text.RegularExpressions;

namespace Kilnwarden.Routing;

public enum AccessKind
{
    /// <summary>Anyone, signed in or not (sign-in pages, the public page, health).</summary>
    Public,

    /// <summary>Any signed-in user.</summary>
    SignedIn,

    /// <summary>A signed-in user whose roles grant <see cref="RouteAccess.Permission"/>.</summary>
    Permission,
}

/// <summary>Who may call a route. Every route states one; the gate (<see cref="Security.IAccessGate"/>) checks it on the
/// server before the handler runs, for HTTP and WebSocket routes alike.</summary>
public readonly partial record struct RouteAccess
{
    public AccessKind Kind { get; }
    public string? Permission { get; }

    private RouteAccess(AccessKind kind, string? permission)
    {
        Kind = kind;
        Permission = permission;
    }

    public static RouteAccess Public { get; } = new(AccessKind.Public, null);
    public static RouteAccess SignedIn { get; } = new(AccessKind.SignedIn, null);

    /// <summary>Needs the permission (dotted lower-case: "players.kill", "core.settings.edit").</summary>
    public static RouteAccess Requires(string permission)
    {
        if (!IsPermissionName(permission)) throw new ArgumentException($"bad permission name '{permission}'");
        return new(AccessKind.Permission, permission);
    }

    /// <summary>Permission names: dot-separated segments of [a-z0-9_-] ("players.kill"). Grants may end in ".*"; names
    /// that routes require may not.</summary>
    public static bool IsPermissionName(string? s) => s is { Length: > 0 and <= 128 } && NameRe().IsMatch(s);

    public override string ToString() => Kind == AccessKind.Permission ? $"permission {Permission}" : Kind.ToString();

    [GeneratedRegex(@"^[a-z0-9_-]+(\.[a-z0-9_-]+)*$")]
    private static partial Regex NameRe();
}
