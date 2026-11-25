// SPDX-License-Identifier: MIT
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Kilnwarden.Events;
using Kilnwarden.Storage;

namespace Kilnwarden.Webhooks;

/// <summary>One webhook. &lt;see cref="Url"/&gt; is a secret (a Discord URL carries its token): stored in webhooks.json
/// (written as a secret file), never sent back whole.</summary>
public sealed class WebhookHook
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>"discord" or "json".</summary>
    public string Kind { get; set; } = "discord";

    public string Url { get; set; } = "";

    /// <summary>Topics ("player.join") or prefixes ("player.*", "*" for all).</summary>
    public List<string> Events { get; set; } = [];

    public bool Enabled { get; set; } = true;
    public string? Template { get; set; }

    public bool Wants(string topic) => Events.Any(p => p == "*" || p == topic || p.EndsWith(".*", StringComparison.Ordinal) && topic.StartsWith(p[..^1], StringComparison.Ordinal));
}

/// <summary>webhooks.json: the hooks (opt-in: none by default). Loaded at start, saved atomically as a secret file on
/// every change through the panel; not hot reloaded (edit through the panel).</summary>
public sealed partial class WebhookStore
{
    public const string File = "webhooks.json";
    public const int MaxHooks = 32;

    private readonly DataStore _data;
    private readonly object _gate = new();
    private List<WebhookHook> _hooks = [];

    public WebhookStore(DataStore data)
    {
        _data = data;
        if (data.ReadJson<StoreFile>(File) is { } f) _hooks = f.Hooks ?? [];
    }

    public event Action? Changed;

    public IReadOnlyList<WebhookHook> All
    {
        get
        {
            lock (_gate) return [.. _hooks];
        }
    }

    public WebhookHook? Find(string id)
    {
        lock (_gate) return _hooks.FirstOrDefault(h => h.Id == id);
    }

    public static string NewId() => "h-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));

    /// <summary>Problems of <paramref name="h"/> (empty: valid). <paramref name="known"/>: the event topics that exist.</summary>
    public static List<string> Check(WebhookHook h, IReadOnlyList<EventKind> known, bool allowPrivate = false)
    {
        var p = new List<string>();
        if (string.IsNullOrWhiteSpace(h.Name) || h.Name.Length > 64) p.Add("name: 1 to 64 characters");
        if (WebhookGuard.CheckUrl(h.Kind, h.Url, out _, allowPrivate) is { } bad) p.Add(bad);
        if (h.Events is null || h.Events.Count is 0 or > 64) p.Add("events: 1 to 64 entries");
        else
            foreach (var e in h.Events)
            {
                var prefix = e.EndsWith(".*", StringComparison.Ordinal) ? e[..^1] : null;
                if (e != "*" && !(prefix is not null ? known.Any(k => k.Topic.StartsWith(prefix, StringComparison.Ordinal)) && TopicRe().IsMatch(e[..^2])
                                                       : known.Any(k => k.Topic == e)))
                    p.Add($"events: unknown event '{(e.Length > 64 ? e[..64] : e)}'");
            }
        if (h.Template is { Length: > WebhookPayloads.MaxTemplate }) p.Add($"template: at most {WebhookPayloads.MaxTemplate} characters");
        return p;
    }

    public void Save(Func<List<WebhookHook>, List<WebhookHook>> change)
    {
        lock (_gate)
        {
            var next = change([.. _hooks]);
            if (next.Count > MaxHooks) throw new ArgumentException($"at most {MaxHooks} webhooks");
            _data.WriteJson(File, new StoreFile { Hooks = next }, secret: true);
            _hooks = next;
        }
        Changed?.Invoke();
    }

    private sealed class StoreFile
    {
        public int Format { get; set; } = 1;
        public List<WebhookHook>? Hooks { get; set; } = [];
    }

    [GeneratedRegex("^[a-z0-9_-]+(\\.[a-z0-9_-]+)*$")]
    private static partial Regex TopicRe();
}
