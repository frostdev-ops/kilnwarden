// SPDX-License-Identifier: MIT
namespace Kilnwarden.Hosting;

/// <summary>The panel's own components after core, in start order: the mod (WebAdminMod.Components()) and the dev host
/// (tools/devhost, the web app's end-to-end tests) start the same list. Auth first: it installs the audit log, the gate,
/// sessions and the KAP gateway every later component uses. The webhooks component taps the audit log itself, so the
/// rest of the order is not load-bearing for auditing.</summary>
public static class FirstPartyComponents
{
    public static IEnumerable<IWebAdminComponent> All() =>
    [
        new Auth.AuthComponent(),
        // step 2 lane A: provider sign-ins hook into auth's sign-in providers; ACME into the TLS seam
        new Identity.IdentityComponent(), new Tls.Acme.AcmeComponent(),
        new Branding.BrandingComponent(), new Static.MapAssetsComponent(),
        // step 2 lane B: schedules, saved commands, mod updates
        new Ops.OpsComponent(),
        // player activity: joins, leaves, deaths (the player.* events webhooks send); restarts, mod faults, crash
        // reports and failed saves (server.*, mod.fault, crash.report, save.failed; metrics' modFaults and saveAgeS)
        new Activity.ActivityComponent(), new Watch.ServerWatchComponent(),
        // step 2 lane C: extension API, metrics, webhooks, public page
        new Extensions.ExtensionComponent(), new Metrics.MetricsComponent(), new Webhooks.WebhooksComponent(), new PublicPage.PublicPageComponent(),
        new Relay.RelayComponent(),
        // 0.1.2: the setup wizard and the console's setup (it writes the relay's, ACME's and the server's settings)
        new Setup.SetupComponent(),
    ];
}
