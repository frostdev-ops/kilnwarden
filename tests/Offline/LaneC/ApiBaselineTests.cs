// SPDX-License-Identifier: MIT
using System.Reflection;
using System.Text;
using Kilnwarden.Api;
using Kilnwarden.Offline.Support;

namespace Kilnwarden.Offline.LaneC;

/// <summary>The API gate (DESIGN §6: "a shipped API baseline file and an analyzer gate like Kiln's"): the public surface
/// of Kilnwarden.Api must equal src/Kilnwarden.Api/PublicAPI.txt. A change is a decision: update the file
/// (KWA_UPDATE_API=1 writes it) and bump WebPanel.ApiVersion (minor for additions, major for removals).</summary>
public sealed class ApiBaselineTests
{
    public static string Surface()
    {
        var asm = typeof(IWebAdmin).Assembly;
        var sb = new StringBuilder();
        sb.Append("// Kilnwarden.Api ").Append(WebPanel.ApiVersion).Append('\n');
        foreach (var t in asm.GetExportedTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            var kind = t.IsEnum ? "enum" : t.IsInterface ? "interface" : t.IsValueType ? "struct" : typeof(Delegate).IsAssignableFrom(t) ? "delegate"
                : t.IsAbstract && t.IsSealed ? "static class" : t.IsAbstract ? "abstract class" : t.IsSealed ? "sealed class" : "class";
            sb.Append(kind).Append(' ').Append(t.FullName);
            if (t.BaseType is { } b && b != typeof(object) && b != typeof(Enum) && b != typeof(ValueType) && b != typeof(MulticastDelegate))
                sb.Append(" : ").Append(b.FullName);
            sb.Append('\n');
            const BindingFlags all = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var members = t.GetMembers(all)
                .Where(m => m is not MethodInfo { IsSpecialName: true } && !(t.IsEnum && m.Name == "value__"))
                .Where(m => m is not MethodBase mb || mb.IsPublic || mb.IsFamily)
                .Select(Describe)
                .Order(StringComparer.Ordinal);
            foreach (var m in members) sb.Append("  ").Append(m).Append('\n');
        }
        return sb.ToString();
    }

    private static string Describe(MemberInfo m) => m switch
    {
        PropertyInfo p => $"prop {p.PropertyType} {p.Name} {{{(p.GetMethod?.IsPublic == true ? " get;" : "")}{(p.SetMethod?.IsPublic == true ? (p.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Any(x => x.Name == "IsExternalInit") ? " init;" : " set;") : "")} }}",
        FieldInfo f => $"field {(f.IsLiteral ? "const " : f.IsStatic ? "static " : "")}{f.FieldType} {f.Name}{(f.IsLiteral ? " = " + f.GetRawConstantValue() : "")}",
        EventInfo e => $"event {e.EventHandlerType} {e.Name}",
        ConstructorInfo c => $"ctor({string.Join(", ", c.GetParameters().Select(p => $"{p.ParameterType} {p.Name}"))})",
        MethodInfo mi => $"method {(mi.IsStatic ? "static " : "")}{mi.ReturnType} {mi.Name}{(mi.IsGenericMethod ? "<" + string.Join(",", mi.GetGenericArguments().Select(a => a.Name)) + ">" : "")}({string.Join(", ", mi.GetParameters().Select(p => $"{p.ParameterType} {p.Name}"))})",
        Type nt => $"nested {nt.Name}",
        _ => m.ToString() ?? m.Name,
    };

    [Fact]
    public void Public_surface_matches_the_baseline()
    {
        var path = Path.Combine(Native.RepoRoot, "src", "Kilnwarden.Api", "PublicAPI.txt");
        var now = Surface();
        if (Environment.GetEnvironmentVariable("KWA_UPDATE_API") == "1") File.WriteAllText(path, now);
        Assert.True(File.Exists(path), "src/Kilnwarden.Api/PublicAPI.txt is missing (KWA_UPDATE_API=1 writes it)");
        Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n"), now);
    }

    [Fact]
    public void Api_assembly_references_nothing_but_the_runtime()
    {
        var refs = typeof(IWebAdmin).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        Assert.All(refs, r => Assert.True(r.StartsWith("System", StringComparison.Ordinal) || r is "netstandard" or "mscorlib", r));
        Assert.Equal(new Version(1, 0, 0, 0), typeof(IWebAdmin).Assembly.GetName().Version);
    }
}
