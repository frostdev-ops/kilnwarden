// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;
using Kilnwarden.Security;

namespace Kilnwarden.Modules;

public enum RunOn
{
    /// <summary>On the server's tick thread (through <see cref="Hosting.IGameThread"/>): may touch game state.</summary>
    GameThread,

    /// <summary>On a pool thread: must not touch game state.</summary>
    Background,
}

/// <summary>A named operation of a module (DESIGN §6 AddAction): the panel, declarative pages, saved commands and the API
/// call it as POST /api/&lt;module&gt;/actions/&lt;name&gt; with a JSON object of arguments. The gate checks
/// <see cref="Permission"/>; the audit sink records every call.</summary>
public sealed record ActionSpec(string Name, string Permission, ActionHandler Handler)
{
    public RunOn RunOn { get; init; } = RunOn.Background;
    public string? Title { get; init; }

    /// <summary>The arguments as a JSON Schema (for forms); null: any object.</summary>
    public JsonObject? ArgsSchema { get; init; }

    /// <summary>Argument names whose values never reach the audit log (passwords, tokens).</summary>
    public IReadOnlyList<string> Redact { get; init; } = [];
}

/// <summary>Runs an action. Return the result object (or null); throw <see cref="Http.HttpError"/> for a refusal the
/// caller should see.</summary>
public delegate ValueTask<JsonNode?> ActionHandler(ActionCall call);

public sealed record ActionCall(string Module, string Action, JsonObject Args, IPrincipal User, CancellationToken Aborted);
