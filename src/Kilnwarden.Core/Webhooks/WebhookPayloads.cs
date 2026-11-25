// SPDX-License-Identifier: MIT
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Kilnwarden.Events;

namespace Kilnwarden.Webhooks;

/// <summary>What a webhook carries: only the event's allow-listed fields, primitives only, strings cut to 256
/// characters, the whole body at most 16 KiB. Discord gets no mentions ("allowed_mentions": {"parse": []}, and "@" is
/// broken with a zero-width space). A hook's template may use {{field}}, {{event}} and {{time}}.</summary>
public static partial class WebhookPayloads
{
    public const int MaxBody = 16 << 10, MaxString = 256, MaxTemplate = 500;

    public static JsonObject Fields(PanelEvent e, EventKind kind)
    {
        var o = new JsonObject();
        foreach (var name in kind.Fields)
        {
            if (e.Payload[name] is not JsonValue v) continue;
            o[name] = v.GetValueKind() switch
            {
                JsonValueKind.String => Cut(v.GetValue<string>()),
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => v.DeepClone(),
                _ => null,
            };
        }
        return o;
    }

    public static byte[] Build(string hookKind, string? template, PanelEvent e, EventKind kind, string serverName)
    {
        var fields = Fields(e, kind);
        var text = template is { Length: > 0 } t ? Render(t, e, fields) : null;
        JsonObject body;
        if (hookKind == "discord")
        {
            var embedFields = new JsonArray();
            foreach (var (k, v) in fields.Take(10))
                embedFields.Add(new JsonObject { ["name"] = k, ["value"] = NoMentions(v?.ToString() ?? "-"), ["inline"] = true });
            body = new JsonObject
            {
                ["username"] = NoMentions(Cut(serverName, 80)),
                ["content"] = text is null ? null : NoMentions(Cut(text, 1900)),
                ["allowed_mentions"] = new JsonObject { ["parse"] = new JsonArray() },
                ["embeds"] = new JsonArray(new JsonObject
                {
                    ["title"] = NoMentions(Cut(kind.Title, 200)), ["description"] = e.Topic, ["fields"] = embedFields,
                    ["timestamp"] = e.At.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                }),
            };
        }
        else
        {
            body = new JsonObject
            {
                ["event"] = e.Topic, ["title"] = kind.Title, ["at"] = e.At.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                ["server"] = Cut(serverName, 80), ["data"] = fields, ["text"] = text,
            };
        }
        var bytes = JsonSerializer.SerializeToUtf8Bytes(body);
        if (bytes.Length > MaxBody) throw new InvalidOperationException("the webhook body is over 16 KiB");
        return bytes;
    }

    public static string Render(string template, PanelEvent e, JsonObject fields) =>
        Slot().Replace(template.Length > MaxTemplate ? template[..MaxTemplate] : template, m => m.Groups[1].Value switch
        {
            "event" => e.Topic,
            "time" => e.At.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'"),
            var name => fields[name]?.ToString() ?? "",
        });

    private static string Cut(string s, int max = MaxString) => s.Length <= max ? s : s[..max];

    private static string NoMentions(string s) => s.Replace("@", "@\u200b", StringComparison.Ordinal);

    [GeneratedRegex(@"\{\{([A-Za-z0-9_]{1,64})\}\}")]
    private static partial Regex Slot();
}
