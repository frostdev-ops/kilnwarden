// SPDX-License-Identifier: MIT
namespace Kilnwarden.Hosting;

/// <summary>A part of the mod that plugs into the host: core (server, settings, static files), auth (accounts, sessions,
/// CSRF, the gate, audit, the KAP client), and later schedules, metrics, webhooks... The mod project lists them in order
/// (WebAdminMod.Components). Lifecycle, all off the tick thread:
/// <list type="number">
/// <item><see cref="Configure"/> in list order: register modules, routes, middleware, services, seams.</item>
/// <item><see cref="StartAsync"/> in list order, before the listeners accept.</item>
/// <item>At unload: listeners stop (WebSockets get 1001), then <see cref="StopAsync"/> in reverse order. The host waits
/// at most about 1 s for all of it, then cancels the token.</item>
/// </list>
/// A component that throws in Configure or StartAsync stops the start (the panel never serves half a gate).</summary>
public interface IWebAdminComponent
{
    string Name { get; }
    void Configure(WebAdminBuilder builder);
    Task StartAsync(WebAdminServices services, CancellationToken ct) => Task.CompletedTask;
    Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
