// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;

namespace Kilnwarden.Api;

/// <summary>The panel's built-in roles, lowest first. A permission's default role is the lowest that has it until the
/// owner changes the roles.</summary>
public enum WebAdminRole
{
    Viewer = 0,
    Moderator = 1,
    Admin = 2,
    Owner = 3,
}

/// <summary>Where an action or query runs.</summary>
public enum RunOn
{
    /// <summary>A pool thread: must not touch game state.</summary>
    Background,

    /// <summary>The server's tick thread (the synchronous part of the handler): may touch game state.</summary>
    GameThread,
}

/// <summary>A module. <see cref="Id"/>: [a-z0-9][a-z0-9_-]{0,63}, the URL segment and the permission prefix.</summary>
public sealed record ModuleDefinition(string Id, string Title)
{
    /// <summary>A lucide icon name.</summary>
    public string? Icon { get; init; }

    public string? Description { get; init; }
    public bool DefaultEnabled { get; init; } = true;

    /// <summary>Every permission the module uses, each "&lt;id&gt;.&lt;name&gt;".</summary>
    public IReadOnlyList<PermissionDefinition> Permissions { get; init; } = [];

    /// <summary>The topics <see cref="IWebAdminModule.Publish"/> may use.</summary>
    public IReadOnlyList<TopicDefinition> Topics { get; init; } = [];

    /// <summary>The settings as a JSON Schema (object; the subset the panel renders: type, properties, required, enum,
    /// minimum, maximum, minLength, maxLength, items, additionalProperties false, title, description, default). With a
    /// schema the panel shows a settings form behind the permission "&lt;id&gt;.settings" (owner, sensitive), declared for
    /// you.</summary>
    public JsonObject? SettingsSchema { get; init; }
}

/// <summary>A permission: "&lt;module&gt;.&lt;name&gt;".</summary>
public sealed record PermissionDefinition(string Name, string Title)
{
    public string? Description { get; init; }
    public WebAdminRole DefaultRole { get; init; } = WebAdminRole.Admin;

    /// <summary>Dangerous: a write needs the user's recent password or two-factor proof.</summary>
    public bool Sensitive { get; init; }
}

/// <summary>An event topic. Readers need <see cref="Permission"/> (a permission of the module).</summary>
public sealed record TopicDefinition(string Name, string Permission)
{
    public string? Title { get; init; }

    /// <summary>Owners may send it to webhooks (as "&lt;module&gt;.&lt;topic&gt;").</summary>
    public bool Webhooks { get; init; }

    /// <summary>The payload fields a webhook may carry (all others are dropped).</summary>
    public IReadOnlyList<string> WebhookFields { get; init; } = [];
}

public sealed record ActionDefinition(string Name, string Permission)
{
    public string? Title { get; init; }
    public RunOn RunOn { get; init; } = RunOn.Background;

    /// <summary>The arguments as a JSON Schema object (forms are built from it; calls are checked against it).</summary>
    public JsonObject? ArgsSchema { get; init; }

    /// <summary>Argument names whose values never reach the audit log.</summary>
    public IReadOnlyList<string> Redact { get; init; } = [];

    /// <summary>The panel asks "are you sure?" with this text before it runs the action.</summary>
    public string? Confirm { get; init; }
}

public sealed record QueryDefinition(string Name, string Permission)
{
    public string? Title { get; init; }
    public RunOn RunOn { get; init; } = RunOn.Background;
}

/// <summary>A page in the navigation. <see cref="Permission"/>: who sees it (null: any signed-in user).</summary>
public abstract record PageDefinition(string Id, string Title)
{
    public string? Icon { get; init; }
    public string? Permission { get; init; }
    public int Order { get; init; } = 500;
}

/// <summary>A page of blocks bound to the module's actions and queries: no JavaScript from the mod.</summary>
public sealed record DeclarativePage(string Id, string Title, IReadOnlyList<PageBlock> Blocks) : PageDefinition(Id, Title);

/// <summary>The mod's own page: <see cref="Script"/> (an ES module's source, at most 512 KiB) runs in a sandboxed iframe
/// with no cookies and no network. It talks to the panel through <c>window.kiln</c> (call, query, subscribe, theme), which
/// only reaches this module's own actions, queries and topics, with the user's permissions.</summary>
public sealed record CustomPage(string Id, string Title, string Script) : PageDefinition(Id, Title)
{
    /// <summary>CSS for the page (at most 128 KiB).</summary>
    public string? Style { get; init; }
}

/// <summary>A declarative page's block.</summary>
public abstract record PageBlock;

/// <summary>A number from a query's result: <see cref="Field"/> is a dotted path ("stats.count").</summary>
public sealed record StatBlock(string Title, string Query, string Field) : PageBlock
{
    public string? Unit { get; init; }
}

/// <summary>A table of a query's rows (the array at <see cref="RowsField"/>, or the result itself when it is an array).</summary>
public sealed record TableBlock(string Query, IReadOnlyList<TableColumn> Columns) : PageBlock
{
    public string? Title { get; init; }
    public string? RowsField { get; init; }
}

public sealed record TableColumn(string Field, string Title);

/// <summary>A form for an action, built from the action's ArgsSchema.</summary>
public sealed record FormBlock(string Action) : PageBlock
{
    public string? Title { get; init; }
    public string? Submit { get; init; }
}

/// <summary>A button that runs an action with fixed arguments.</summary>
public sealed record ButtonBlock(string Action, string Label) : PageBlock
{
    public JsonObject? Args { get; init; }
}

/// <summary>Plain text (no HTML, no markdown).</summary>
public sealed record TextBlock(string Text) : PageBlock;

public enum WidgetSurface
{
    Dashboard,
    Public,
}

/// <summary>A widget. Dashboard widgets show a query's result (<see cref="Query"/>, gated on the query's permission).
/// Public widgets show what <see cref="PublicData"/> returns (anonymous viewers: return nothing private; at most 8 KiB,
/// cached with the public page); the owner turns each public widget on.</summary>
public sealed record WidgetDefinition(string Id, string Title, WidgetSurface Surface)
{
    /// <summary>"stat" (a number at <see cref="Field"/>), "table" or "text".</summary>
    public string Kind { get; init; } = "stat";

    public string? Query { get; init; }
    public string? Field { get; init; }
    public Func<JsonNode?>? PublicData { get; init; }
}
