// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;
using Kilnwarden.Api;

namespace Kilnwarden.Sample;

/// <summary>Typed settings of the sample module (ext/sample.json; the panel's form comes from the schema below).</summary>
public sealed class SampleSettings
{
    public string Greeting { get; set; } = "Hello";
    public int MaxVisits { get; set; } = 100;
}

/// <summary>The sample's web panel module "sample". Only BCL and Kilnwarden.Api: no Kiln types, so it can also be
/// registered by tests without a game.</summary>
public static class SamplePanel
{
    public const string ModuleId = "sample";

    /// <summary>Waits for the panel (now, and after every panel start) and registers the module; dispose to stop.</summary>
    public static IDisposable Start(string modId, Action<string>? log = null)
    {
        IWebAdminExtension? ext = null;
        var wait = WebPanel.WhenAvailable(admin =>
        {
            ext?.Dispose();
            ext = Register(admin, modId);
            log?.Invoke($"web panel module '{ModuleId}' registered (panel {admin.Version}, API {admin.ApiVersion})");
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
        var visits = 0;
        var m = ext.RegisterModule(new ModuleDefinition(ModuleId, "Sample")
        {
            Icon = "sparkles",
            Description = "What a mod can add to the web panel",
            Permissions =
            [
                new PermissionDefinition("sample.view", "See the sample page") { DefaultRole = WebAdminRole.Viewer },
                new PermissionDefinition("sample.greet", "Greet") { DefaultRole = WebAdminRole.Moderator },
                new PermissionDefinition("sample.reset", "Reset the counter") { DefaultRole = WebAdminRole.Admin, Sensitive = true },
            ],
            Topics = [new TopicDefinition("greeted", "sample.view") { Title = "Someone was greeted", Webhooks = true, WebhookFields = ["name"] }],
            SettingsSchema = new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["greeting"] = new JsonObject { ["type"] = "string", ["title"] = "Greeting", ["minLength"] = 1, ["maxLength"] = 40 },
                    ["maxVisits"] = new JsonObject { ["type"] = "integer", ["title"] = "Most visits", ["minimum"] = 1, ["maximum"] = 10000 },
                },
            },
        });
        var settings = m.Settings<SampleSettings>();

        m.AddQuery(new QueryDefinition("stats", "sample.view"), _ =>
            ValueTask.FromResult<JsonNode?>(new JsonObject { ["visits"] = visits, ["greeting"] = settings.Current.Greeting }));
        m.AddAction(new ActionDefinition("greet", "sample.greet")
        {
            Title = "Greet someone",
            ArgsSchema = new JsonObject
            {
                ["type"] = "object", ["required"] = new JsonArray("name"),
                ["properties"] = new JsonObject { ["name"] = new JsonObject { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = 32 } },
            },
        }, ctx =>
        {
            if (Interlocked.Increment(ref visits) > settings.Current.MaxVisits) throw new WebAdminRefusal(409, "too_many", "the counter is full: reset it");
            var name = ctx.Args["name"]!.GetValue<string>();
            m.Publish("greeted", new JsonObject { ["name"] = name, ["by"] = ctx.User.Name });
            return ValueTask.FromResult<JsonNode?>(new JsonObject { ["message"] = $"{settings.Current.Greeting}, {name}!" });
        });
        m.AddAction(new ActionDefinition("reset", "sample.reset") { Title = "Reset the counter", Confirm = "Reset the visit counter?" }, _ =>
        {
            Interlocked.Exchange(ref visits, 0);
            return ValueTask.FromResult<JsonNode?>(null);
        });
        m.AddPage(new DeclarativePage("overview", "Sample",
        [
            new TextBlock("A page made of blocks: no JavaScript from the mod."),
            new StatBlock("Visits", "stats", "visits"),
            new FormBlock("greet") { Submit = "Greet" },
            new ButtonBlock("reset", "Reset"),
        ]) { Permission = "sample.view", Icon = "sparkles" });
        m.AddPage(new CustomPage("custom", "Sample (custom)", CustomScript) { Permission = "sample.view" });
        m.AddWidget(new WidgetDefinition("visits", "Sample visits", WidgetSurface.Dashboard) { Query = "stats", Field = "visits" });
        m.AddWidget(new WidgetDefinition("hello", "Hello", WidgetSurface.Public) { Kind = "text", PublicData = () => settings.Current.Greeting });
        return ext;
    }

    /// <summary>The custom page: plain DOM, the panel's bridge (window.kiln) for everything else.</summary>
    private const string CustomScript = """
const out = document.createElement("pre");
document.body.append(out);
const show = async () => { out.textContent = JSON.stringify(await kiln.query("stats"), null, 2); kiln.resize(document.body.scrollHeight); };
const b = document.createElement("button");
b.textContent = "Greet the panel";
b.onclick = async () => { await kiln.call("greet", { name: "panel" }); show(); };
document.body.prepend(b);
kiln.subscribe("greeted", show);
show();
""";

    private sealed class Stop(Action stop) : IDisposable
    {
        private int _done;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0) stop();
        }
    }
}
