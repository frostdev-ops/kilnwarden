// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;
using Kilnwarden.Http;
using Kilnwarden.Routing;
using Kilnwarden.Security;
using Kilnwarden.Storage;
using Kilnwarden.WebSockets;

namespace Kilnwarden.Modules;

/// <summary>Every module, its routes, actions and permissions, and its on/off switch (DESIGN §5). Components register
/// during Configure; extension mods (step 2, through Kilnwarden.Api) register and unregister at any time, by owner
/// (their mod id), so a reloading mod takes its modules with it. Reads are lock-free snapshots; changes rebuild the
/// route table and raise <see cref="Changed"/>. The owner's on/off choices persist in modules.json.</summary>
public sealed class ModuleRegistry
{
    public const string StateFile = "modules.json";

    /// <summary>Module routes live under /api/&lt;module id&gt;.</summary>
    public const string ApiPrefix = "/api/";

    private readonly object _gate = new();
    private readonly DataStore? _data;
    private readonly Dictionary<string, Registered> _modules = new(StringComparer.Ordinal);
    private Dictionary<string, bool> _switches = new(StringComparer.Ordinal);
    private Snapshot _snapshot = new([], RouteTable.Empty, new Dictionary<string, PermissionSpec>());

    public ModuleRegistry(DataStore? data = null)
    {
        _data = data;
        if (data?.ReadJson<ModuleState>(StateFile) is { } st) _switches = new(st.Switches, StringComparer.Ordinal);
    }

    public event Action? Changed;

    /// <summary>The current route table (rebuilt on every change).</summary>
    public RouteTable Routes => Volatile.Read(ref _snapshot).Routes;

    public IReadOnlyList<RegisteredModule> Modules => Volatile.Read(ref _snapshot).Modules;

    public IReadOnlyCollection<PermissionSpec> Permissions => [.. Volatile.Read(ref _snapshot).Permissions.Values];

    public PermissionSpec? FindPermission(string name) => Volatile.Read(ref _snapshot).Permissions.GetValueOrDefault(name);

    public bool IsEnabled(string moduleId) =>
        Volatile.Read(ref _snapshot).Modules.FirstOrDefault(m => m.Spec.Id == moduleId) is { Enabled: true };

    /// <summary>Registers <paramref name="spec"/> for <paramref name="owner"/> (a Kiln mod id; first-party modules use
    /// <see cref="Product.ModId"/>). Throws ArgumentException on a bad spec and InvalidOperationException when the id or a
    /// permission name is taken.</summary>
    public ModuleRegistration Register(ModuleSpec spec, string owner)
    {
        Validate(spec);
        lock (_gate)
        {
            if (_modules.ContainsKey(spec.Id)) throw new InvalidOperationException($"module '{spec.Id}' is already registered");
            foreach (var p in spec.Permissions)
                if (_modules.Values.Any(m => m.Spec.Permissions.Any(q => q.Name == p.Name)))
                    throw new InvalidOperationException($"permission '{p.Name}' is already declared");
            _modules[spec.Id] = new Registered(spec, owner);
            Rebuild();
        }
        RaiseChanged();
        return new ModuleRegistration(this, spec.Id);
    }

    public bool Unregister(string moduleId)
    {
        lock (_gate)
        {
            if (!_modules.Remove(moduleId)) return false;
            Rebuild();
        }
        RaiseChanged();
        return true;
    }

    /// <summary>Removes every module of <paramref name="owner"/> (an extension mod that unloads).</summary>
    public int UnregisterOwner(string owner)
    {
        int n;
        lock (_gate)
        {
            var ids = _modules.Values.Where(m => m.Owner == owner).Select(m => m.Spec.Id).ToList();
            foreach (var id in ids) _modules.Remove(id);
            n = ids.Count;
            if (n > 0) Rebuild();
        }
        if (n > 0) RaiseChanged();
        return n;
    }

    /// <summary>Switches a module on or off and saves the choice. Throws for an unknown module or one that cannot be
    /// switched off.</summary>
    public void SetEnabled(string moduleId, bool enabled)
    {
        lock (_gate)
        {
            if (!_modules.TryGetValue(moduleId, out var m)) throw new KeyNotFoundException($"no module '{moduleId}'");
            if (!enabled && !m.Spec.CanDisable) throw new InvalidOperationException($"module '{moduleId}' cannot be switched off");
            var next = new Dictionary<string, bool>(_switches, StringComparer.Ordinal);
            if (enabled == m.Spec.DefaultEnabled) next.Remove(moduleId);
            else next[moduleId] = enabled;
            _data?.WriteJson(StateFile, new ModuleState { Switches = next });
            _switches = next;
            Rebuild();
        }
        RaiseChanged();
    }

