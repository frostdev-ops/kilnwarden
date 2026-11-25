// SPDX-License-Identifier: MIT
using System.Text.Json;
using System.Text.Json.Nodes;
using Kilnwarden.Api;
using Kilnwarden.Events;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Modules;
using Kilnwarden.Routing;
using Kilnwarden.Security;
using ApiRunOn = Kilnwarden.Api.RunOn;
using CoreRunOn = Kilnwarden.Modules.RunOn;
using PageSpec = Kilnwarden.Modules.PageSpec;
using WidgetSpec = Kilnwarden.Modules.WidgetSpec;

namespace Kilnwarden.Extensions;

/// <summary>One extension module (IWebAdminModule) over its registry module. Routes, all under /api/&lt;id&gt;:
/// actions/&lt;name&gt; (POST, gated, audited), data/&lt;name&gt; (GET, gated), pages/&lt;id&gt; (declarative layout),
/// frame/&lt;id&gt; (custom page document, sandboxed), events (GET, per-topic permission), settings (GET/PUT, the
/// module's "&lt;id&gt;.settings" permission, sensitive).</summary>
public sealed class ExtensionModule : IWebAdminModule
{
    public const int MaxEvents = 200, MaxEventBytes = 16 << 10, MaxQueryBytes = 1 << 20, MaxScriptChars = 512 << 10, MaxStyleChars = 128 << 10;
    public const int MaxPublicWidgetBytes = 8 << 10;

    private readonly ExtensionHost _host;
    private readonly ExtensionHandle _owner;
    private readonly ModuleRegistration _reg;
    private readonly ModuleDefinition _def;
    private readonly object _gate = new();
    private readonly Dictionary<string, ActionDefinition> _actions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, QueryDefinition> _queries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PageDefinition> _pages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WidgetDefinition> _widgets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TopicDefinition> _topics;
    private readonly LinkedList<(long Seq, string Topic, DateTimeOffset At, JsonObject Payload)> _events = new();
    private long _seq;
    private IExtSettings? _settings;
    private int _disposed;

    private ExtensionModule(ExtensionHost host, ExtensionHandle owner, ModuleDefinition def, ModuleRegistration reg)
    {
        _host = host;
        _owner = owner;
        _def = def;
        _reg = reg;
        _topics = def.Topics.ToDictionary(t => t.Name, StringComparer.Ordinal);
    }

    public string Id => _def.Id;
    public string ModId => _owner.ModId;
    public bool Enabled => !IsDisposed && _host.Registry.IsEnabled(Id);
    public bool IsDisposed => Volatile.Read(ref _disposed) == 1;
    public string SettingsPermission => Id + ".settings";
    internal string EventOwner => $"{ModId}:{Id}";
    internal string SettingsFile => $"ext/{Id}.json";

    internal IReadOnlyList<WidgetDefinition> PublicWidgetList
    {
        get
        {
            lock (_gate) return [.. _widgets.Values.Where(w => w.Surface == Api.WidgetSurface.Public)];
        }
    }

