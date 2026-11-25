// SPDX-License-Identifier: MIT
namespace Kilnwarden.Auth;

/// <summary>The first-run setup for the console's <c>kilnwarden setup</c> (Hosting/ConsoleCommands): a new setup link
/// while no owner exists.</summary>
public sealed class FirstRunSetup
{
    private readonly AuthComponent _auth;

    internal FirstRunSetup(AuthComponent auth) => _auth = auth;

    public bool OwnerExists => _auth.OwnerExists;

    /// <summary>A new one-use setup link (an hour), null once the owner exists; <paramref name="where"/>: where it works.</summary>
    public string? NewLink(out string where) => _auth.NewConsoleSetupLink(out where);
}