    internal void AddRoute(string moduleId, RouteSpec route)
    {
        lock (_gate)
        {
            var m = _modules.GetValueOrDefault(moduleId) ?? throw new InvalidOperationException($"module '{moduleId}' is not registered");
            if (route.Access.Kind == AccessKind.Permission && !_modules.Values.Any(x => x.Spec.Permissions.Any(p => p.Name == route.Access.Permission)))
                throw new ArgumentException($"{route}: permission '{route.Access.Permission}' is not declared by any module");
            var clash = _modules.Values.SelectMany(x => x.Routes)
                .FirstOrDefault(r => r.Method == route.Method && RouteTable.SameShape(r.Pattern, route.Pattern));
            if (clash is not null) throw new InvalidOperationException($"{route} clashes with {clash}");
            m.Routes.Add(route);
            Rebuild();
        }
        RaiseChanged();
    }

    internal void AddAction(string moduleId, ActionSpec action)
    {
        lock (_gate)
        {
            var m = _modules.GetValueOrDefault(moduleId) ?? throw new InvalidOperationException($"module '{moduleId}' is not registered");
            if (!ModuleSpec.IsModuleId(action.Name)) throw new ArgumentException($"bad action name '{action.Name}'");
            if (m.Actions.ContainsKey(action.Name)) throw new InvalidOperationException($"action '{moduleId}/{action.Name}' exists");
            m.Actions[action.Name] = action;
        }
        // the route that runs it; ActionRoutes does the gate-checked call, the game-thread hop and the audit record
        AddRoute(moduleId, ActionRoutes.RouteFor(moduleId, action));
    }

    /// <summary>Replaces a module's pages and widgets (an extension adds them after it registered, lane C). Routes,
    /// actions and permissions stay as they are.</summary>
    internal void SetPresentation(string moduleId, IReadOnlyList<PageSpec> pages, IReadOnlyList<WidgetSpec> widgets)
    {
        lock (_gate)
        {
            var m = _modules.GetValueOrDefault(moduleId) ?? throw new InvalidOperationException($"module '{moduleId}' is not registered");
            var next = m.Spec with { Pages = [.. pages], Widgets = [.. widgets] };
            Validate(next);
            m.Spec = next;
            Rebuild();
        }
        RaiseChanged();
    }

    public ActionSpec? FindAction(string moduleId, string name)
    {
        lock (_gate) return _modules.GetValueOrDefault(moduleId)?.Actions.GetValueOrDefault(name);
    }

    /// <summary>What the web app's navigation needs for <paramref name="user"/>: enabled modules with the pages, widgets
    /// and permissions this user can see.</summary>
    public JsonArray Describe(IPrincipal? user)
    {
        var arr = new JsonArray();
        foreach (var m in Modules)
        {
            if (!m.Enabled) continue;
            bool Sees(string? perm) => perm is null ? user is not null : user?.Has(perm) == true;
            var pages = new JsonArray();
            foreach (var p in m.Spec.Pages.Where(p => Sees(p.Permission)).OrderBy(p => p.Order))
                pages.Add(new JsonObject { ["id"] = p.Id, ["title"] = p.Title, ["kind"] = p.Kind.ToString().ToLowerInvariant(), ["entry"] = p.Entry, ["icon"] = p.Icon });
            var widgets = new JsonArray();
            foreach (var w in m.Spec.Widgets.Where(w => w.Surface == WidgetSurface.Dashboard && Sees(w.Permission)))
                widgets.Add(new JsonObject { ["id"] = w.Id, ["title"] = w.Title, ["data"] = w.DataPath, ["kind"] = w.Kind, ["field"] = w.Field });
            if (pages.Count == 0 && widgets.Count == 0) continue;
            arr.Add(new JsonObject
            {
                ["id"] = m.Spec.Id,
                ["title"] = m.Spec.Title,
                ["icon"] = m.Spec.Icon,
                ["pages"] = pages,
                ["widgets"] = widgets,
                ["permissions"] = new JsonArray([.. m.Spec.Permissions.Where(p => user?.Has(p.Name) == true).Select(p => (JsonNode)p.Name)]),
            });
        }
        return arr;
    }

