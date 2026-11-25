// SPDX-License-Identifier: MIT
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Routing;

namespace Kilnwarden.Static;

/// <summary>The built web app from the mod folder's web/ (DESIGN §4): scanned once at start into a table (request
/// paths never touch the file system, so nothing can climb out of web/). Files under assets/ carry content hashes in
/// their names (Vite) and are cached for a year as immutable; everything else is revalidated (ETag). A sibling
/// "&lt;file&gt;.br" is served when the browser accepts brotli. Paths outside /api/ with no file extension get index.html
/// (the app's own router). Without web/index.html a built-in placeholder page answers.</summary>
public sealed class StaticFiles
{
    public const long MemoryBudget = 24L << 20;
    private readonly Dictionary<string, Entry> _files;

    private StaticFiles(Dictionary<string, Entry> files, bool placeholder)
    {
        _files = files;
        IsPlaceholder = placeholder;
    }

    public bool IsPlaceholder { get; }
    public int Count => _files.Count;

    public static StaticFiles Load(string root, IWebAdminLog log)
    {
        var files = new Dictionary<string, Entry>(StringComparer.Ordinal);
        if (!File.Exists(Path.Combine(root, "index.html")) || !SafeFile(root, Path.Combine(root, "index.html"))) return Placeholder();
        long budget = MemoryBudget;
        var rootFull = Path.GetFullPath(root);
        foreach (var path in Directory.EnumerateFiles(rootFull, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false }))
        {
            var rel = Path.GetRelativePath(rootFull, path).Replace('\\', '/');
            if (rel.EndsWith(".br", StringComparison.Ordinal) || rel.Split('/').Any(s => s.StartsWith('.'))) continue;
            if (!SafeFile(rootFull, path)) continue;
            var br = File.Exists(path + ".br") && SafeFile(rootFull, path + ".br") ? path + ".br" : null;
            files[rel] = Entry.From(rootFull, path, br, rel.StartsWith("assets/", StringComparison.Ordinal), ref budget);
        }
        log.Info($"web app: {files.Count} files from {rootFull}" + (budget < 0 ? " (some streamed from disk: over the memory budget)" : ""));
        return new StaticFiles(files, placeholder: false);
    }

    private static bool SafeFile(string root, string path)
    {
        root = Path.GetFullPath(root);
        path = Path.GetFullPath(path);
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return false;
        for (var at = path; at is not null; at = Path.GetDirectoryName(at))
        {
            if ((File.GetAttributes(at) & FileAttributes.ReparsePoint) != 0) return false;
            if (at == root) return true;
        }
        return false;
    }

    private static StaticFiles Placeholder()
    {
        var asm = typeof(StaticFiles).Assembly;
        string Res(string name)
        {
            using var s = asm.GetManifestResourceStream("placeholder/" + name) ?? throw new InvalidOperationException("missing resource " + name);
            using var r = new StreamReader(s, Encoding.UTF8);
            return r.ReadToEnd();
        }
        var html = Res("index.html").Replace("{{product}}", WebUtility.HtmlEncode(Product.Name)).Replace("{{version}}", WebUtility.HtmlEncode(Product.Version));
        var files = new Dictionary<string, Entry>(StringComparer.Ordinal)
        {
            ["index.html"] = Entry.Memory(Encoding.UTF8.GetBytes(html), null, "text/html; charset=utf-8", immutable: false),
            ["_placeholder/style.css"] = Entry.Memory(Encoding.UTF8.GetBytes(Res("style.css")), null, "text/css; charset=utf-8", immutable: false),
        };
        return new StaticFiles(files, placeholder: true);
    }

    /// <summary>The pipeline's fallback (no route matched).</summary>
    public ValueTask<HttpResponse> HandleAsync(RequestContext ctx)
    {
        var req = ctx.Request;
        if (!req.IsGetOrHead || req.Path.StartsWith(ModulesApiPrefix, StringComparison.Ordinal) || req.Path == "/api")
            return ValueTask.FromResult(HttpResponse.NotFound());
        if (!PathSegments.TryParse(req.Path, out var segs)) return ValueTask.FromResult(HttpResponse.NotFound());
        var key = segs.Length == 0 ? "index.html" : string.Join('/', segs);
        if (!_files.TryGetValue(key, out var e))
        {
            // the app's own routes (/players/7): no extension in the last segment
            if (segs.Length > 0 && segs[^1].Contains('.')) return ValueTask.FromResult(HttpResponse.NotFound());
            e = _files["index.html"];
        }
        return ValueTask.FromResult(e.Respond(req));
    }

    private const string ModulesApiPrefix = "/api/";

    private sealed class Entry
    {
        public required string ContentType { get; init; }
        public required bool Immutable { get; init; }
        public required string ETag { get; init; }
        public byte[]? Body { get; init; }
        public byte[]? Brotli { get; init; }
        public string? Root { get; init; }
        public string? Path { get; init; }
        public string? BrotliPath { get; init; }
        public long Length { get; init; }
        public long BrotliLength { get; init; }
        public bool HasBrotli => Brotli is not null || BrotliPath is not null;

        public static Entry Memory(byte[] body, byte[]? br, string type, bool immutable) => new()
        {
            ContentType = type, Immutable = immutable, Body = body, Brotli = br, Length = body.Length, BrotliLength = br?.Length ?? 0,
            ETag = "\"" + Convert.ToHexString(SHA256.HashData(body))[..16].ToLowerInvariant() + "\"",
        };

        public static Entry From(string root, string path, string? brPath, bool immutable, ref long budget)
        {
            var type = ContentTypes.For(path);
            var len = new FileInfo(path).Length;
            var brLen = brPath is null ? 0 : new FileInfo(brPath).Length;
            if (len + brLen <= budget)
            {
                budget -= len + brLen;
                return Memory(File.ReadAllBytes(path), brPath is null ? null : File.ReadAllBytes(brPath), type, immutable);
            }
            budget = -1;
            using var fs = File.OpenRead(path);
            return new Entry
            {
                ContentType = type, Immutable = immutable, Root = root, Path = path, BrotliPath = brPath, Length = len, BrotliLength = brLen,
                ETag = "\"" + Convert.ToHexString(SHA256.HashData(fs))[..16].ToLowerInvariant() + "\"",
            };
        }

        public HttpResponse Respond(HttpRequest req)
        {
            var r = new HttpResponse { Status = 200 };
            r.Headers.Set("Content-Type", ContentType);
            r.Headers.Set("Cache-Control", Immutable ? "public, max-age=31536000, immutable" : "no-cache");
            r.Headers.Set("ETag", ETag);
            if (HasBrotli) r.Headers.Set("Vary", "Accept-Encoding");
            if (!Immutable && req.Headers.Get("If-None-Match") is { } inm && EtagMatches(inm, ETag))
            {
                r.Status = 304;
                return r;
            }
            var br = HasBrotli && AcceptEncoding.Accepts(req.Headers.Get("Accept-Encoding"), "br");
            if (br) r.Headers.Set("Content-Encoding", "br");
            var mem = br ? Brotli : Body;
            if (mem is not null)
            {
                r.Body = mem;
                return r;
            }
            var file = br ? BrotliPath! : Path!;
            r.BodyLength = br ? BrotliLength : Length;
            r.BodyWriter = async (s, ct) =>
            {
                if (!SafeFile(Root!, file)) throw new IOException("static file is no longer inside its root");
                await using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
                if (fs.Length != r.BodyLength) throw new IOException("static file changed: reload the panel");
                var buffer = new byte[64 * 1024];
                for (var left = r.BodyLength; left > 0;)
                {
                    var n = await fs.ReadAsync(buffer.AsMemory(0, (int)Math.Min(left, buffer.Length)), ct).ConfigureAwait(false);
                    if (n == 0) throw new EndOfStreamException("static file changed");
                    await s.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                    left -= n;
                }
            };
            return r;
        }

        private static bool EtagMatches(string header, string etag)
        {
            foreach (var part in header.Split(','))
            {
                var t = part.Trim();
                if (t.StartsWith("W/", StringComparison.Ordinal)) t = t[2..];
                if (t == etag || t == "*") return true;
            }
            return false;
        }
    }
}

