// SPDX-License-Identifier: MIT
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Kilnwarden.Api;
using WebAdminApi = Kilnwarden.Api.WebPanel;
using Kilnwarden.Events;
using Kilnwarden.Hosting;
using Kilnwarden.Modules;
using Kilnwarden.Storage;

namespace Kilnwarden.Extensions;

/// <summary>The panel's side of Kilnwarden.Api (DESIGN §6, lane C): &lt;see cref="IWebAdmin"/&gt; over the module
/// registry. Every extension module is an ordinary registry module owned by its mod id, so the gate, the audit log, the
/// on/off switches and the navigation treat it like a first-party one. Extensions get no service of the panel: only
/// what the API hands them (their own module's calls, their own settings file, the calling user's id, name and
/// permission check).</summary>
public sealed partial class ExtensionHost : IWebAdmin
{
    /// <summary>Module ids extensions may not take: every first-party module of every lane (the KAP modules from
    /// KapPolicy, auth, core, identity/acme, ops, lane C), route words and web paths. The composed-graph test checks that
    /// every module the full component list registers is in here.</summary>
    public static readonly IReadOnlySet<string> ReservedIds = new HashSet<string>(
    [
        "core", "auth", "users", "audit", "server", "extensions", "metrics", "webhooks", "public", "schedules", "mods", "commands",
        "identity", "steam", "discord", "acme", "tls", "api", "ext", "static", "assets", "web", "kap", "dashboard", "settings",
        // integration: lane B's modules, branding, and the web app's own paths
        "updates", "saved-commands", "branding", "public-page", "m", "signin", "setup", "launcher", "account", "servers",
        // complete: player activity, and every first-party event prefix (an extension topic is "<module>.<topic>", so a
        // module named like these would publish events that look like the panel's own to webhooks: review I5)
        "activity", "player", "mod", "crash", "save", "schedule", "admin", "webhook", "panel", "kiln",
        .. Kap.KapPolicy.Modules.Select(m => m.Id),
    ], StringComparer.Ordinal);

    private readonly object _gate = new();
    private readonly Dictionary<string, ExtensionHandle> _attached = new(StringComparer.Ordinal);
    private bool _closed;

    internal ExtensionHost(ModuleRegistry registry, DataStore data, WebAdminServices services, IWebAdminLog log, TimeProvider time, PanelEvents events)
    {
        Registry = registry;
        Data = data;
        Services = services;
        Log = log;
        Time = time;
        Events = events;
    }

    internal ModuleRegistry Registry { get; }
    internal DataStore Data { get; }
    internal WebAdminServices Services { get; }
    internal IWebAdminLog Log { get; }
    internal TimeProvider Time { get; }
    internal PanelEvents Events { get; }

    public string Version => Product.Version;
    public string ApiVersion => WebAdminApi.ApiVersion;

