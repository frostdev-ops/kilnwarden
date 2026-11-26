// SPDX-License-Identifier: MIT
namespace Kilnwarden.Hosting;

/// <summary>The panel's address on the internet (https://&lt;name&gt;.kiln.nexus/ or an own address, with a trailing
/// slash) and the SHA-256 of the trusted certificate it serves there ("AB:CD:...", as <see cref="Tls.CertificateInfo"/>
/// writes it).</summary>
public sealed record PublicAddress(string Url, string Sha256);

/// <summary>Where the panel really works from the internet right now, for the Kiln Launcher's "Open web panel"
/// (launcher-endpoint.json <c>public</c>). The setup component provides it: null unless the owner exists, the launcher's
/// login is on (auth.json launcherLogin) and the wizard's "Ready" holds for the route the panel uses (hosted access: the
/// relay connected, the name claimed, the trusted certificate installed and served, the panel's own check of the address
/// passing for that certificate, recently enough for its schedule; an own address: the same for that name).
/// <see cref="Changed"/> fires on any thread when it changes.</summary>
public interface IPublicAddress
{
    PublicAddress? Public { get; }

    event Action? Changed;
}
