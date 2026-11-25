// SPDX-License-Identifier: MIT
namespace Kilnwarden.Security;

/// <summary>A signed-in user as the core sees it. The auth component makes these (accounts, roles, sessions) and owns
/// what <see cref="Has"/> means (role grants, wildcards such as "spawn.*", the owner having everything).</summary>
public interface IPrincipal
{
    /// <summary>The account's stable id.</summary>
    string Id { get; }

    /// <summary>The name to show and to write in the audit log.</summary>
    string Name { get; }

    bool Has(string permission);
}
