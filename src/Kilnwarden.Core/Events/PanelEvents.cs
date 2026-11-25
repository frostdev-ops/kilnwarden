// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;
using Kilnwarden.Hosting;

namespace Kilnwarden.Events;

/// <summary>An event inside the panel: a player joined, a metric alert fired, an extension published. Webhooks pick the
/// ones their owner chose. &lt;see cref="Fields"/&gt;: the payload fields that may leave the panel (webhooks copy only
/// these).</summary>
public sealed record PanelEvent(string Topic, DateTimeOffset At, JsonObject Payload);

/// <summary>A kind of event webhooks can subscribe to.</summary>
/// <param name="Topic">"player.join", "metrics.alert", "&lt;module&gt;.&lt;topic&gt;".</param>
/// <param name="Fields">The payload fields that may leave the panel (an allow-list; the rest is dropped).</param>
/// <param name="Owner">The component or extension mod that declared it.</param>
public sealed record EventKind(string Topic, string Title, IReadOnlyList<string> Fields, string Owner);

/// <summary>The panel's event bus (lane C): components and extensions publish, webhooks subscribe. Publishing never
/// blocks or throws: subscribers must queue. A topic must be declared (its field allow-list travels with it).
/// Shared through the services; &lt;see cref="Of"/&gt; makes it on first use, so no composition root is needed.</summary>
public sealed class PanelEvents
{
    private readonly object _gate = new();
    private readonly IWebAdminLog? _log;
    private Dictionary<string, EventKind> _kinds = new(StringComparer.Ordinal);

    public PanelEvents(IWebAdminLog? log = null) => _log = log;

    /// <summary>The bus of this host (made and registered on first use; Configure runs on one thread).</summary>
    public static PanelEvents Of(WebAdminServices services, IWebAdminLog? log = null)
    {
        if (services.TryGet<PanelEvents>() is { } e) return e;
        var made = new PanelEvents(log);
        services.Add(made);
        return made;
    }

    public event Action<PanelEvent, EventKind>? Published;

    public IReadOnlyList<EventKind> Kinds
    {
        get
        {
            lock (_gate) return [.. _kinds.Values.OrderBy(k => k.Topic, StringComparer.Ordinal)];
        }
    }

    public EventKind? Find(string topic)
    {
        lock (_gate) return _kinds.GetValueOrDefault(topic);
    }

    public void Declare(EventKind kind)
    {
        lock (_gate)
        {
            if (_kinds.TryGetValue(kind.Topic, out var had) && had.Owner != kind.Owner)
                throw new InvalidOperationException($"event '{kind.Topic}' is declared by {had.Owner}");
            _kinds = new(_kinds, StringComparer.Ordinal) { [kind.Topic] = kind };
        }
    }

    /// <summary>Removes every kind <paramref name="owner"/> declared (an extension that unloads).</summary>
    public int Forget(string owner)
    {
        lock (_gate)
        {
            var keep = _kinds.Where(kv => kv.Value.Owner != owner).ToDictionary(StringComparer.Ordinal);
            var n = _kinds.Count - keep.Count;
            _kinds = keep;
            return n;
        }
    }

    /// <summary>Publishes a declared event; an undeclared topic is dropped (false).</summary>
    public bool Publish(string topic, JsonObject? payload, DateTimeOffset? at = null)
    {
        var kind = Find(topic);
        if (kind is null) return false;
        var e = new PanelEvent(topic, at ?? DateTimeOffset.UtcNow, payload ?? []);
        foreach (var d in Published?.GetInvocationList() ?? [])
        {
            try
            {
                ((Action<PanelEvent, EventKind>)d)(e, kind);
            }
            catch (Exception ex)
            {
                _log?.Error($"event {topic}: a subscriber failed", ex);
            }
        }
        return true;
    }
}
