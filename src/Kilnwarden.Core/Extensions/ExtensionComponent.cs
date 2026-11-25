// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;
using Kilnwarden.Api;
using WebAdminApi = Kilnwarden.Api.WebPanel;
using Kilnwarden.Events;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Modules;
using Kilnwarden.Routing;

namespace Kilnwarden.Extensions;

/// <summary>The public extension API as a host component (lane C): makes the &lt;see cref="ExtensionHost"/&gt;, publishes it
/// through &lt;see cref="WebPanel"/&gt; once the panel has started its components, and detaches every extension at stop
/// (before the listeners' services go away). Module "extensions": GET /api/extensions (extensions.view).</summary>
public sealed class ExtensionComponent : IWebAdminComponent
{
    public const string ModuleId = "extensions";
    public const string ViewPermission = "extensions.view";

    private ExtensionHost? _host;
    private IWebAdminLog? _log;

    public string Name => "extensions";

    /// <summary>The host (after Configure).</summary>
    public ExtensionHost? Host => _host;

    public void Configure(WebAdminBuilder b)
    {
        _log = b.Log;
        var events = PanelEvents.Of(b.Services, b.Log);
        _host = new ExtensionHost(b.Modules, b.Data, b.Services, b.Log, b.Environment.Time, events);
        b.Services.Add(_host);
        var m = b.Modules.Register(new ModuleSpec
        {
            Id = ModuleId, Title = "Extensions", Icon = "puzzle", CanDisable = false,
            Description = "Pages, actions and widgets other mods add through Kilnwarden.Api",
            Permissions = [new PermissionSpec(ViewPermission, "See which mods extend the panel") { DefaultRole = BuiltinRole.Admin }],
        }, Product.ModId);
        m.MapGet("", RouteAccess.Requires(ViewPermission), _ => ValueTask.FromResult(HttpResponse.Json(Describe())));
    }

    private JsonObject Describe()
    {
        var mods = new JsonArray();
        foreach (var h in _host!.Attached)
            mods.Add(new JsonObject
            {
                ["mod"] = h.ModId,
                ["modules"] = new JsonArray([.. h.Modules.Select(m => (JsonNode)new JsonObject { ["id"] = m.Id, ["enabled"] = m.Enabled })]),
            });
        return new JsonObject { ["apiVersion"] = WebAdminApi.ApiVersion, ["mods"] = mods };
    }

    public Task StartAsync(WebAdminServices services, CancellationToken ct)
    {
        WebAdminApi.Publish(_host!, e => _log!.Error("an extension's WebPanel.WhenAvailable callback failed", e));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        if (_host is null) return Task.CompletedTask;
        WebAdminApi.Retract(_host);
        _host.DetachAll();
        return Task.CompletedTask;
    }
}
