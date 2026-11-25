// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Kilnwarden.Modules;

/// <summary>A module (DESIGN §5): every page, widget, action and background job belongs to one. First-party modules and
/// (from step 2) other mods' modules through Kilnwarden.Api use this same registry, so this record is the shape the
/// public API's ModuleSpec maps onto: plain data, no Kiln types.</summary>
public sealed partial record ModuleSpec
{
    /// <summary>[a-z0-9][a-z0-9_-]{0,63}: the URL segment (/api/&lt;id&gt;/...) and the permission prefix ("&lt;id&gt;.").</summary>
    public required string Id { get; init; }

    public required string Title { get; init; }

    /// <summary>An icon name from the web app's set (lucide names), or null.</summary>
    public string? Icon { get; init; }

    public string? Description { get; init; }

    /// <summary>On when the owner has not switched it either way.</summary>
    public bool DefaultEnabled { get; init; } = true;

    /// <summary>False for modules the panel cannot work without (core, users and roles).</summary>
    public bool CanDisable { get; init; } = true;

    /// <summary>Every permission the module's routes, actions and pages use, each named "&lt;id&gt;.&lt;rest&gt;".</summary>
    public IReadOnlyList<PermissionSpec> Permissions { get; init; } = [];

    /// <summary>The module's settings as a JSON Schema (draft 2020-12 subset the web app renders as a form); null: none.</summary>
    public JsonObject? SettingsSchema { get; init; }

    public IReadOnlyList<PageSpec> Pages { get; init; } = [];
    public IReadOnlyList<WidgetSpec> Widgets { get; init; } = [];

    public static bool IsModuleId(string? s) => s is not null && IdRe().IsMatch(s);

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,63}$")]
    private static partial Regex IdRe();
}

/// <summary>The built-in roles, lowest first (DESIGN §3). A permission's <see cref="PermissionSpec.DefaultRole"/> is the
/// lowest built-in role that has it until the owner changes the roles; custom roles start with nothing.</summary>
public enum BuiltinRole
{
    Viewer = 0,
    Moderator = 1,
    Admin = 2,
    Owner = 3,
}

/// <summary>A permission a module declares: "players.kill", "core.settings.edit".</summary>
public sealed record PermissionSpec(string Name, string Title)
{
    public string? Description { get; init; }
    public BuiltinRole DefaultRole { get; init; } = BuiltinRole.Admin;

    /// <summary>Dangerous enough that the panel asks again (and step-up 2FA may apply): server stop, console, mod install.</summary>
    public bool Sensitive { get; init; }
}

public enum PageKind
{
    /// <summary>A page the web app ships (Players, Settings, Users...): <see cref="PageSpec.Entry"/> is its route name.</summary>
    Builtin,

    /// <summary>Declarative (no JS): tables, forms, stat cards and buttons bound to the module's actions. Entry: the
    /// layout's API path. Step 2.</summary>
    Declarative,

    /// <summary>The mod's own ES-module bundle in a sandboxed iframe (postMessage host API). Entry: the bundle's path
    /// under the module's static root. Step 2.</summary>
    Custom,
}

/// <summary>A page in the panel's navigation.</summary>
public sealed record PageSpec(string Id, string Title, PageKind Kind, string Entry)
{
    public string? Icon { get; init; }

    /// <summary>Who sees the page (the UI hides it otherwise; its routes are gated on their own). Null: any signed-in user.</summary>
    public string? Permission { get; init; }

    /// <summary>Position in the navigation; lower first.</summary>
    public int Order { get; init; } = 100;
}

public enum WidgetSurface
{
    Dashboard,
    Public,
}

/// <summary>A dashboard or public-page widget. Public widgets get only what their data route returns to anonymous
/// callers: they must be Public routes of their own that expose no admin data.</summary>
public sealed record WidgetSpec(string Id, string Title, WidgetSurface Surface, string DataPath)
{
    public string? Permission { get; init; }
    public string? Kind { get; init; }

    /// <summary>A stat widget's value: a dotted path into its data (lane C).</summary>
    public string? Field { get; init; }
}
