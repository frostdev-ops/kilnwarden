// SPDX-License-Identifier: MIT
namespace Kilnwarden.Routing;

public enum LookupResult
{
    Found,
    NotFound,

    /// <summary>The path matches a route, the method does not (405, with <see cref="RouteLookup.Allowed"/>).</summary>
    MethodNotAllowed,
}

public readonly record struct RouteLookup(LookupResult Result, RouteMatch? Match, IReadOnlyList<string> Allowed);

/// <summary>An immutable route table (the registry builds a new one on every change, so lookups on I/O threads take
/// no lock). Routes are kept most specific first: at each segment a literal beats "{param}", which beats "{*rest}".</summary>
public sealed class RouteTable
{
    private readonly Entry[] _entries;

    public static RouteTable Empty { get; } = new([]);

    public RouteTable(IEnumerable<RouteSpec> routes)
    {
        var list = routes.Select(r => new Entry(r, ParsePattern(r.Pattern))).ToList();
        list.Sort(static (a, b) => Compare(a.Segments, b.Segments));
        _entries = [.. list];
    }

    public int Count => _entries.Length;
    public IEnumerable<RouteSpec> Routes => _entries.Select(e => e.Route);

    /// <summary>Finds the route for <paramref name="method"/> and the decoded <paramref name="segments"/>. HEAD matches GET
    /// routes. <paramref name="isEnabled"/> hides the routes of switched-off modules (they answer 404).</summary>
    public RouteLookup Lookup(string method, string[] segments, Func<string, bool>? isEnabled = null)
    {
        List<string>? allowed = null;
        foreach (var e in _entries)
        {
            if (isEnabled is not null && !isEnabled(e.Route.ModuleId)) continue;
            var values = Match(e.Segments, segments);
            if (values is null) continue;
            if (e.Route.Method == method || method == "HEAD" && e.Route.Method == "GET" && e.Route.WebSocket is null)
                return new(LookupResult.Found, new RouteMatch(e.Route, values), []);
            (allowed ??= []).Add(e.Route.Method);
        }
        return allowed is null
            ? new(LookupResult.NotFound, null, [])
            : new(LookupResult.MethodNotAllowed, null, allowed.Distinct().ToList());
    }

    /// <summary>Whether two patterns would match the same paths with the same method (a registration conflict).</summary>
    public static bool SameShape(string a, string b)
    {
        var x = ParsePattern(a);
        var y = ParsePattern(b);
        return x.Length == y.Length && x.Zip(y).All(p => p.First.Kind == p.Second.Kind && (p.First.Kind != SegKind.Literal || p.First.Text == p.Second.Text));
    }

    private static Dictionary<string, string>? Match(Seg[] pattern, string[] path)
    {
        Dictionary<string, string>? values = null;
        for (var i = 0; i < pattern.Length; i++)
        {
            var p = pattern[i];
            if (p.Kind == SegKind.CatchAll)
            {
                (values ??= [])[p.Text] = string.Join('/', path.Skip(i));
                return values;
            }
            if (i >= path.Length) return null;
            if (p.Kind == SegKind.Literal)
            {
                if (!string.Equals(p.Text, path[i], StringComparison.Ordinal)) return null;
            }
            else (values ??= [])[p.Text] = path[i];
        }
        return pattern.Length == path.Length ? values ?? Empty_ : null;
    }

    private static readonly Dictionary<string, string> Empty_ = [];

    private static int Compare(Seg[] a, Seg[] b)
    {
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            var c = a[i].Kind.CompareTo(b[i].Kind);
            if (c != 0) return c;
        }
        return b.Length.CompareTo(a.Length);
    }

    internal enum SegKind
    {
        Literal = 0,
        Param = 1,
        CatchAll = 2,
    }

    internal readonly record struct Seg(SegKind Kind, string Text);

    private sealed record Entry(RouteSpec Route, Seg[] Segments);

    /// <summary>Parses "/api/players/{id}/{*rest}". Throws ArgumentException on a bad pattern.</summary>
    internal static Seg[] ParsePattern(string pattern)
    {
        if (!pattern.StartsWith('/')) throw new ArgumentException($"route pattern '{pattern}' must start with '/'");
        if (pattern == "/") return [];
        var parts = pattern.TrimEnd('/')[1..].Split('/');
        var segs = new Seg[parts.Length];
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            if (p.Length == 0) throw new ArgumentException($"route pattern '{pattern}' has an empty segment");
            if (p[0] == '{')
            {
                if (p[^1] != '}' || p.Length < 3) throw new ArgumentException($"route pattern '{pattern}': bad parameter '{p}'");
                var catchAll = p[1] == '*';
                var name = catchAll ? p[2..^1] : p[1..^1];
                if (name.Length == 0 || !name.All(char.IsAsciiLetterOrDigit)) throw new ArgumentException($"route pattern '{pattern}': bad parameter name '{name}'");
                if (!names.Add(name)) throw new ArgumentException($"route pattern '{pattern}': parameter '{name}' twice");
                if (catchAll && i != parts.Length - 1) throw new ArgumentException($"route pattern '{pattern}': '{{*{name}}}' must be last");
                segs[i] = new(catchAll ? SegKind.CatchAll : SegKind.Param, name);
            }
            else
            {
                if (p.IndexOfAny(['{', '}', '%', '?', '#']) >= 0 || p is "." or "..")
                    throw new ArgumentException($"route pattern '{pattern}': bad literal '{p}'");
                segs[i] = new(SegKind.Literal, p);
            }
        }
        return segs;
    }
}
