// SPDX-License-Identifier: MIT
using System.Net;
using System.Text.Json;

namespace Kilnwarden.Relay;

/// <summary>The relay's messages on the control link (docs/protocol-relay-broker.md §3): announcements of browser
/// connections, and, since 0.1.1 (the mod asks with <c>X-Kwa-Notices: 1</c>), notices about this server's name. A
/// notice of a kind this version does not know, or a notice it can't read, is ignored: a notice never ends the link,
/// so a later relay may send new ones to this version. An announcement that is not exactly its shape still ends it.</summary>
internal static class RelayControl
{
    internal abstract record Message;

    /// <summary>A browser connection to open a data link for.</summary>
    internal sealed record Announce(string Id, IPAddress Peer, int Port) : Message;

    /// <summary>The name's scheduled move or release (lane L1b), or none (<see cref="Change"/> null). <see cref="Done"/>:
    /// the change took effect (the relay's last notice on this link, which ends right after).</summary>
    internal sealed record NameNotice(string Host, Scheduled? Change, Done? Done = null) : Message;

    /// <summary>The name moved to the key <see cref="To"/> (null: it was released) at <see cref="At"/>.</summary>
    internal sealed record Done(string? To, DateTimeOffset At)
    {
        public bool IsRelease => To is null;
    }

    /// <summary>A notice this version does not take.</summary>
    internal sealed record Ignored(string Why) : Message;

    /// <summary>A move to the key <see cref="To"/> (SHA-256 hex of its SPKI), or a release (<see cref="To"/> null),
    /// asked at <see cref="Requested"/>, taking effect at <see cref="At"/> unless undone or canceled first.</summary>
    internal sealed record Scheduled(string? To, DateTimeOffset Requested, DateTimeOffset At)
    {
        public bool IsRelease => To is null;
    }

    /// <summary>One complete text message. Throws <see cref="IOException"/> for one that is neither a notice nor a
    /// well-formed announcement.</summary>
    public static Message Parse(ReadOnlySpan<byte> json)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json.ToArray(), new JsonDocumentOptions { MaxDepth = 8 }); }
        catch (JsonException) { throw new IOException("Invalid relay control message."); }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new IOException("Invalid relay control message.");
            if (Get(root, "notice") is { } kind) return Notice(root, kind);
            return Announcement(root);
        }
    }

    private static JsonElement? Get(JsonElement o, string name)
    {
        foreach (var p in o.EnumerateObject())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                return p.Value;
        return null;
    }

    private static Message Notice(JsonElement root, JsonElement kind)
    {
        if (kind.ValueKind != JsonValueKind.String) return new Ignored("a notice without a kind");
        if (kind.GetString() != "name") return new Ignored($"notice {Short(kind.GetString())}");
        if (Get(root, "host") is not { ValueKind: JsonValueKind.String } h || h.GetString() is not { Length: > 0 and <= 253 } host)
            return new Ignored("a name notice without its host");
        if (Get(root, "done") is { ValueKind: not JsonValueKind.Null } d)
        {
            if (d.ValueKind != JsonValueKind.Object) return new Ignored("a name notice with a bad done");
            var (ok, to) = Key(d);
            if (!ok || Time(d, "at") is not { } at) return new Ignored("a name notice with a bad done");
            return new NameNotice(host, null, new Done(to, at));
        }
        switch (Get(root, "scheduled"))
        {
            case null or { ValueKind: JsonValueKind.Null }:
                return new NameNotice(host, null);
            case { ValueKind: JsonValueKind.Object } c:
                var (good, to) = Key(c);
                if (!good) return new Ignored("a name notice with a bad key");
                if (Time(c, "requested") is not { } requested || Time(c, "at") is not { } at) return new Ignored("a name notice without its times");
                return new NameNotice(host, new Scheduled(to, requested, at));
            default:
                return new Ignored("a name notice with a bad change");
        }
    }

    /// <summary>An object's <c>to</c>: absent or null (a release), or a key's fingerprint (64 lower-case hex).</summary>
    private static (bool Ok, string? To) Key(JsonElement o)
    {
        if (Get(o, "to") is not { } t || t.ValueKind == JsonValueKind.Null) return (true, null);
        return t.ValueKind == JsonValueKind.String && t.GetString() is { Length: 64 } fp && fp.All(char.IsAsciiHexDigitLower) ? (true, fp) : (false, null);
    }

    private static DateTimeOffset? Time(JsonElement o, string name) =>
        Get(o, name) is { ValueKind: JsonValueKind.Number } v && v.TryGetInt64(out var s) && s is > 0 and < 253_402_300_800
            ? DateTimeOffset.FromUnixTimeSeconds(s)
            : null;

    private static string Short(string? s) => s is null ? "?" : s.Length <= 32 ? s : s[..32];

    private static Announce Announcement(JsonElement root)
    {
        string? id = null, ip = null;
        int port = 0;
        try
        {
            if (Get(root, "id") is { ValueKind: JsonValueKind.String } i) id = i.GetString();
            if (Get(root, "ip") is { ValueKind: JsonValueKind.String } a) ip = a.GetString();
            if (Get(root, "port") is { ValueKind: JsonValueKind.Number } p && p.TryGetInt32(out var n)) port = n;
        }
        catch (InvalidOperationException) { }
        if (id is not { Length: 32 } || !id.All(char.IsAsciiHexDigitLower) || ip is null || port is <= 0 or > 65535 || !IPAddress.TryParse(ip, out var peer))
            throw new IOException("Invalid relay peer.");
        return new Announce(id, peer, port);
    }
}
