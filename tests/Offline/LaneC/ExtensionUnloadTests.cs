// SPDX-License-Identifier: MIT
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Kilnwarden.Api;
using Kilnwarden.Auth;
using Kilnwarden.Offline.Support;

namespace Kilnwarden.Offline.LaneC;

/// <summary>A mod that unloads (Kiln unloads a mod's collectible load context) leaves nothing behind in the panel, even
/// when it never disposed its handle, and the panel holds nothing that keeps its load context alive.</summary>
[Collection("lane-c")]
public sealed class ExtensionUnloadTests
{
    /// <summary>The sample's assembly in its own collectible context; Kilnwarden.Api comes from the default one (as
    /// Kiln shares an export).</summary>
    private sealed class ModContext(string dir) : AssemblyLoadContext("webadmin-sample-test", isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName name) =>
            name.Name == typeof(IWebAdmin).Assembly.GetName().Name ? null
            : File.Exists(Path.Combine(dir, name.Name + ".dll")) && name.Name!.StartsWith("Kilnwarden.Sample", StringComparison.Ordinal)
                ? LoadFromAssemblyPath(Path.Combine(dir, name.Name + ".dll")) : null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference LoadRegisterAndUnload(IWebAdmin admin, bool dispose)
    {
        var dir = Path.GetDirectoryName(typeof(Kilnwarden.Sample.SamplePanel).Assembly.Location)!;
        var alc = new ModContext(dir);
        var asm = alc.LoadFromAssemblyPath(Path.Combine(dir, "Kilnwarden.Sample.dll"));
        Assert.NotSame(typeof(Kilnwarden.Sample.SamplePanel).Assembly, asm);
        var start = asm.GetType("Kilnwarden.Sample.SamplePanel")!.GetMethod("Start")!;
        var handle = (IDisposable)start.Invoke(null, ["webadmin-sample", null])!;
        Assert.Contains(((Extensions.ExtensionHost)admin).Attached, h => h.ModId == "webadmin-sample");
        if (dispose) handle.Dispose();
        alc.Unload();
        return new WeakReference(alc);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (IDisposable Handle, WeakReference Context) LoadSample(string name)
    {
        var dir = Path.GetDirectoryName(typeof(Kilnwarden.Sample.SamplePanel).Assembly.Location)!;
        var alc = new ModContext(dir);
        var asm = alc.LoadFromAssemblyPath(Path.Combine(dir, "Kilnwarden.Sample.dll"));
        var start = asm.GetType("Kilnwarden.Sample.SamplePanel")!.GetMethod("Start")!;
        var handle = (IDisposable)start.Invoke(null, [name, null])!;
        return (new UnloadOnDispose(handle, alc), new WeakReference(alc));
    }

    private sealed class UnloadOnDispose(IDisposable handle, AssemblyLoadContext alc) : IDisposable
    {
        public void Dispose()
        {
            handle.Dispose();
            alc.Unload();
        }
    }

    /// <summary>Regression (step 2 lab): settings the panel saved survive a hot reload of the mod. The new instance (a new
    /// collectible context, a new SampleSettings type) reads the same ext/sample.json and must accept it, not fall back
    /// to the defaults.</summary>
    [Fact]
    public async Task Saved_settings_survive_a_hot_reload()
    {
        await using var c = await LaneC.StartAsync();
        var (first, _) = LoadSample("webadmin-sample");
        using var owner = await c.UserAsync("olive", BuiltinRoles.Owner);
        await LaneC.ReauthAsync(owner);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await LaneC.PutAsync(owner, "/api/sample/settings", new { greeting = "Hi", maxVisits = 3 })).StatusCode);
        var saved = c.H.Host.Data.ReadJson<System.Text.Json.Nodes.JsonObject>("ext/sample.json")!;
        Assert.Equal(3, saved["maxVisits"]!.GetValue<int>());

        for (var round = 0; round < 3; round++)
        {
            // the hot reload: the old instance unloads, a new one loads into a new collectible context
            // (Kiln reloads on the game's tick thread, which runs with denormals flushed: the lab's failure)
            first.Dispose();
            (first, _) = FloatMode.Supported ? FloatMode.WithDenormalsFlushed(() => LoadSample("webadmin-sample")) : LoadSample("webadmin-sample");
            var statsText = await (await owner.GetAsync("/api/sample/data/stats")).Content.ReadAsStringAsync();
            Assert.Contains("\"greeting\":\"Hi\"", statsText.Replace(" ", ""));
            // maxVisits 3 is in force: the fourth greeting is refused (the default would allow 100)
            for (var i = 0; i < 3; i++)
                Assert.Equal(System.Net.HttpStatusCode.OK, (await owner.PostAsync("/api/sample/actions/greet", new { name = "n" })).StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.Conflict, (await owner.PostAsync("/api/sample/actions/greet", new { name = "n" })).StatusCode);
            var got = await LaneC.JsonAsync(await owner.GetAsync("/api/sample/settings"));
            Assert.Equal(3, got!["value"]!["maxVisits"]!.GetValue<int>());
        }
        first.Dispose();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)] // the mod forgot OnUnload: the context's Unloading event detaches it
    public async Task Unloaded_mod_leaves_nothing_and_its_context_is_collected(bool dispose)
    {
        await using var c = await LaneC.StartAsync();
        var waiters = WebPanel.WaiterCount;
        var weak = LoadRegisterAndUnload(c.ExtHost, dispose);
        for (var i = 0; i < 20 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(20);
        }
        Assert.False(weak.IsAlive, "the sample's load context is still referenced");
        Assert.Empty(c.ExtHost.Attached);
        Assert.DoesNotContain(c.H.Host.Modules.Modules, m => m.Spec.Id == "sample");
        Assert.True(WebPanel.WaiterCount <= waiters);
    }
}
