// SPDX-License-Identifier: MIT
using System.Text.Json;

namespace Kilnwarden.Http;

/// <summary>Request bodies as JSON: strict UTF-8 JSON (no comments, no trailing commas, depth ≤ 32), media type
/// application/json. Every failure is an <see cref="HttpError"/> (415 wrong type, 400 unreadable), never another
/// exception type, so a handler can let it propagate.</summary>
public static class JsonBody
{
    public const int MaxDepth = 32;

    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = MaxDepth,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip,
        // enums by the data files' names ("self-signed"); numbers still read
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    public static T Read<T>(HttpRequest request)
    {
        if (!IsJsonMediaType(request.Headers.Get("Content-Type")))
            throw new HttpError(415, "the body must be application/json");
        return Parse<T>(request.Body.Span);
    }

    /// <summary>The parser on its own (fuzz target).</summary>
    public static T Parse<T>(ReadOnlySpan<byte> body)
    {
        if (body.IsEmpty) throw new HttpError(400, "the body is empty");
        try
        {
            var value = JsonSerializer.Deserialize<T>(body, Options) ?? throw new HttpError(400, "the body is null");
            // JsonNode values are read lazily: a duplicate property or invalid UTF-8 in a string would only throw where a
            // handler touches it (a 500); touch everything now so it is a 400 here
            if (value is System.Text.Json.Nodes.JsonNode node) Touch(node, 0);
            return value;
        }
        catch (JsonException e)
        {
            throw new HttpError(400, "the body is not valid JSON" + (e.Path is { } p ? $" (at {p})" : ""));
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException or ArgumentException or FormatException or OverflowException)
        {
            throw new HttpError(400, "the body does not have the expected shape");
        }
    }

    internal static void Touch(System.Text.Json.Nodes.JsonNode? n, int depth)
    {
        if (depth > MaxDepth) throw new FormatException("too deep");
        switch (n)
        {
            case System.Text.Json.Nodes.JsonObject o:
                foreach (var kv in o) Touch(kv.Value, depth + 1);
                break;
            case System.Text.Json.Nodes.JsonArray a:
                foreach (var item in a) Touch(item, depth + 1);
                break;
            case System.Text.Json.Nodes.JsonValue v when v.GetValueKind() == JsonValueKind.String:
                _ = v.GetValue<string>();
                break;
        }
    }

    /// <summary>application/json, optionally with parameters (charset=utf-8); case-insensitive.</summary>
    public static bool IsJsonMediaType(string? contentType)
    {
        if (contentType is null) return false;
        var semi = contentType.IndexOf(';');
        var type = (semi < 0 ? contentType : contentType[..semi]).Trim();
        return type.Equals("application/json", StringComparison.OrdinalIgnoreCase);
    }
}
