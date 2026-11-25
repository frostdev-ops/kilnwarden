// SPDX-License-Identifier: MIT
using Kilnwarden.Kap;
using Kilnwarden.Modules;
using Kilnwarden.Security;

namespace Kilnwarden.Auth;

/// <summary>Why a signed-in session may do nothing but fix its account first.</summary>
public enum Restriction
{
    None,

    /// <summary>The account was made or reset by someone else: change the password first.</summary>
    PasswordChange,

    /// <summary>A role requires two-factor and the account has none: set it up first.</summary>
    TwoFactorSetup,

    /// <summary>A role requires two-factor and this session signed in without it: sign in again.</summary>
    TwoFactorSignIn,
}

/// <summary>A signed-in user: the account, its roles and its session as they are for this request (made fresh for each
/// request and each WebSocket message, so a role change or a revoke applies at once). The owner has every permission;
/// everyone else has what their roles give (<see cref="RoleDef"/>). A restricted session has no permission at all.</summary>
public sealed class WebPrincipal : IPrincipal
{
    private readonly ModuleRegistry _registry;

    public WebPrincipal(Account account, IReadOnlyList<RoleDef> roles, SessionRecord session, ModuleRegistry registry)
    {
        Account = account;
        Roles = roles;
        Session = session;
        _registry = registry;
        Restriction = account.MustChangePassword ? Restriction.PasswordChange
            : roles.Any(r => r.Require2fa) && !account.TwoFactor ? Restriction.TwoFactorSetup
            : roles.Any(r => r.Require2fa) && !session.Mfa ? Restriction.TwoFactorSignIn
            : Restriction.None;
    }

    public Account Account { get; }
    public IReadOnlyList<RoleDef> Roles { get; }
    public SessionRecord Session { get; }
    public Restriction Restriction { get; }

    public string Id => Account.Id;
    public string Name => Account.Name;
    public bool IsOwner => Account.IsOwner;

    public bool Has(string permission) =>
        Restriction == Restriction.None && (IsOwner || Roles.Any(r => Grants(r, permission, _registry)));

    /// <summary>The highest command level of the user's roles (None when restricted).</summary>
    public CommandLevel CommandLevel => Restriction != Restriction.None || Roles.Count == 0 ? CommandLevel.None : Roles.Max(r => r.CommandLevel);

    /// <summary>The KAP caller for this user at <paramref name="level"/> (default: the user's highest).</summary>
    public KapCaller Caller(CommandLevel? level = null) => KapCaller.ForUser(Name, level ?? CommandLevel);

    /// <summary>Every declared permission this user has (the UI's view; the gate asks <see cref="Has"/>).</summary>
    public IReadOnlyList<string> Effective() => [.. _registry.Permissions.Select(p => p.Name).Where(Has).Order(StringComparer.Ordinal)];

    /// <summary>Whether role <paramref name="r"/> gives <paramref name="permission"/>.</summary>
    public static bool Grants(RoleDef r, string permission, ModuleRegistry registry)
    {
        if (r.Id == BuiltinRoles.Owner) return true;
        if (BuiltinRoles.Rank(r.Id) is { } rank)
        {
            var byDefault = registry.FindPermission(permission) is { } spec && spec.DefaultRole <= rank;
            return (byDefault || PermissionPattern.Any(r.Grants, permission)) && !PermissionPattern.Any(r.Revokes, permission);
        }
        return PermissionPattern.Any(r.Grants, permission);
    }

    /// <summary>The declared permissions role <paramref name="r"/> gives.</summary>
    public static IReadOnlySet<string> Effective(RoleDef r, ModuleRegistry registry) =>
        registry.Permissions.Select(p => p.Name).Where(p => Grants(r, p, registry)).ToHashSet(StringComparer.Ordinal);
}
