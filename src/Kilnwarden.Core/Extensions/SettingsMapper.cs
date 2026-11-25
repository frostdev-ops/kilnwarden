// SPDX-License-Identifier: MIT
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Kilnwarden.Extensions;

/// <summary>Maps an extension's settings type to and from its JSON object without System.Text.Json's reflection
/// serializer, whose process-wide member-accessor cache would keep a mod's collectible types (and its load context)
/// alive after the mod unloads. Supported: public instance properties with a public getter and setter of type string,
/// bool, int, long, double, float, decimal, an enum (as its name), string[] or List&lt;string&gt; (and nullable forms);
/// JSON names are camelCase. Anything else is refused when the module asks for its settings.</summary>
internal static class SettingsMapper
{
    public static void Check(Type t)
    {
        if (t.GetConstructor(Type.EmptyTypes) is null) throw new ArgumentException($"{t.Name}: settings need a public parameterless constructor");
        foreach (var p in Props(t))
            if (!Supported(p.PropertyType)) throw new ArgumentException($"{t.Name}.{p.Name}: settings properties are string, bool, numbers, enums or string lists");
    }

    private static IEnumerable<PropertyInfo> Props(Type t) =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetMethod?.IsPublic == true && p.SetMethod?.IsPublic == true && p.GetIndexParameters().Length == 0);

    private static bool Supported(Type t)
    {
        t = Nullable.GetUnderlyingType(t) ?? t;
        return t == typeof(string) || t == typeof(bool) || t == typeof(int) || t == typeof(long) || t == typeof(double) || t == typeof(float) ||
               t == typeof(decimal) || t.IsEnum || t == typeof(string[]) || t == typeof(List<string>);
    }

    public static string Name(PropertyInfo p) => JsonNamingPolicy.CamelCase.ConvertName(p.Name);

    public static JsonObject ToJson(object value)
    {
        var o = new JsonObject();
        foreach (var p in Props(value.GetType()))
        {
            var v = p.GetValue(value);
            o[Name(p)] = v switch
            {
                null => null,
                string s => s,
                bool b => b,
                int i => i,
                long l => l,
                double d => d,
                float f => f,
                decimal m => m,
                Enum e => e.ToString(),
                IEnumerable<string> list => new JsonArray([.. list.Select(x => (JsonNode?)x)]),
                _ => throw new ArgumentException($"{p.Name}: unsupported"),
            };
        }
        return o;
    }

    /// <summary>A new T from <paramref name="o"/>: properties the object names are set (case-insensitive), the rest keep
    /// their defaults; a value of the wrong kind throws FormatException.</summary>
    public static T FromJson<T>(JsonObject o) where T : class, new()
    {
        var t = new T();
        var props = Props(typeof(T)).ToDictionary(Name, StringComparer.OrdinalIgnoreCase);
        foreach (var (k, node) in o)
        {
            if (!props.TryGetValue(k, out var p)) continue;
            p.SetValue(t, Convert(node, p.PropertyType, p.Name));
        }
        return t;
    }

    private static object? Convert(JsonNode? n, Type type, string name)
    {
        var under = Nullable.GetUnderlyingType(type);
        if (n is null) return under is not null || !type.IsValueType ? null : throw new FormatException($"{name}: null");
        var t = under ?? type;
        var kind = n.GetValueKind();
        string Text() => kind == JsonValueKind.String ? n.GetValue<string>() : throw new FormatException($"{name}: a string");
        double Num() => JsonSchemaLite.Num(n) ?? throw new FormatException($"{name}: a number");
        if (t == typeof(string)) return Text();
        if (t == typeof(bool)) return kind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => throw new FormatException($"{name}: true or false") };
        if (t == typeof(int)) return checked((int)Whole(Num(), name));
        if (t == typeof(long)) return checked((long)Whole(Num(), name));
        if (t == typeof(double)) return Num();
        if (t == typeof(float)) return (float)Num();
        if (t == typeof(decimal)) return decimal.Parse(n.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture);
        if (t.IsEnum) return Enum.TryParse(t, Text(), ignoreCase: true, out var e) && Enum.IsDefined(t, e!) ? e : throw new FormatException($"{name}: one of {string.Join(", ", Enum.GetNames(t))}");
        if (n is not JsonArray arr) throw new FormatException($"{name}: a list");
        var list = arr.Select(x => x?.GetValueKind() == JsonValueKind.String ? x.GetValue<string>() : throw new FormatException($"{name}: a list of strings")).ToList();
        return t == typeof(string[]) ? list.ToArray() : list;
    }

    private static double Whole(double d, string name) => JsonSchemaLite.IsWhole(d) ? d : throw new FormatException($"{name}: a whole number");
}
