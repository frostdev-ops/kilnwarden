// SPDX-License-Identifier: MIT
using Kilnwarden.Identity;
using Kilnwarden.Tls.Acme;

namespace Kilnwarden.Relay;

public sealed class RelaySettings
{
    public bool Enabled { get; set; }
    public string Origin { get; set; } = "https://frostdev.io";
    public string Host { get; set; } = "";

    /// <summary>The zone the relay gives panel names under (frostdev.io's relay: kiln.nexus). The setup wizard and
    /// <c>kilnwarden setup hosted &lt;name&gt;</c> make <see cref="Host"/> from a name and this (0.1.2).</summary>
    public string Zone { get; set; } = Setup.PanelNames.Zone;

    public static IReadOnlyList<string> Validate(RelaySettings settings)
    {
        var errors = new List<string>();
        if (!IdentitySettings.IsBrokerOrigin(settings.Origin)) errors.Add("origin must be an HTTPS origin");
        if (settings.Enabled && (!AcmeSettings.IsDnsName(settings.Host) || settings.Host != settings.Host.ToLowerInvariant()))
            errors.Add("host must be the registered lower-case DNS name");
        if (!AcmeSettings.IsDnsName(settings.Zone) || settings.Zone != settings.Zone.ToLowerInvariant())
            errors.Add("zone must be a lower-case DNS name (kiln.nexus)");
        return errors;
    }
}
