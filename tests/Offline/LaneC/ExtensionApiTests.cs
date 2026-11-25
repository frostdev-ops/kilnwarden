// SPDX-License-Identifier: MIT
using System.Net;
using System.Text.Json.Nodes;
using Kilnwarden.Api;
using Kilnwarden.Auth;
using Kilnwarden.Extensions;
using Kilnwarden.Sample;

namespace Kilnwarden.Offline.LaneC;

[Collection("lane-c")]
public sealed class ExtensionApiTests
{
    private const string Mod = "webadmin-sample";

    [Fact]
    public async Task Sample_registers_and_its_routes_are_gated_and_audited()
    {
        await using var c = await LaneC.StartAsync();
        using var ext = SamplePanel.Register(c.ExtHost, Mod);
        using var owner = await c.UserAsync("olive", BuiltinRoles.Owner);
        using var viewer = await c.UserAsync("vic", BuiltinRoles.Viewer);

        // navigation: the owner sees the module, its two pages and its dashboard widget
        var mods = (JsonArray)(await LaneC.JsonAsync(await owner.GetAsync("/api/core/modules")))!;
        var sample = mods.Single(m => m!["id"]!.GetValue<string>() == "sample")!;
        Assert.Equal(["custom", "overview"], sample["pages"]!.AsArray().Select(p => p!["id"]!.GetValue<string>()).Order());
        Assert.Contains(sample["pages"]!.AsArray(), p => p!["kind"]!.GetValue<string>() == "custom" && p["entry"]!.GetValue<string>() == "/api/sample/frame/custom");
        Assert.Equal("/api/sample/data/stats", sample["widgets"]![0]!["data"]!.GetValue<string>());

        // a viewer may read but not greet
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/sample/data/stats")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync("/api/sample/actions/greet", new { name = "x" })).StatusCode);

        // the owner greets; arguments are checked against the schema
        var bad = await owner.PostAsync("/api/sample/actions/greet", new { name = 5 });
        Assert.Equal(HttpStatusCode.UnprocessableContent, bad.StatusCode);
        var ok = await owner.PostAsync("/api/sample/actions/greet", new { name = "Ann" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("Hello, Ann!", (await LaneC.JsonAsync(ok))!["result"]!["message"]!.GetValue<string>());
        var stats = await LaneC.JsonAsync(await owner.GetAsync("/api/sample/data/stats"));
        Assert.Equal(1, stats!["result"]!["visits"]!.GetValue<int>());

        // events: the viewer has sample.view, so it sees the topic
        var ev = await LaneC.JsonAsync(await viewer.GetAsync("/api/sample/events?after=0"));
        Assert.Equal("greeted", ev!["events"]![0]!["topic"]!.GetValue<string>());
        Assert.Equal("Ann", ev["events"]![0]!["payload"]!["name"]!.GetValue<string>());

        // a sensitive action needs recent proof
        var reset = await owner.PostAsync("/api/sample/actions/reset", null);
        Assert.Equal(HttpStatusCode.Forbidden, reset.StatusCode);
        Assert.Equal("reauth_required", await LaneC.CodeAsync(reset));
        await LaneC.ReauthAsync(owner);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync("/api/sample/actions/reset", null)).StatusCode);

        // every call is in the audit log under the user's name
        await c.H.Host.Services.Get<AuditLog>().StopAsync(CancellationToken.None);
        var audit = File.ReadAllText(Path.Combine(c.H.DataDir, AuditLog.File));
        Assert.Contains("\"op\":\"sample.greet\"", audit);
        Assert.Contains("\"op\":\"sample.reset\"", audit);
    }

