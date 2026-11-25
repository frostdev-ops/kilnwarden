// SPDX-License-Identifier: MIT
using Kilnwarden.Config;

namespace Kilnwarden.Hosting;

/// <summary>Whether the owner account exists (the auth lane provides it). Until it does, the server binds loopback
/// only, whatever settings.json says (DESIGN §2). <see cref="Changed"/> fires on any thread when the answer changes;
/// the host then re-evaluates its listeners.</summary>
public interface ISetupState
{
    bool OwnerExists { get; }

    /// <summary>The first-run setup can create the owner now (no owner yet, and the panel would serve the setup).</summary>
    bool SetupOpen => !OwnerExists;

    event Action? Changed;

    /// <summary>Whether the panel may be reached from other machines (a public bind, the relay, an ACME order): once
    /// the owner exists, or before that for the first-run setup when settings.json remoteSetup is on.</summary>
    static bool RemoteReachAllowed(ISetupState state, ServerSettings settings) =>
        state.OwnerExists || (settings.RemoteSetup && state.SetupOpen);
}

/// <summary>The default until an auth component provides one: no owner, so loopback only (and no setup to serve).</summary>
public sealed class NoOwnerYet : ISetupState
{
    public bool OwnerExists => false;
    public bool SetupOpen => false;
    public event Action? Changed { add { } remove { } }
}
