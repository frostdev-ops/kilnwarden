// SPDX-License-Identifier: MIT
using Kilnwarden.Identity;
using Kilnwarden.Tls.Acme;

namespace Kilnwarden.Relay;

public sealed class RelaySettings
{
    public bool Enabled { get; set; }
    public string Origin { get; set; } = "https://frostdev.io";
    public string Host { get; set; } = "";

    public static IReadOnlyList<string> Validate(RelaySettings settings)
    {
        var errors = new List<string>();
        if (!IdentitySettings.IsBrokerOrigin(settings.Origin)) errors.Add("origin must be an HTTPS origin");
        if (settings.Enabled && (!AcmeSettings.IsDnsName(settings.Host) || settings.Host != settings.Host.ToLowerInvariant()))
            errors.Add("host must be the registered lower-case DNS name");
        return errors;
    }
}