    internal static ExtensionModule Create(ExtensionHost host, ExtensionHandle owner, ModuleDefinition d)
    {
        if (!ModuleSpec.IsModuleId(d.Id)) throw new ArgumentException($"bad module id '{d.Id}'");
        var perms = d.Permissions.Select(p => new PermissionSpec(p.Name, p.Title)
        {
            Description = p.Description, DefaultRole = (BuiltinRole)(int)p.DefaultRole, Sensitive = p.Sensitive,
        }).ToList();
        if (d.SettingsSchema is { } schema)
        {
            if (JsonSchemaLite.CheckSchema(schema) is { Count: > 0 } bad) throw new ArgumentException($"module '{d.Id}': settings schema: {string.Join("; ", bad)}");
            if (schema["type"]?.GetValue<string>() != "object") throw new ArgumentException($"module '{d.Id}': the settings schema must be of type object");
            if (perms.Any(p => p.Name == d.Id + ".settings")) throw new ArgumentException($"module '{d.Id}': '{d.Id}.settings' is declared for you");
            perms.Add(new PermissionSpec(d.Id + ".settings", $"Change {d.Title}'s settings") { DefaultRole = BuiltinRole.Owner, Sensitive = true });
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in d.Topics)
        {
            if (!ExtensionHost.IsName(t.Name) || !names.Add(t.Name)) throw new ArgumentException($"module '{d.Id}': bad or repeated topic '{t.Name}'");
            if (!perms.Any(p => p.Name == t.Permission)) throw new ArgumentException($"module '{d.Id}': topic '{t.Name}' needs a permission of this module");
            if (t.WebhookFields.Any(f => !ExtensionHost.IsName(f.ToLowerInvariant()) || f.Length > 64))
                throw new ArgumentException($"module '{d.Id}': topic '{t.Name}' has a bad webhook field name");
        }
        var spec = new ModuleSpec
        {
            Id = d.Id, Title = d.Title, Icon = d.Icon, Description = d.Description, DefaultEnabled = d.DefaultEnabled,
            Permissions = perms, SettingsSchema = (JsonObject?)d.SettingsSchema?.DeepClone(),
        };
        var reg = host.Registry.Register(spec, owner.ModId);
        var m = new ExtensionModule(host, owner, d, reg);
        try
        {
            m.MapCommon();
            foreach (var t in d.Topics.Where(t => t.Webhooks))
                host.Events.Declare(new EventKind($"{d.Id}.{t.Name}", t.Title ?? $"{d.Title}: {t.Name}", [.. t.WebhookFields], m.EventOwner));
        }
        catch
        {
            host.Registry.Unregister(d.Id);
            host.Events.Forget(m.EventOwner);
            throw;
        }
        return m;
    }

    private void MapCommon()
    {
        _reg.MapGet("/events", RouteAccess.SignedIn, ctx => ValueTask.FromResult(HttpResponse.Json(EventsFor(ctx))));
        _reg.MapGet("/describe", RouteAccess.SignedIn, ctx => ValueTask.FromResult(HttpResponse.Json(DescribeFor(ctx.User!))));
        if (_def.SettingsSchema is not null)
        {
            _reg.MapGet("/settings", RouteAccess.Requires(SettingsPermission), _ => ValueTask.FromResult(HttpResponse.Json(new JsonObject
            {
                ["schema"] = _def.SettingsSchema.DeepClone(), ["value"] = ReadSettingsFile(),
            })));
            _reg.MapPut("/settings", RouteAccess.Requires(SettingsPermission), ctx =>
            {
                var body = ctx.ReadJson<JsonObject>();
                if (JsonSchemaLite.Validate(_def.SettingsSchema, body) is { Count: > 0 } problems) throw new HttpError(422, string.Join("; ", problems.Take(10)));
                try
                {
                    IExtSettings? typed;
                    lock (_gate) typed = _settings;
                    if (typed is not null) typed.Apply(body);
                    else _host.Data.WriteJson(SettingsFile, body, secret: true);
                }
                catch (Exception e) when (e is ArgumentException or JsonException or NotSupportedException)
                {
                    throw new HttpError(422, e.Message);
                }
                Audit(ctx, $"{Id}.settings.edit", null, true, null);
                return ValueTask.FromResult(HttpResponse.Json(new { ok = true }));
            });
        }
    }

    private JsonNode? ReadSettingsFile()
    {
        try
        {
            return _host.Data.ReadJson<JsonObject>(SettingsFile) ?? new JsonObject();
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            return null;
        }
    }

    private bool OwnPermission(string? p) => p is not null && _host.Registry.FindPermission(p) is not null && p.StartsWith(Id + ".", StringComparison.Ordinal);