    /// <summary>The mods attached now.</summary>
    public IReadOnlyList<ExtensionHandle> Attached
    {
        get
        {
            lock (_gate) return [.. _attached.Values.OrderBy(h => h.ModId, StringComparer.Ordinal)];
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public IWebAdminExtension Attach(string modId) => AttachFor(modId, Assembly.GetCallingAssembly());

    /// <summary>Attaches <paramref name="modId"/>; when <paramref name="caller"/> lives in a collectible load context
    /// (a Kiln mod), its unloading detaches the mod even if it never disposed its handle.</summary>
    internal ExtensionHandle AttachFor(string modId, Assembly? caller)
    {
        if (!IsModId(modId)) throw new ArgumentException($"'{modId}' is not a Kiln mod id");
        if (modId == Product.ModId) throw new ArgumentException("the panel's own id");
        var alc = caller is null ? null : AssemblyLoadContext.GetLoadContext(caller);
        if (alc is not null && (alc == AssemblyLoadContext.Default || !alc.IsCollectible)) alc = null;
        lock (_gate)
        {
            if (_closed) throw new InvalidOperationException("the panel is stopping");
            if (_attached.ContainsKey(modId)) throw new InvalidOperationException($"'{modId}' is already attached: dispose its handle first");
            var h = new ExtensionHandle(this, modId, alc);
            _attached[modId] = h;
            Log.Info($"extension {modId} attached");
            return h;
        }
    }

    internal void Detached(ExtensionHandle h)
    {
        lock (_gate)
            if (_attached.TryGetValue(h.ModId, out var cur) && ReferenceEquals(cur, h)) _attached.Remove(h.ModId);
    }

    /// <summary>Detaches every mod (the panel stops) and refuses new ones.</summary>
    internal void DetachAll()
    {
        List<ExtensionHandle> all;
        lock (_gate)
        {
            _closed = true;
            all = [.. _attached.Values];
        }
        foreach (var h in all) h.Dispose();
    }

    /// <summary>The public widgets of switched-on modules: (module/widget id, title, kind, provider).</summary>
    internal IReadOnlyList<(string Key, string Title, string Kind, Func<JsonNode?> Data)> PublicWidgets()
    {
        var list = new List<(string, string, string, Func<JsonNode?>)>();
        foreach (var h in Attached)
            foreach (var m in h.Modules)
                if (m.Enabled)
                    foreach (var w in m.PublicWidgetList) list.Add(($"{m.Id}/{w.Id}", w.Title, w.Kind, w.PublicData!));
        return list;
    }

    public static bool IsModId(string? s) => s is not null && ModIdRe().IsMatch(s);

    /// <summary>Names of actions, queries, pages, widgets and topics.</summary>
    public static bool IsName(string? s) => ModuleSpec.IsModuleId(s);

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,63}$")]
    private static partial Regex ModIdRe();
}

/// <summary>One mod's attachment (IWebAdminExtension).</summary>
public sealed class ExtensionHandle : IWebAdminExtension
{
    private readonly ExtensionHost _host;
    private readonly AssemblyLoadContext? _alc;
    private readonly object _gate = new();
    private readonly List<ExtensionModule> _modules = [];
    private int _disposed;

    internal ExtensionHandle(ExtensionHost host, string modId, AssemblyLoadContext? alc)
    {
        _host = host;
        ModId = modId;
        _alc = alc;
        if (alc is not null) alc.Unloading += OnUnloading;
    }

    public string ModId { get; }

    public bool IsDisposed => Volatile.Read(ref _disposed) == 1;

    public IReadOnlyList<ExtensionModule> Modules
    {
        get
        {
            lock (_gate) return [.. _modules];
        }
    }

    public IWebAdminModule RegisterModule(ModuleDefinition module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (IsDisposed) throw new ObjectDisposedException(nameof(IWebAdminExtension));
        if (ExtensionHost.ReservedIds.Contains(module.Id)) throw new InvalidOperationException($"module id '{module.Id}' is reserved");
        var m = ExtensionModule.Create(_host, this, module);
        lock (_gate) _modules.Add(m);
        return m;
    }

    internal void Removed(ExtensionModule m)
    {
        lock (_gate) _modules.Remove(m);
    }

    private void OnUnloading(AssemblyLoadContext alc)
    {
        _host.Log.Warn($"extension {ModId}: its mod unloaded without disposing its web panel handle; removing its modules");
        Dispose();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        if (_alc is { } alc)
        {
            alc.Unloading -= OnUnloading;
            // callbacks of that mod's code would keep its load context alive
            WebAdminApi.Forget(d => AssemblyLoadContext.GetLoadContext(d.Method.Module.Assembly) == alc);
        }
        foreach (var m in Modules) m.Dispose();
        _host.Registry.UnregisterOwner(ModId);
        _host.Events.Forget(ModId);
        _host.Detached(this);
        _host.Log.Info($"extension {ModId} detached");
    }
}
