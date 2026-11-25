// SPDX-License-Identifier: MIT
using System.Net;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Extensions;
using Kilnwarden.Modules;
using Kilnwarden.Offline.Support;
using Kilnwarden.Security;

namespace Kilnwarden.Offline.Integration;

/// <summary>Step 2 integration: the real component list of the mod (WebAdminMod.Components(), lanes A, B and C together)
/// composes without collisions, keeps one audit sink, and exposes nothing new by default.</summary>
[Collection("lane-c")]
public sealed class ComposedGraphTests
{
    private static Task<AuthHarness> StartAsync() =>
        // the harness brings its own (fast-hashing) auth component; everything after it is the mod's own list
        AuthHarness.StartAsync(extra: WebAdminMod.Components().Where(c => c is not AuthComponent).ToArray());

    [Fact]
    public void The_mod_and_the_dev_host_start_the_same_list()
    {
        Assert.Equal(global::Kilnwarden.Hosting.FirstPartyComponents.All().Select(c => c.GetType()), WebAdminMod.Components().Select(c => c.GetType()));
    }

    [Fact]
    public void Auth_comes_first_and_every_lane_is_in_the_list()
    {
        var names = WebAdminMod.Components().Select(c => c.GetType().Name).ToArray();
        Assert.Equal("AuthComponent", names[0]);
        foreach (var n in new[] { "IdentityComponent", "AcmeComponent", "BrandingComponent", "OpsComponent", "ExtensionComponent", "MetricsComponent", "WebhooksComponent", "PublicPageComponent" })
            Assert.Contains(n, names);
        Assert.Equal(names.Length, names.Distinct().Count());
        Assert.True(Array.IndexOf(names, "IdentityComponent") > 0 && Array.IndexOf(names, "OpsComponent") > 0);
    }

    [Fact]
    public async Task Modules_routes_and_permissions_are_unique_and_reserved()
    {
        await using var h = await StartAsync();
        Assert.DoesNotContain(h.Log.Lines, l => l.StartsWith("E ", StringComparison.Ordinal));
        var reg = h.Host.Services.Get<ModuleRegistry>();
        var mods = reg.Modules;
        foreach (var id in new[] { "identity", "acme", "updates", "schedules", "saved-commands", "extensions", "metrics", "webhooks", "public" })
            Assert.Contains(mods, m => m.Spec.Id == id);
        // extensions can never take a first-party id (the registry would refuse a duplicate only while that module is up)
        Assert.All(mods, m => Assert.Contains(m.Spec.Id, ExtensionHost.ReservedIds));
        // one title per module in the module switches (lane B's "Mods" became "Mod updates")
        var titles = mods.GroupBy(m => m.Spec.Title).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        Assert.Empty(titles);
        // routes: one handler per method and path across every module
        var routes = mods.SelectMany(m => m.Routes.Select(r => (r.WebSocket is null ? r.Method : "WS") + " " + r.Pattern)).ToArray();
        Assert.Equal(routes.Length, routes.Distinct(StringComparer.Ordinal).Count());
        // every route sits under its own module's prefix
        Assert.All(mods, m => Assert.All(m.Routes, r => Assert.StartsWith(ModuleRegistry.ApiPrefix + m.Spec.Id, r.Pattern)));
        // permissions: unique names, each prefixed by its module (KAP modules use the module.op form)
        var perms = mods.SelectMany(m => m.Spec.Permissions.Select(p => (m.Spec.Id, p.Name))).ToArray();
        Assert.Equal(perms.Length, perms.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count());
        // page ids are unique per module, and the nav keys module:page are unique overall
        var keys = mods.SelectMany(m => m.Spec.Pages.Select(p => m.Spec.Id + ":" + p.Id)).ToArray();
        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task One_audit_sink_with_the_webhook_tap_attached()
    {
        await using var h = await StartAsync();
        var log = h.Host.Services.Get<AuditLog>();
        Assert.Same(log, h.Host.Services.Get<IAuditSink>());
        Assert.Same(log, h.Auth.Audit);
        Assert.Equal(1, log.TapCount);
    }

    [Fact]
    public async Task Nothing_new_is_enabled_or_exposed_by_default()
    {
        await using var h = await StartAsync();
        using var c = h.NewClient();
        // only local sign-in (Steam and Discord off; Discord also has no pinned broker key)
        var status = JsonNode.Parse(await (await c.GetAsync("/api/auth/status")).Content.ReadAsStringAsync())!;
        Assert.Equal("[\"local\"]", status["providers"]!.ToJsonString());
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync("/api/identity/start", new { provider = "steam", intent = "login" })).StatusCode);
        // the public page is off: its anonymous routes answer 404
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/public/status")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/public/page")).StatusCode);
        // every other new route needs a sign-in
        foreach (var path in new[] { "/api/acme/status", "/api/schedules", "/api/saved-commands", "/api/updates", "/api/extensions", "/api/metrics/live",
                     "/api/webhooks/hooks", "/api/identity/links", "/api/identity/status" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync(path)).StatusCode);
    }
}
