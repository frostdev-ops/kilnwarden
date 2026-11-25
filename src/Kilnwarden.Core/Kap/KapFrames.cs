// SPDX-License-Identifier: MIT
using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Kilnwarden.Kap;

/// <summary>A KAP/1 frame that breaks the protocol (admin-protocol.md §2): the connection is closed.</summary>
public sealed class KapProtocolException(string code, string message) : Exception(message)
{
    /// <summary>The KAP error code: bad_frame, too_large, version, auth_failed...</summary>
    public string Code { get; } = code;
}

/// <summary>KAP/1 framing (admin-protocol.md §2): a u32 little-endian payload length, then one UTF-8 JSON object with a
/// string "t". The same payload is one WebSocket message on the web side (§12). Parsing is strict: valid UTF-8, an
/// object at the top, no duplicate keys, depth ≤ 64, no comments or trailing commas.</summary>
public static class KapFrames
{
    /// <summary>The largest frame a client may send (Kiln.Admin closes on bigger ones).</summary>
    public const int MaxClientFrame = 1 << 20;

    /// <summary>The largest frame Kiln.Admin sends (ready.limits.maxFrame may say less).</summary>
    public const int MaxServerFrame = 16 << 20;

    public const int MaxDepth = 64;

    private static readonly JsonDocumentOptions DocOptions = new()
    {
        MaxDepth = MaxDepth,
        CommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        AllowDuplicateProperties = false,
    };

    /// <summary>The payload of <paramref name="frame"/> as UTF-8 (no length prefix).</summary>
    public static byte[] Payload(JsonObject frame) => JsonSerializer.SerializeToUtf8Bytes(frame);

    /// <summary>The frame with its length prefix. Throws <see cref="KapProtocolException"/> (too_large) over
    /// <paramref name="max"/>.</summary>
    public static byte[] Encode(JsonObject frame, int max = MaxClientFrame)
    {
        var payload = Payload(frame);
        if (payload.Length > max) throw new KapProtocolException("too_large", $"a {payload.Length}-byte frame is over the {max}-byte limit");
        var buf = new byte[4 + payload.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, (uint)payload.Length);
        payload.CopyTo(buf.AsSpan(4));
        return buf;
    }

    /// <summary>Reads a length prefix. Throws <see cref="KapProtocolException"/>: bad_frame for 0, too_large over
    /// <paramref name="max"/>.</summary>
    public static int Length(ReadOnlySpan<byte> head, int max)
    {
        var len = BinaryPrimitives.ReadUInt32LittleEndian(head);
        if (len == 0) throw new KapProtocolException("bad_frame", "a frame of length 0");
        if (len > (uint)max) throw new KapProtocolException("too_large", $"a {len}-byte frame is over the {max}-byte limit");
        return (int)len;
    }

    /// <summary>Parses one payload. Throws <see cref="KapProtocolException"/> (bad_frame) for anything but a JSON object
    /// with a string "t"; never another exception type (fuzz target).</summary>
    public static JsonObject Decode(ReadOnlySpan<byte> payload)
    {
        var obj = ParseObject(payload);
        if (Str(obj, "t") is null) throw new KapProtocolException("bad_frame", "a frame needs a string \"t\"");
        return obj;
    }

    /// <summary>One strict JSON object (frames, endpoint.json). Throws <see cref="KapProtocolException"/> (bad_frame) only.</summary>
    public static JsonObject ParseObject(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty) throw new KapProtocolException("bad_frame", "an empty frame");
        // the reader checks string bytes lazily (a bad property name would throw at first access): check them all now
        if (!System.Text.Unicode.Utf8.IsValid(payload)) throw new KapProtocolException("bad_frame", "not UTF-8");
        try
        {
            using var doc = JsonDocument.Parse(payload.ToArray(), DocOptions);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new KapProtocolException("bad_frame", "a frame must be a JSON object");
            CheckStrings(doc.RootElement);
            return JsonObject.Create(doc.RootElement.Clone())!;
        }
        catch (InvalidOperationException e)
        {
            // a string escape that is not text ("\ud800" alone): the reader only notices when the string is read
            throw new KapProtocolException("bad_frame", "not JSON text: " + e.Message);
        }
        catch (JsonException e)
        {
            throw new KapProtocolException("bad_frame", "not JSON: " + e.Message);
        }
        catch (ArgumentException e)
        {
            // invalid UTF-8 or a key twice, depending on where the reader noticed it
            throw new KapProtocolException("bad_frame", "not JSON: " + e.Message);
        }
    }

    /// <summary>Reads every property name and string once, so a lone surrogate escape fails here and not later in the
    /// code that reads the field.</summary>
    private static void CheckStrings(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject())
                {
                    _ = p.Name;
                    CheckStrings(p.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (var x in e.EnumerateArray()) CheckStrings(x);
                break;
            case JsonValueKind.String:
                _ = e.GetString();
                break;
        }
    }

    /// <summary>A string field, or null when it is missing or not a string.</summary>
    public static string? Str(JsonObject f, string name) =>
        f.TryGetPropertyValue(name, out var v) && v is JsonValue jv && jv.GetValueKind() == JsonValueKind.String ? jv.GetValue<string>() : null;

    /// <summary>An unsigned integer id (KAP ids are unsigned integers), or null.</summary>
    public static ulong? Id(JsonObject f, string name = "id")
    {
        if (!f.TryGetPropertyValue(name, out var v) || v is not JsonValue jv || jv.GetValueKind() != JsonValueKind.Number) return null;
        return jv.TryGetValue<ulong>(out var u) ? u : null;
    }

    public static bool? Bool(JsonObject f, string name) =>
        f.TryGetPropertyValue(name, out var v) && v is JsonValue jv && jv.GetValueKind() is JsonValueKind.True or JsonValueKind.False
            ? jv.GetValue<bool>() : null;

    public static int? Int(JsonObject f, string name) =>
        f.TryGetPropertyValue(name, out var v) && v is JsonValue jv && jv.GetValueKind() == JsonValueKind.Number && jv.TryGetValue<int>(out var i)
            ? i : null;
}
