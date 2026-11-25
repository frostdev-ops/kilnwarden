// SPDX-License-Identifier: MIT
using System.Net;
using Kilnwarden.Auth;

namespace Kilnwarden.Offline.LaneC;

/// <summary>The `dotnet new kilnwarden-module` template's module (templates/kilnwarden-module/Panel.cs) registers
/// and works through the real extension host: its page, action, query and widget.</summary>
[Collection("lane-c")]
public sealed class TemplateTests
{
    [Fact]
    public async Task The_template_module_registers_and_runs()
    {
        await using var c = await LaneC.StartAsync();
        c.H.Host.Services.Add<Kilnwarden.Hosting.IGameThread>(new Inline()); // the template's action runs on the game thread
        using var ext = MyPanelMod.Panel.Register(c.ExtHost, "my-panel-mod");
        using var admin = await c.UserAsync("adam", BuiltinRoles.Admin);
        var page = await LaneC.JsonAsync(await admin.GetAsync("/api/mymodule/pages/overview"));
        Assert.NotNull(page);
        var r = await admin.PostAsync("/api/mymodule/actions/press", new { });
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var stats = await LaneC.JsonAsync(await admin.GetAsync("/api/mymodule/data/stats"));
        Assert.Equal(1, stats!["result"]!["presses"]!.GetValue<int>());
        var modules = await (await admin.GetAsync("/api/core/modules")).Content.ReadAsStringAsync();
        Assert.Contains("MyPanelMod presses", modules);
    }

    private sealed class Inline : Kilnwarden.Hosting.IGameThread
    {
        public bool IsGameThread => true;
        public Task<T> InvokeAsync<T>(Func<T> work, CancellationToken ct = default) => Task.FromResult(work());
    }
}