/// <summary>Accept-Encoding (RFC 9110 §12.5.3): codings with optional q-values; "*" covers codings not named; q=0 means
/// "not acceptable". A malformed q makes that element unacceptable (fuzz target).</summary>
public static class AcceptEncoding
{
    public static bool Accepts(string? header, string coding)
    {
        if (string.IsNullOrWhiteSpace(header) || header.Length > 1024) return false;
        double? named = null, star = null;
        foreach (var element in header.Split(','))
        {
            var parts = element.Split(';');
            var name = parts[0].Trim();
            if (name.Length == 0) continue;
            var q = 1.0;
            for (var i = 1; i < parts.Length; i++)
            {
                var p = parts[i].Trim();
                if (!p.StartsWith("q=", StringComparison.OrdinalIgnoreCase)) continue;
                q = ParseQ(p[2..]);
            }
            if (name.Equals(coding, StringComparison.OrdinalIgnoreCase)) named = q;
            else if (name == "*") star = q;
        }
        return (named ?? star ?? 0) > 0;
    }

    /// <summary>qvalue = ( "0" [ "." 0*3DIGIT ] ) / ( "1" [ "." 0*3("0") ] ); anything else reads as 0.</summary>
    private static double ParseQ(string s)
    {
        if (s.Length is 0 or > 5 || s[0] is not ('0' or '1')) return 0;
        if (s.Length > 1 && (s[1] != '.' || s.AsSpan(2).ContainsAnyExceptInRange('0', '9'))) return 0;
        var v = double.Parse(s.Length == 2 ? s[..1] : s, System.Globalization.CultureInfo.InvariantCulture);
        return v > 1 ? 0 : v;
    }
}

/// <summary>Content types by extension (the web app's file kinds).</summary>
public static class ContentTypes
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".mjs"] = "text/javascript; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".map"] = "application/json; charset=utf-8",
        [".webmanifest"] = "application/manifest+json; charset=utf-8",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".avif"] = "image/avif",
        [".gif"] = "image/gif",
        [".ico"] = "image/x-icon",
        [".woff2"] = "font/woff2",
        [".woff"] = "font/woff",
        [".ttf"] = "font/ttf",
        [".txt"] = "text/plain; charset=utf-8",
        [".wasm"] = "application/wasm",
    };

    public static string For(string path) => Map.GetValueOrDefault(Path.GetExtension(path), "application/octet-stream");
}
