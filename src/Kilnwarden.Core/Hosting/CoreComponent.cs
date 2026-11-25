// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;
using Kilnwarden.Config;
using Kilnwarden.Http;
using Kilnwarden.Modules;
using Kilnwarden.Routing;
using Kilnwarden.Security;

namespace Kilnwarden.Hosting;

/// <summary>The core's own module ("core"): health, server info, the module list and switches, settings. Always first,
/// cannot be switched off. Also the example of a component: other lanes' components look like this.</summary>
internal sealed class CoreComponent : IWebAdminComponent
{
    public const string ModuleId = "core";
    public const string SettingsView = "core.settings.view";
    public const string SettingsEdit = "core.settings.edit";
    public const string ModulesEdit = "core.modules.edit";

    public string Name => "core";

    public void Configure(WebAdminBuilder b)
    {
        var m = b.Modules.Register(new ModuleSpec
        {
            Id = ModuleId,
            Title = "Panel",
            Icon = "settings",
            CanDisable = false,
            Permissions =
            [
                new PermissionSpec(SettingsView, "See the panel's settings") { DefaultRole = BuiltinRole.Admin },
                new PermissionSpec(SettingsEdit, "Change the panel's settings (bind, TLS, limits)") { DefaultRole = BuiltinRole.Owner, Sensitive = true },
                new PermissionSpec(ModulesEdit, "Switch modules on and off") { DefaultRole = BuiltinRole.Owner },
            ],
            Pages = [new PageSpec("settings", "Panel settings", PageKind.Builtin, "core/settings") { Icon = "settings", Permission = SettingsView, Order = 900 }],
        }, Product.ModId);

        m.MapGet("/health", RouteAccess.Public, _ => ValueTask.FromResult(HttpResponse.Json(new { ok = true })));
        m.MapGet("/modules", RouteAccess.SignedIn, ctx => ValueTask.FromResult(HttpResponse.Json(b.Modules.Describe(ctx.User))));
        m.MapPut("/modules/{id}", RouteAccess.Requires(ModulesEdit), ctx =>
        {
            var id = ctx.RouteValue("id");
            var body = ctx.ReadJson<JsonObject>();
            if (body["enabled"] is not JsonValue v || !v.TryGetValue<bool>(out var on)) throw new HttpError(400, "body: {\"enabled\": true|false}");
            try
            {
                b.Modules.SetEnabled(id, on);
            }
            catch (KeyNotFoundException)
            {
                throw new HttpError(404, $"no module '{id}'");
            }
            catch (InvalidOperationException e)
            {
                throw new HttpError(409, e.Message);
            }
            Audit(ctx, "core.modules.edit", new JsonObject { ["id"] = id, ["enabled"] = on });
            return ValueTask.FromResult(HttpResponse.Json(new { ok = true }));
        });
        // every module with its switch (Panel settings > Modules), switched-off ones too
        m.MapGet("/modules/switches", RouteAccess.Requires(SettingsView), _ => ValueTask.FromResult(HttpResponse.Json(new JsonObject
        {
            ["modules"] = new JsonArray([.. b.Modules.Modules.Select(x => (JsonNode)new JsonObject
            {
                ["id"] = x.Spec.Id, ["title"] = x.Spec.Title, ["description"] = x.Spec.Description, ["icon"] = x.Spec.Icon,
                ["enabled"] = x.Enabled, ["canDisable"] = x.Spec.CanDisable, ["owner"] = x.Owner,
            })]),
        })));
        m.MapGet("/info", RouteAccess.SignedIn, ctx => ValueTask.FromResult(HttpResponse.Json(Info(b, ctx))));
        var layouts = new DashboardLayouts(b.Data);
        b.Services.Add(layouts);
        DashboardLayouts.Map(m, layouts);
        // the components' own settings files (Panel settings): read with core.settings.view, saved with core.settings.edit
        // (owner by default, sensitive: recent proof), validated by each file's own rules, audited
        m.MapGet("/settings/files", RouteAccess.Requires(SettingsView), _ => ValueTask.FromResult(HttpResponse.Json(new JsonObject
        {
            ["files"] = new JsonArray([.. EditableSettings.Of(b.Services).All.Select(f => (JsonNode)new JsonObject
            {
                ["id"] = f.Id, ["title"] = f.Title, ["file"] = f.File, ["value"] = f.Value,
                ["problems"] = new JsonArray([.. f.Problems.Select(p => (JsonNode)p)]),
            })]),
        })));
        m.MapPut("/settings/files/{id}", RouteAccess.Requires(SettingsEdit), ctx =>
        {
            var f = EditableSettings.Of(b.Services).Find(ctx.RouteValue("id")) ?? throw new HttpError(404, "no such settings file");
            var body = ctx.ReadJson<JsonObject>();
            try
            {
                f.Save(body);
            }
            catch (ArgumentException e)
            {
                throw new HttpError(422, e.Message);
            }
            Audit(ctx, "core.settings.edit", new JsonObject { ["file"] = f.File });
            return ValueTask.FromResult(HttpResponse.Json(new { ok = true }));
        });
        m.MapGet("/settings", RouteAccess.Requires(SettingsView), _ =>
            ValueTask.FromResult(HttpResponse.Json(new { settings = b.Settings.Current, problems = b.Settings.Problems })));
        m.MapPut("/settings", RouteAccess.Requires(SettingsEdit), ctx =>
        {
            var next = ctx.ReadJson<ServerSettings>();
            try
            {
                b.Settings.Save(next);
            }
            catch (ArgumentException e)
            {
                throw new HttpError(422, e.Message);
            }
            Audit(ctx, "core.settings.edit", null);
            return ValueTask.FromResult(HttpResponse.Json(new { ok = true }));
        });
    }

    private static JsonObject Info(WebAdminBuilder b, RequestContext ctx)
    {
        var s = b.Server;
        var c = s.Certificate;
        var t = ctx.Connection.Tls;
        return new JsonObject
        {
            ["product"] = Product.Name,
            ["version"] = Product.Version,
            ["kiln"] = b.Environment.KilnVersion,
            ["endpoints"] = new JsonArray([.. s.Endpoints.Select(e => (JsonNode)e.ToString())]),
            ["tls"] = new JsonObject
            {
                ["engine"] = s.TlsEngine,
                ["certificate"] = c is null ? null : new JsonObject
                {
                    ["sha256"] = c.Sha256, ["subject"] = c.Subject, ["names"] = new JsonArray([.. c.Names.Select(n => (JsonNode)n)]),
                    ["notAfter"] = c.NotAfter.ToString("O"), ["keyType"] = c.KeyType, ["source"] = c.Source, ["chain"] = c.ChainLength,
                },
            },
            ["connection"] = new JsonObject
            {
                ["protocol"] = t?.Protocol, ["cipherSuite"] = t?.CipherSuite, ["alpn"] = t?.Alpn, ["client"] = ctx.ClientAddress.ToString(),
            },
        };
    }

    private static void Audit(RequestContext ctx, string op, JsonObject? args) =>
        ctx.Services.TryGet<IAuditSink>()?.Record(new AuditEntry(DateTimeOffset.UtcNow, ctx.User?.Id ?? "", ctx.User?.Name ?? "", op, args, true, null, 0,
            ctx.ClientAddress));
}
