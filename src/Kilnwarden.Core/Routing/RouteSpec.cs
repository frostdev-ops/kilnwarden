// SPDX-License-Identifier: MIT
using Kilnwarden.WebSockets;

namespace Kilnwarden.Routing;

/// <summary>One route of the table: a method, a full path pattern, who may call it and what runs.
/// Patterns are '/'-separated segments: literals, "{name}" (one segment) and a final "{*name}" (the rest). Matching is
/// case-sensitive on the decoded segments; literals win over parameters.</summary>
public sealed record RouteSpec
{
    /// <summary>"GET", "POST", "PUT", "PATCH", "DELETE"; WebSocket routes are GET with <see cref="WebSocket"/> set. HEAD
    /// matches GET routes.</summary>
    public required string Method { get; init; }

    public required string Pattern { get; init; }
    public required RouteAccess Access { get; init; }

    /// <summary>The module that owns the route (its on/off switch applies: a disabled module's routes answer 404).</summary>
    public required string ModuleId { get; init; }

    public RouteHandler? Handler { get; init; }
    public WebSocketHandler? WebSocket { get; init; }

    /// <summary>The body cap for this route; null: settings.json limits.maxBodyBytes.</summary>
    public long? MaxBodyBytes { get; init; }

    public override string ToString() => $"{(WebSocket is not null ? "WS" : Method)} {Pattern} ({Access}, module {ModuleId})";
}

/// <summary>A matched route with its decoded values.</summary>
public sealed record RouteMatch(RouteSpec Route, IReadOnlyDictionary<string, string> Values);