    private void Rebuild()
    {
        var modules = _modules.Values
            .Select(m => new RegisteredModule(m.Spec, m.Owner, _switches.TryGetValue(m.Spec.Id, out var on) ? on || !m.Spec.CanDisable : m.Spec.DefaultEnabled,
                [.. m.Routes], [.. m.Actions.Values]))
            .OrderBy(m => m.Spec.Id, StringComparer.Ordinal)
            .ToList();
        var perms = modules.SelectMany(m => m.Spec.Permissions).ToDictionary(p => p.Name, StringComparer.Ordinal);
        Volatile.Write(ref _snapshot, new Snapshot(modules, new RouteTable(modules.SelectMany(m => m.Routes)), perms));
    }

    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception)
        {
            // a listener's bug never undoes a registration
        }
    }

    private static void Validate(ModuleSpec spec)
    {
        if (!ModuleSpec.IsModuleId(spec.Id)) throw new ArgumentException($"bad module id '{spec.Id}'");
        if (string.IsNullOrWhiteSpace(spec.Title)) throw new ArgumentException($"module '{spec.Id}' has no title");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in spec.Permissions)
        {
            if (!RouteAccess.IsPermissionName(p.Name) || !p.Name.StartsWith(spec.Id + ".", StringComparison.Ordinal))
                throw new ArgumentException($"module '{spec.Id}': permission '{p.Name}' must be '{spec.Id}.<name>' (lower-case, dot-separated)");
            if (!seen.Add(p.Name)) throw new ArgumentException($"module '{spec.Id}': permission '{p.Name}' twice");
        }
        foreach (var page in spec.Pages)
            if (page.Permission is not null && !seen.Contains(page.Permission) && !RouteAccess.IsPermissionName(page.Permission))
                throw new ArgumentException($"module '{spec.Id}': page '{page.Id}' has a bad permission '{page.Permission}'");
    }

    private sealed class Registered(ModuleSpec spec, string owner)
    {
        public ModuleSpec Spec = spec;
        public readonly string Owner = owner;
        public readonly List<RouteSpec> Routes = [];
        public readonly Dictionary<string, ActionSpec> Actions = new(StringComparer.Ordinal);
    }

    private sealed record Snapshot(IReadOnlyList<RegisteredModule> Modules, RouteTable Routes, IReadOnlyDictionary<string, PermissionSpec> Permissions);

    private sealed class ModuleState
    {
        public int Format { get; set; } = 1;

        /// <summary>Modules the owner switched away from their default: id -> on.</summary>
        public Dictionary<string, bool> Switches { get; set; } = [];
    }
}

/// <summary>A module as registered: its spec, the mod that owns it, whether it is on, its routes and actions.</summary>
public sealed record RegisteredModule(ModuleSpec Spec, string Owner, bool Enabled, IReadOnlyList<RouteSpec> Routes, IReadOnlyList<ActionSpec> Actions);

/// <summary>What <see cref="ModuleRegistry.Register"/> returns: the module's way to add routes and actions, mounted under
/// /api/&lt;id&gt;. Disposing it unregisters the module.</summary>
public sealed class ModuleRegistration : IDisposable
{
    private readonly ModuleRegistry _registry;

    internal ModuleRegistration(ModuleRegistry registry, string moduleId)
    {
        _registry = registry;
        ModuleId = moduleId;
    }

    public string ModuleId { get; }

    /// <summary>The full path of a module-relative one: "/{id}" -> "/api/players/{id}".</summary>
    public string FullPath(string relative) =>
        ModuleRegistry.ApiPrefix + ModuleId + (relative is "" or "/" ? "" : relative.StartsWith('/') ? relative : "/" + relative);

    public ModuleRegistration Map(string method, string path, RouteAccess access, RouteHandler handler, long? maxBodyBytes = null)
    {
        if (method is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE")) throw new ArgumentException($"method '{method}' is not routable");
        _registry.AddRoute(ModuleId, new RouteSpec
        {
            Method = method, Pattern = FullPath(path), Access = access, ModuleId = ModuleId, Handler = handler, MaxBodyBytes = maxBodyBytes,
        });
        return this;
    }

    public ModuleRegistration MapGet(string path, RouteAccess access, RouteHandler handler) => Map("GET", path, access, handler);
    public ModuleRegistration MapPost(string path, RouteAccess access, RouteHandler handler, long? maxBodyBytes = null) => Map("POST", path, access, handler, maxBodyBytes);
    public ModuleRegistration MapPut(string path, RouteAccess access, RouteHandler handler, long? maxBodyBytes = null) => Map("PUT", path, access, handler, maxBodyBytes);
    public ModuleRegistration MapPatch(string path, RouteAccess access, RouteHandler handler, long? maxBodyBytes = null) => Map("PATCH", path, access, handler, maxBodyBytes);
    public ModuleRegistration MapDelete(string path, RouteAccess access, RouteHandler handler) => Map("DELETE", path, access, handler);

    /// <summary>A WebSocket endpoint (GET + Upgrade). The upgrade request passes the same middleware and gate as any GET;
    /// the core also requires an Origin equal to the panel's own (cross-site WebSocket hijacking).</summary>
    public ModuleRegistration MapWebSocket(string path, RouteAccess access, WebSocketHandler handler)
    {
        _registry.AddRoute(ModuleId, new RouteSpec { Method = "GET", Pattern = FullPath(path), Access = access, ModuleId = ModuleId, WebSocket = handler });
        return this;
    }

    /// <summary>Adds an action: POST /api/&lt;module&gt;/actions/&lt;name&gt;, gated on its permission, audited.</summary>
    public ModuleRegistration AddAction(ActionSpec action)
    {
        _registry.AddAction(ModuleId, action);
        return this;
    }

    public void Dispose() => _registry.Unregister(ModuleId);
}
