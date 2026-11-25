// SPDX-License-Identifier: MIT
using Kilnwarden.Http;

namespace Kilnwarden.Routing;

/// <summary>A request path split into decoded segments, strictly: it starts with '/', has no empty segment (one
/// trailing '/' is allowed and ignored), no "." or ".." segment (also not once decoded), and no segment that decodes to
/// '/', '\' or a control character. Anything else is a 400: the router and the static files never see a path that
/// could climb out of its folder.</summary>
public static class PathSegments
{
    public const int MaxSegments = 32;

    public static bool TryParse(string path, out string[] segments)
    {
        segments = [];
        if (path.Length == 0 || path[0] != '/') return false;
        if (path == "/") return true;
        var body = path.EndsWith('/') ? path[1..^1] : path[1..];
        var parts = body.Split('/');
        if (parts.Length > MaxSegments) return false;
        for (var i = 0; i < parts.Length; i++)
        {
            var raw = parts[i];
            if (raw.Length == 0) return false;
            if (!QueryString.TryDecode(raw, plusIsSpace: false, out var seg)) return false;
            if (seg is "." or ".." || seg.Length == 0) return false;
            foreach (var c in seg)
                if (c is '/' or '\\' || char.IsControl(c)) return false;
            parts[i] = seg;
        }
        segments = parts;
        return true;
    }
}
