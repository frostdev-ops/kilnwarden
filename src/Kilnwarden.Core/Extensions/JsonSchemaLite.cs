// SPDX-License-Identifier: MIT
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Kilnwarden.Extensions;

/// <summary>The JSON Schema subset extension settings and action arguments use (and the web app renders as forms):
/// type (object, string, integer, number, boolean, array), properties, required, additionalProperties (false only),
/// enum, minimum, maximum, minLength, maxLength, items, maxItems. Other keywords (title, description, default, format...)
/// are carried for the form and not checked. No pattern (no regex on user input, no ReDoS). Bounded: depth 16, 256
/// problems at most.</summary>
public static class JsonSchemaLite
{
    public const int MaxDepth = 16;

    private static readonly HashSet<string> Types = ["object", "string", "integer", "number", "boolean", "array"];

    /// <summary>Problems of the schema itself (empty: usable).</summary>
    public static IReadOnlyList<string> CheckSchema(JsonObject schema)
    {
        var problems = new List<string>();
        CheckSchema(schema, "$", 0, problems);
        return problems;
    }

    private static void CheckSchema(JsonNode? node, string at, int depth, List<string> problems)
    {
        if (depth > MaxDepth) { problems.Add($"{at}: nested too deep"); return; }
        if (node is not JsonObject s) { problems.Add($"{at}: a schema must be an object"); return; }
        if (s["type"] is JsonNode t && (t.GetValueKind() != JsonValueKind.String || !Types.Contains(t.GetValue<string>())))
            problems.Add($"{at}: type must be one of {string.Join(", ", Types)}");
        if (s["properties"] is JsonNode ps)
        {
            if (ps is not JsonObject po) problems.Add($"{at}.properties: must be an object");
            else foreach (var (k, v) in po) CheckSchema(v, $"{at}.{k}", depth + 1, problems);
        }
        if (s["items"] is JsonNode items) CheckSchema(items, $"{at}[]", depth + 1, problems);
        if (s["required"] is JsonNode req && (req is not JsonArray ra || ra.Any(x => x?.GetValueKind() != JsonValueKind.String)))
            problems.Add($"{at}.required: must be an array of names");
        if (s["enum"] is JsonNode en && (en is not JsonArray ea || ea.Count == 0)) problems.Add($"{at}.enum: must be a non-empty array");
        if (s["additionalProperties"] is JsonNode ap && ap.GetValueKind() is not (JsonValueKind.False or JsonValueKind.True))
            problems.Add($"{at}.additionalProperties: only true or false");
        if (s["pattern"] is not null) problems.Add($"{at}.pattern: not supported");
    }

    /// <summary>Problems of <paramref name="value"/> against <paramref name="schema"/> (empty: valid).</summary>
    public static IReadOnlyList<string> Validate(JsonObject schema, JsonNode? value)
    {
        var problems = new List<string>();
        Validate(schema, value, "$", 0, problems);
        return problems;
    }

    private static void Validate(JsonObject s, JsonNode? v, string at, int depth, List<string> problems)
    {
        if (problems.Count >= 256 || depth > MaxDepth) return;
        var kind = v?.GetValueKind() ?? JsonValueKind.Null;
        if (s["type"]?.GetValue<string>() is { } type)
        {
            var ok = type switch
            {
                "object" => kind == JsonValueKind.Object,
                "array" => kind == JsonValueKind.Array,
                "string" => kind == JsonValueKind.String,
                "boolean" => kind is JsonValueKind.True or JsonValueKind.False,
                "number" => kind == JsonValueKind.Number,
                "integer" => kind == JsonValueKind.Number && IsInteger(v!),
                _ => false,
            };
            if (!ok)
            {
                problems.Add($"{at}: must be {(type is "integer" or "array" or "object" ? "an" : "a")} {type}");
                return;
            }
        }
        if (s["enum"] is JsonArray en && !en.Any(e => JsonNode.DeepEquals(e, v))) problems.Add($"{at}: must be one of the listed values");
        if (kind == JsonValueKind.Number)
        {
            var d = Num(v)!.Value;
            if (Num(s["minimum"]) is { } min && d < min) problems.Add($"{at}: must be at least {min}");
            if (Num(s["maximum"]) is { } max && d > max) problems.Add($"{at}: must be at most {max}");
        }
        if (kind == JsonValueKind.String)
        {
            var len = v!.GetValue<string>().Length;
            if (Num(s["minLength"]) is { } min && len < min) problems.Add($"{at}: at least {min} characters");
            if (Num(s["maxLength"]) is { } max && len > max) problems.Add($"{at}: at most {max} characters");
        }
        if (v is JsonArray arr)
        {
            if (Num(s["maxItems"]) is { } maxItems && arr.Count > maxItems) problems.Add($"{at}: at most {maxItems} items");
            if (s["items"] is JsonObject item)
                for (var i = 0; i < arr.Count; i++) Validate(item, arr[i], $"{at}[{i}]", depth + 1, problems);
        }
        if (v is JsonObject obj)
        {
            var props = s["properties"] as JsonObject;
            if (s["required"] is JsonArray req)
                foreach (var r in req)
                    if (r?.GetValue<string>() is { } name && !obj.ContainsKey(name)) problems.Add($"{at}.{name}: required");
            foreach (var (k, child) in obj)
            {
                if (props?[k] is JsonObject ps) Validate(ps, child, $"{at}.{k}", depth + 1, problems);
                else if (s["additionalProperties"]?.GetValueKind() == JsonValueKind.False) problems.Add($"{at}.{k}: not allowed");
            }
        }
    }

    private static bool IsInteger(JsonNode v) => Num(v) is { } d && IsWhole(d) && Math.Abs(d) <= 9007199254740991;

    /// <summary>Whether &lt;paramref name="d"/&gt; is a whole number, read from its bits: no floating-point arithmetic, so the
    /// answer does not depend on the thread's SSE mode. (A game engine runs its threads with flush-to-zero and
    /// denormals-are-zero; Kiln reloads mods on such a thread. The old check, d % 1 &lt; double.Epsilon, compared with a
    /// subnormal that reads as 0 there, so every whole number failed and hot-reloaded settings fell back to defaults.)
    /// Subnormals (non-zero, below 1) are not whole.</summary>
    internal static bool IsWhole(double d)
    {
        var bits = BitConverter.DoubleToInt64Bits(d);
        var exp = (int)((bits >> 52) & 0x7FF);
        var mantissa = bits & 0xF_FFFF_FFFF_FFFFL;
        if (exp == 0x7FF) return false;                // infinity, NaN
        if (exp == 0) return mantissa == 0;            // zero; a subnormal is a fraction
        var shift = 1075 - exp;                        // fraction bits of the 53-bit significand
        if (shift <= 0) return true;
        if (shift > 52) return false;                  // 0 < |d| < 1
        return (mantissa & ((1L << shift) - 1)) == 0;
    }

    /// <summary>A number node as a double, whatever backs it (parsed JSON or a C# int/long/double the mod put in).</summary>
    internal static double? Num(JsonNode? n) =>
        n is JsonValue v && v.GetValueKind() == JsonValueKind.Number &&
        double.TryParse(v.ToJsonString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
}
