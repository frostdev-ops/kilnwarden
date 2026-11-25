using System.Text.Json.Nodes;
using Kilnwarden.Api;

namespace MyPanelMod;

/// <summary>Typed settings (kiln/data/kilnwarden/ext/mymodule.json; the panel's form comes from the schema below).</summary>
public sealed class Settings
{
    public string Message { get; set; } = "Hello from MyPanelMod";
}

/// <summary>The module "mymodule": a page of blocks, one action, one query, settings and a dashboard widget.</summary>
public static class Panel
{
    public const string ModuleId = "mymodule";

    public static IDisposable Start(string modId, Action<string>? log = null)
    {
        IWebAdminExtension? ext = null;
        var wait = WebPanel.WhenAvailable(admin =>
        {
            ext?.Dispose();
            ext = Register(admin, modId);
            log?.Invoke($"web panel module '{ModuleId}' registered (panel API {admin.ApiVersion})");
        });
        return new Stop(() =>
        {
            wait.Dispose();
            ext?.Dispose();
        });
    }

    public static IWebAdminExtension Register(IWebAdmin admin, string modId)
    {
        var ext = admin.Attach(modId);
        var count = 0;
        var m = ext.RegisterModule(new ModuleDefinition(ModuleId, "MyPanelMod")
        {
            Icon = "sparkles",
            Description = "What MyPanelMod adds to the panel",
            Permissions =
            [
                new PermissionDefinition($"{ModuleId}.view", "See MyPanelMod's page") { DefaultRole = WebAdminRole.Moderator },
                new PermissionDefinition($"{ModuleId}.press", "Press MyPanelMod's button") { DefaultRole = WebAdminRole.Admin },
            ],
            SettingsSchema = new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject { ["message"] = new JsonObject { ["type"] = "string", ["title"] = "Message", ["minLength"] = 1, ["maxLength"] = 80 } },
            },
        });
        var settings = m.Settings<Settings>();
        m.AddQuery(new QueryDefinition("stats", $"{ModuleId}.view"), _ =>
            ValueTask.FromResult<JsonNode?>(new JsonObject { ["presses"] = count, ["message"] = settings.Current.Message }));
        // RunOn.GameThread: the handler may use Kiln's game APIs (it runs in the server's tick)
        m.AddAction(new ActionDefinition("press", $"{ModuleId}.press") { Title = "Press the button", RunOn = RunOn.GameThread }, _ =>
            ValueTask.FromResult<JsonNode?>(new JsonObject { ["presses"] = Interlocked.Increment(ref count) }));
        m.AddPage(new DeclarativePage("overview", "MyPanelMod",
        [
            new TextBlock("This page is made of blocks: no JavaScript needed."),
            new StatBlock("Presses", "stats", "presses"),
            new ButtonBlock("press", "Press"),
        ]) { Permission = $"{ModuleId}.view" });
        m.AddWidget(new WidgetDefinition("presses", "MyPanelMod presses", WidgetSurface.Dashboard) { Query = "stats", Field = "presses" });
        return ext;
    }

    private sealed class Stop(Action stop) : IDisposable
    {
        private int _done;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0) stop();
        }
    }
}
