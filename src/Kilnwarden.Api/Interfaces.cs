// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;

namespace Kilnwarden.Api;

/// <summary>The running panel.</summary>
public interface IWebAdmin
{
    /// <summary>The panel mod's version.</summary>
    string Version { get; }

    /// <summary>This API's version as the panel implements it (<see cref="WebPanel.ApiVersion"/> of the panel's copy).</summary>
    string ApiVersion { get; }

    /// <summary>The handle of <paramref name="modId"/> (the caller's Kiln mod id). One per mod at a time: attaching again
    /// while the earlier handle is alive throws InvalidOperationException.</summary>
    IWebAdminExtension Attach(string modId);
}

/// <summary>One mod's presence in the panel. Disposing it removes every module, route, page, widget and settings watcher
/// the mod registered.</summary>
public interface IWebAdminExtension : IDisposable
{
    string ModId { get; }

    /// <summary>Registers a module (a navigation entry with its own permissions). Throws ArgumentException for a bad
    /// definition and InvalidOperationException when its id or a permission name is taken.</summary>
    IWebAdminModule RegisterModule(ModuleDefinition module);
}

/// <summary>A registered module: add actions, queries, pages and widgets, publish events, read settings.</summary>
public interface IWebAdminModule : IDisposable
{
    string Id { get; }

    /// <summary>Whether the owner has the module switched on (off: its routes answer 404 and Publish drops events).</summary>
    bool Enabled { get; }

    /// <summary>POST /api/&lt;module&gt;/actions/&lt;name&gt;: gated on the action's permission, audited.</summary>
    void AddAction(ActionDefinition action, ActionHandler handler);

    /// <summary>GET /api/&lt;module&gt;/data/&lt;name&gt;: a read for pages and widgets, gated, not audited.</summary>
    void AddQuery(QueryDefinition query, QueryHandler handler);

    /// <summary>A page: <see cref="DeclarativePage"/> (no JavaScript) or <see cref="CustomPage"/> (sandboxed iframe).</summary>
    void AddPage(PageDefinition page);

    void AddWidget(WidgetDefinition widget);

    /// <summary>Sends an event on a declared topic to subscribed pages (and webhooks when the topic allows it).</summary>
    void Publish(string topic, JsonObject? payload);

    /// <summary>The module's settings (ext/&lt;module&gt;.json), typed and hot reloaded. One type per module. T has a public
    /// parameterless constructor and public get/set properties of type string, bool, int, long, double, float, decimal,
    /// an enum (stored as its name), string[] or List&lt;string&gt; (JSON names are camelCase). The panel converts them
    /// itself, so it keeps no reference to your types after your mod unloads. Throws ArgumentException for another
    /// property type.</summary>
    IWebAdminSettings<T> Settings<T>() where T : class, new();
}

/// <summary>Typed settings that follow the file and the panel's settings form.</summary>
public interface IWebAdminSettings<T> where T : class
{
    /// <summary>The current value (treat it as read-only).</summary>
    T Current { get; }

    /// <summary>Raised with the new value after the file or the panel changed it (on a pool thread).</summary>
    event Action<T>? Changed;
}

/// <summary>Runs an action; return its result (or null). Throw <see cref="WebAdminRefusal"/> for an answer the user should see.</summary>
public delegate ValueTask<JsonNode?> ActionHandler(ActionContext context);

/// <summary>Answers a query; return the data (or null).</summary>
public delegate ValueTask<JsonNode?> QueryHandler(QueryContext context);

/// <summary>The signed-in user an action or query runs for.</summary>
public sealed class WebAdminUser
{
    private readonly Func<string, bool> _has;

    public WebAdminUser(string id, string name, Func<string, bool> has)
    {
        Id = id;
        Name = name;
        _has = has;
    }

    /// <summary>The account's stable id.</summary>
    public string Id { get; }

    /// <summary>The account's name (as in the audit log).</summary>
    public string Name { get; }

    /// <summary>Whether the user's roles grant <paramref name="permission"/> now.</summary>
    public bool Has(string permission) => _has(permission);
}

public sealed class ActionContext
{
    public required string Module { get; init; }
    public required string Action { get; init; }

    /// <summary>The JSON object the caller sent (already checked against the action's ArgsSchema when it has one).</summary>
    public required JsonObject Args { get; init; }

    public required WebAdminUser User { get; init; }
    public CancellationToken Aborted { get; init; }
}

public sealed class QueryContext
{
    public required string Module { get; init; }
    public required string Query { get; init; }

    /// <summary>The query string's parameters (first value of each).</summary>
    public required IReadOnlyDictionary<string, string> Parameters { get; init; }

    public required WebAdminUser User { get; init; }
    public CancellationToken Aborted { get; init; }
}

/// <summary>A refusal the caller sees: {"error": {"code", "message"}} with <see cref="Status"/> (400, 403, 404, 409 or 422).</summary>
public sealed class WebAdminRefusal : Exception
{
    public WebAdminRefusal(int status, string code, string message) : base(message)
    {
        if (status is not (400 or 403 or 404 or 409 or 422)) throw new ArgumentOutOfRangeException(nameof(status), "use 400, 403, 404, 409 or 422");
        Status = status;
        Code = code;
    }

    public int Status { get; }
    public string Code { get; }
}