    public void AddAction(ActionDefinition action, Api.ActionHandler handler)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(handler);
        ThrowIfDisposed();
        if (!ExtensionHost.IsName(action.Name)) throw new ArgumentException($"bad action name '{action.Name}'");
        if (!OwnPermission(action.Permission)) throw new ArgumentException($"action '{action.Name}': '{action.Permission}' is not a permission of module '{Id}'");
        if (action.ArgsSchema is { } s && JsonSchemaLite.CheckSchema(s) is { Count: > 0 } bad) throw new ArgumentException($"action '{action.Name}': {string.Join("; ", bad)}");
        var schema = (JsonObject?)action.ArgsSchema?.DeepClone();
        lock (_gate)
        {
            if (_actions.ContainsKey(action.Name)) throw new InvalidOperationException($"action '{Id}/{action.Name}' exists");
            _actions[action.Name] = action;
        }
        try
        {
            _reg.AddAction(new ActionSpec(action.Name, action.Permission, async call =>
            {
                if (schema is not null && JsonSchemaLite.Validate(schema, call.Args) is { Count: > 0 } problems)
                    throw new HttpError(422, string.Join("; ", problems.Take(10)));
                try
                {
                    var r = await handler(new ActionContext
                    {
                        Module = Id, Action = action.Name, Args = (JsonObject)call.Args.DeepClone(), User = UserOf(call.User), Aborted = call.Aborted,
                    }).ConfigureAwait(false);
                    return r?.DeepClone();
                }
                catch (WebAdminRefusal r)
                {
                    throw new HttpError(r.Status, r.Message);
                }
            })
            {
                RunOn = action.RunOn == ApiRunOn.GameThread ? CoreRunOn.GameThread : CoreRunOn.Background,
                Title = action.Title, ArgsSchema = schema, Redact = [.. action.Redact],
            });
        }
        catch
        {
            lock (_gate) _actions.Remove(action.Name);
            throw;
        }
    }

    public void AddQuery(QueryDefinition query, QueryHandler handler)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(handler);
        ThrowIfDisposed();
        if (!ExtensionHost.IsName(query.Name)) throw new ArgumentException($"bad query name '{query.Name}'");
        if (!OwnPermission(query.Permission)) throw new ArgumentException($"query '{query.Name}': '{query.Permission}' is not a permission of module '{Id}'");
        lock (_gate)
        {
            if (_queries.ContainsKey(query.Name)) throw new InvalidOperationException($"query '{Id}/{query.Name}' exists");
            _queries[query.Name] = query;
        }
        try
        {
            _reg.MapGet("/data/" + query.Name, RouteAccess.Requires(query.Permission), async ctx =>
            {
                var qc = new QueryContext
                {
                    Module = Id, Query = query.Name, Parameters = ctx.Request.QueryValues, User = UserOf(ctx.User!), Aborted = ctx.Aborted,
                };
                JsonNode? result;
                try
                {
                    if (query.RunOn == ApiRunOn.GameThread)
                    {
                        var game = ctx.Services.TryGet<IGameThread>() ?? throw new HttpError(503, "the game thread is not available");
                        result = await (await game.InvokeAsync(() => handler(qc).AsTask(), ctx.Aborted).ConfigureAwait(false)).ConfigureAwait(false);
                    }
                    else result = await handler(qc).ConfigureAwait(false);
                }
                catch (WebAdminRefusal r)
                {
                    return HttpResponse.Error(r.Status, r.Code, r.Message);
                }
                catch (Exception e) when (e is not (HttpError or OperationCanceledException))
                {
                    _host.Log.Error($"extension {ModId}: query {Id}/{query.Name} failed", e);
                    return HttpResponse.Error(500, "query_failed", "the module's query failed (see kiln.log)");
                }
                var body = JsonSerializer.SerializeToUtf8Bytes(new JsonObject { ["result"] = result?.DeepClone() });
                return body.Length > MaxQueryBytes
                    ? HttpResponse.Error(500, "too_large", "the module's answer is too large")
                    : HttpResponse.Bytes(200, "application/json; charset=utf-8", body);
            });
        }
        catch
        {
            lock (_gate) _queries.Remove(query.Name);
            throw;
        }
    }

    public void AddPage(PageDefinition page)
    {
        ArgumentNullException.ThrowIfNull(page);
        ThrowIfDisposed();
        if (!ExtensionHost.IsName(page.Id)) throw new ArgumentException($"bad page id '{page.Id}'");
        if (string.IsNullOrWhiteSpace(page.Title)) throw new ArgumentException($"page '{page.Id}' has no title");
        if (page.Permission is not null && !OwnPermission(page.Permission))
            throw new ArgumentException($"page '{page.Id}': '{page.Permission}' is not a permission of module '{Id}'");
        var access = page.Permission is null ? RouteAccess.SignedIn : RouteAccess.Requires(page.Permission);
        string path;
        switch (page)
        {
            case DeclarativePage dp:
                if (dp.Blocks.Count > 64) throw new ArgumentException($"page '{page.Id}': at most 64 blocks");
                path = "/pages/" + page.Id;
                break;
            case CustomPage cp:
                if (string.IsNullOrEmpty(cp.Script) || cp.Script.Length > MaxScriptChars) throw new ArgumentException($"page '{page.Id}': the script must be 1 to {MaxScriptChars} characters");
                if (cp.Style is { Length: > MaxStyleChars }) throw new ArgumentException($"page '{page.Id}': the style is over {MaxStyleChars} characters");
                path = "/frame/" + page.Id;
                break;
            default:
                throw new ArgumentException($"page '{page.Id}': use DeclarativePage or CustomPage");
        }
        lock (_gate)
        {
            if (_pages.ContainsKey(page.Id)) throw new InvalidOperationException($"page '{Id}/{page.Id}' exists");
            _pages[page.Id] = page;
        }
        try
        {
            if (page is DeclarativePage d) _reg.MapGet(path, access, ctx => ValueTask.FromResult(HttpResponse.Json(ExtensionPages.Layout(this, d, ctx.User!))));
            else _reg.MapGet(path, access, ctx => ValueTask.FromResult(ExtensionPages.Frame(this, (CustomPage)page, ctx)));
            UpdatePresentation();
        }
        catch
        {
            lock (_gate) _pages.Remove(page.Id);
            throw;
        }
    }

    public void AddWidget(WidgetDefinition widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        ThrowIfDisposed();
        if (!ExtensionHost.IsName(widget.Id)) throw new ArgumentException($"bad widget id '{widget.Id}'");
        if (widget.Kind is not ("stat" or "table" or "text")) throw new ArgumentException($"widget '{widget.Id}': kind is stat, table or text");
        if (widget.Surface == Api.WidgetSurface.Dashboard)
        {
            if (widget.Query is null || FindQuery(widget.Query) is null) throw new ArgumentException($"widget '{widget.Id}': add its query first");
        }
        else if (widget.PublicData is null) throw new ArgumentException($"widget '{widget.Id}': a public widget needs PublicData");
        lock (_gate)
        {
            if (_widgets.ContainsKey(widget.Id)) throw new InvalidOperationException($"widget '{Id}/{widget.Id}' exists");
            _widgets[widget.Id] = widget;
        }
        try
        {
            UpdatePresentation();
        }
        catch
        {
            lock (_gate) _widgets.Remove(widget.Id);
            throw;
        }
    }

    private void UpdatePresentation()
    {
        List<PageSpec> pages;
        List<WidgetSpec> widgets;
        lock (_gate)
        {
            pages = [.. _pages.Values.Select(p => new PageSpec(p.Id, p.Title, p is CustomPage ? PageKind.Custom : PageKind.Declarative,
                _reg.FullPath((p is CustomPage ? "/frame/" : "/pages/") + p.Id)) { Icon = p.Icon, Permission = p.Permission, Order = p.Order })];
            widgets = [.. _widgets.Values.Select(w => w.Surface == Api.WidgetSurface.Dashboard
                ? new WidgetSpec(w.Id, w.Title, Modules.WidgetSurface.Dashboard, _reg.FullPath("/data/" + w.Query)) { Permission = _queries[w.Query!].Permission, Kind = w.Kind, Field = w.Field }
                : new WidgetSpec(w.Id, w.Title, Modules.WidgetSurface.Public, "/api/public/status") { Kind = w.Kind })];
        }
        _host.Registry.SetPresentation(Id, pages, widgets);
    }

    public void Publish(string topic, JsonObject? payload)
    {
        if (IsDisposed || !Enabled) return;
        if (!_topics.TryGetValue(topic, out var t)) throw new ArgumentException($"module '{Id}' has no topic '{topic}'");
        var copy = (JsonObject?)payload?.DeepClone() ?? [];
        if (JsonSerializer.SerializeToUtf8Bytes(copy).Length > MaxEventBytes) throw new ArgumentException($"topic '{topic}': the payload is over {MaxEventBytes} bytes");
        var at = _host.Time.GetUtcNow();
        lock (_gate)
        {
            _events.AddLast((++_seq, topic, at, copy));
            while (_events.Count > MaxEvents) _events.RemoveFirst();
        }
        if (t.Webhooks) _host.Events.Publish($"{Id}.{topic}", (JsonObject)copy.DeepClone(), at);
    }

    private JsonObject EventsFor(RequestContext ctx)
    {
        long after = ctx.Request.QueryValues.TryGetValue("after", out var a) && long.TryParse(a, out var n) ? n : 0;
        var user = ctx.User!;
        var arr = new JsonArray();
        long last;
        lock (_gate)
        {
            last = _seq;
            foreach (var e in _events)
                if (e.Seq > after && _topics.TryGetValue(e.Topic, out var t) && user.Has(t.Permission))
                    arr.Add(new JsonObject { ["seq"] = e.Seq, ["topic"] = e.Topic, ["at"] = e.At.ToString("O"), ["payload"] = e.Payload.DeepClone() });
        }
        return new JsonObject { ["seq"] = last, ["events"] = arr };
    }

    /// <summary>What this user may name in the module (the custom page host's allow-list; the server checks each call).</summary>
    internal JsonObject DescribeFor(IPrincipal user)
    {
        lock (_gate)
            return new JsonObject
            {
                ["module"] = Id,
                ["actions"] = new JsonArray([.. _actions.Values.Where(a => user.Has(a.Permission)).OrderBy(a => a.Name, StringComparer.Ordinal)
                    .Select(a => (JsonNode)new JsonObject { ["name"] = a.Name, ["title"] = a.Title, ["confirm"] = a.Confirm, ["schema"] = a.ArgsSchema?.DeepClone() })]),
                ["queries"] = new JsonArray([.. _queries.Values.Where(q => user.Has(q.Permission)).Select(q => (JsonNode)q.Name).Order()]),
                ["topics"] = new JsonArray([.. _topics.Values.Where(t => user.Has(t.Permission)).Select(t => (JsonNode)t.Name).Order()]),
                ["settings"] = _def.SettingsSchema is not null && user.Has(SettingsPermission),
            };
    }

    public IWebAdminSettings<T> Settings<T>() where T : class, new()
    {
        ThrowIfDisposed();
        lock (_gate)
        {
            if (_settings is ExtSettings<T> same) return same;
            if (_settings is not null) throw new InvalidOperationException($"module '{Id}': settings are already read as another type");
            var s = new ExtSettings<T>(_host.Data, SettingsFile, _host.Log, (JsonObject?)_def.SettingsSchema?.DeepClone(), _host.Time);
            _settings = s;
            return s;
        }
    }

    internal ActionDefinition? FindAction(string name)
    {
        lock (_gate) return _actions.GetValueOrDefault(name);
    }

    internal QueryDefinition? FindQuery(string name)
    {
        lock (_gate) return _queries.GetValueOrDefault(name);
    }

    internal IReadOnlyList<TopicDefinition> Topics => [.. _topics.Values];

    internal static WebAdminUser UserOf(IPrincipal p) => new(p.Id, p.Name, p.Has);

    private static void Audit(RequestContext ctx, string op, JsonObject? args, bool ok, string? error) =>
        ctx.Services.TryGet<IAuditSink>()?.Record(new AuditEntry(DateTimeOffset.UtcNow, ctx.User?.Id ?? "", ctx.User?.Name ?? "", op, args, ok, error, 0,
            ctx.ClientAddress));

    private void ThrowIfDisposed()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(IWebAdminModule), $"module '{Id}' was removed");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        IExtSettings? s;
        lock (_gate)
        {
            s = _settings;
            _settings = null;
            _events.Clear();
        }
        s?.Dispose();
        _host.Registry.Unregister(Id);
        _host.Events.Forget(EventOwner);
        _owner.Removed(this);
    }
}