    [Fact]
    public async Task Refusals_layouts_and_the_custom_page_sandbox()
    {
        await using var c = await LaneC.StartAsync();
        using var ext = SamplePanel.Register(c.ExtHost, Mod);
        using var mod = await c.UserAsync("mona", BuiltinRoles.Moderator);
        using var viewer = await c.UserAsync("vic", BuiltinRoles.Viewer);

        // the declarative layout leaves out blocks the user may not use
        var layout = await LaneC.JsonAsync(await viewer.GetAsync("/api/sample/pages/overview"));
        Assert.Equal(["stat", "text"], layout!["blocks"]!.AsArray().Select(b => b!["type"]!.GetValue<string>()).Order());
        var modLayout = await LaneC.JsonAsync(await mod.GetAsync("/api/sample/pages/overview"));
        Assert.Contains(modLayout!["blocks"]!.AsArray(), b => b!["type"]!.GetValue<string>() == "form" && b["schema"] is JsonObject);
        Assert.DoesNotContain(modLayout["blocks"]!.AsArray(), b => b!["type"]!.GetValue<string>() == "button"); // reset is admin

        // what the frame host may let the page name
        var d = await LaneC.JsonAsync(await viewer.GetAsync("/api/sample/describe"));
        Assert.Empty(d!["actions"]!.AsArray());
        Assert.Equal(["stats"], d["queries"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal(["greeted"], d["topics"]!.AsArray().Select(x => x!.GetValue<string>()));
        var dm = await LaneC.JsonAsync(await mod.GetAsync("/api/sample/describe"));
        Assert.Equal(["greet"], dm!["actions"]!.AsArray().Select(x => x!["name"]!.GetValue<string>()));

        // the custom page: sandboxed, no same-origin, no network, framed only by the panel
        var frame = await viewer.GetAsync("/api/sample/frame/custom");
        Assert.Equal(HttpStatusCode.OK, frame.StatusCode);
        var csp = string.Join(";", frame.Headers.GetValues("Content-Security-Policy"));
        Assert.StartsWith("sandbox allow-scripts;", csp);
        Assert.DoesNotContain("allow-same-origin", csp);
        Assert.Contains("connect-src 'none'", csp);
        Assert.Contains("frame-ancestors 'self'", csp);
        Assert.Equal("SAMEORIGIN", frame.Headers.GetValues("X-Frame-Options").Single());
        var html = await frame.Content.ReadAsStringAsync();
        Assert.Contains("window.kiln", html);
        Assert.Contains("kiln.query(\"stats\")", html);

        // a refusal from the handler reaches the caller with its status
        for (var i = 0; i < 100; i++) Assert.Equal(HttpStatusCode.OK, (await mod.PostAsync("/api/sample/actions/greet", new { name = "n" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await mod.PostAsync("/api/sample/actions/greet", new { name = "n" })).StatusCode);

        // signed out: nothing
        using var anon = c.H.NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/sample/frame/custom")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/sample/data/stats")).StatusCode);
    }

    [Fact]
    public async Task Settings_are_typed_validated_sensitive_and_hot()
    {
        await using var c = await LaneC.StartAsync();
        var admin = c.ExtHost;
        using var ext = admin.Attach("settings-test");
        using var m = ext.RegisterModule(new ModuleDefinition("cfgmod", "Cfg")
        {
            SettingsSchema = new JsonObject
            {
                ["type"] = "object", ["additionalProperties"] = false,
                ["properties"] = new JsonObject { ["greeting"] = new JsonObject { ["type"] = "string", ["maxLength"] = 10 }, ["maxVisits"] = new JsonObject { ["type"] = "integer" } },
            },
        });
        var s = m.Settings<SampleSettings>();
        Assert.Equal("Hello", s.Current.Greeting);
        var changed = new TaskCompletionSource<SampleSettings>();
        s.Changed += v => changed.TrySetResult(v);
        Assert.NotNull(c.H.Host.Modules.FindPermission("cfgmod.settings"));
        Assert.True(c.H.Host.Modules.FindPermission("cfgmod.settings")!.Sensitive);

        using var owner = await c.UserAsync("olive", BuiltinRoles.Owner);
        var r = await LaneC.PutAsync(owner, "/api/cfgmod/settings", new { greeting = "Hi" });
        Assert.Equal("reauth_required", await LaneC.CodeAsync(r));
        await LaneC.ReauthAsync(owner);
        Assert.Equal(HttpStatusCode.UnprocessableContent, (await LaneC.PutAsync(owner, "/api/cfgmod/settings", new { greeting = "far too long greeting" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableContent, (await LaneC.PutAsync(owner, "/api/cfgmod/settings", new { other = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LaneC.PutAsync(owner, "/api/cfgmod/settings", new { greeting = "Hi", maxVisits = 3 })).StatusCode);
        var v = await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Hi", v.Greeting);
        Assert.Equal(3, s.Current.MaxVisits);
        var got = await LaneC.JsonAsync(await owner.GetAsync("/api/cfgmod/settings"));
        Assert.Equal("Hi", got!["value"]!["greeting"]!.GetValue<string>());
        Assert.Equal("object", got["schema"]!["type"]!.GetValue<string>());

        // a viewer cannot even read them
        using var viewer = await c.UserAsync("vic", BuiltinRoles.Viewer);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/cfgmod/settings")).StatusCode);
    }

    [Fact]
    public async Task Definitions_are_checked()
    {
        await using var c = await LaneC.StartAsync();
        using var ext = c.ExtHost.Attach("checks");
        Assert.Throws<InvalidOperationException>(() => c.ExtHost.Attach("checks"));       // one handle per mod
        Assert.Throws<ArgumentException>(() => c.ExtHost.Attach("Bad Id"));
        Assert.Throws<InvalidOperationException>(() => ext.RegisterModule(new ModuleDefinition("metrics", "x")));   // reserved
        Assert.Throws<InvalidOperationException>(() => ext.RegisterModule(new ModuleDefinition("users", "x")));
        Assert.Throws<ArgumentException>(() => ext.RegisterModule(new ModuleDefinition("m1", "x") { Permissions = [new PermissionDefinition("other.x", "x")] }));
        Assert.Throws<ArgumentException>(() => ext.RegisterModule(new ModuleDefinition("m1", "x") { SettingsSchema = new JsonObject { ["type"] = "object", ["pattern"] = "a+" } }));
        var m = ext.RegisterModule(new ModuleDefinition("m1", "x") { Permissions = [new PermissionDefinition("m1.use", "use")] });
        // permissions of other modules (here a first-party one) cannot gate an extension's action
        Assert.Throws<ArgumentException>(() => m.AddAction(new ActionDefinition("a", "core.settings.edit"), _ => default));
        Assert.Throws<ArgumentException>(() => m.AddAction(new ActionDefinition("A B", "m1.use"), _ => default));
        m.AddAction(new ActionDefinition("a", "m1.use"), _ => default);
        Assert.Throws<InvalidOperationException>(() => m.AddAction(new ActionDefinition("a", "m1.use"), _ => default));
        Assert.Throws<ArgumentException>(() => m.AddWidget(new WidgetDefinition("w", "w", WidgetSurface.Dashboard) { Query = "missing" }));
        Assert.Throws<ArgumentException>(() => m.AddWidget(new WidgetDefinition("w", "w", WidgetSurface.Public)));
        Assert.Throws<ArgumentException>(() => m.AddPage(new CustomPage("p", "p", new string('x', ExtensionModule.MaxScriptChars + 1))));
        Assert.Throws<ArgumentException>(() => m.Publish("nope", null));
        Assert.ThrowsAny<ArgumentException>(() => new WebAdminRefusal(500, "x", "y"));
    }

    [Fact]
    public async Task Disposing_the_handle_removes_everything()
    {
        await using var c = await LaneC.StartAsync();
        var ext = SamplePanel.Register(c.ExtHost, Mod);
        using var owner = await c.UserAsync("olive", BuiltinRoles.Owner);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/sample/data/stats")).StatusCode);
        var ext2 = await LaneC.JsonAsync(await owner.GetAsync("/api/extensions"));
        Assert.Equal(Mod, ext2!["mods"]![0]!["mod"]!.GetValue<string>());
        Assert.NotNull(c.H.Host.Services.Get<Events.PanelEvents>().Find("sample.greeted"));

        ext.Dispose();
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync("/api/sample/data/stats")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync("/api/sample/frame/custom")).StatusCode);
        Assert.DoesNotContain(c.H.Host.Modules.Modules, m => m.Spec.Id == "sample");
        Assert.Null(c.H.Host.Modules.FindPermission("sample.view"));
        Assert.Null(c.H.Host.Services.Get<Events.PanelEvents>().Find("sample.greeted"));
        Assert.Empty(c.ExtHost.Attached);
        // and the mod can come back (a reload)
        using var again = SamplePanel.Register(c.ExtHost, Mod);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/sample/data/stats")).StatusCode);
    }

    [Fact]
    public async Task WhenAvailable_runs_at_start_and_the_stop_detaches()
    {
        IWebAdmin? seen = null;
        IWebAdminExtension? handle = null;
        using var wait = WebPanel.WhenAvailable(a =>
        {
            seen = a;
            handle = a.Attach("late-mod");
        });
        var c = await LaneC.StartAsync();
        try
        {
            Assert.Same(c.ExtHost, seen);
            Assert.True(WebPanel.TryGet(out var now));
            Assert.Same(c.ExtHost, now);
            Assert.Single(c.ExtHost.Attached);
        }
        finally
        {
            await c.DisposeAsync();
        }
        Assert.False(WebPanel.TryGet(out _));
        Assert.True(((ExtensionHandle)handle!).IsDisposed);
    }
}
