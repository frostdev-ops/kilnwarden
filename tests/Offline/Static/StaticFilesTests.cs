// SPDX-License-Identifier: MIT
using System.Text;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Offline.Modules;
using Kilnwarden.Offline.Support;
using Kilnwarden.Static;

namespace Kilnwarden.Offline.Static;

public class StaticFilesTests
{
    private static TempDir Web()
    {
        var d = new TempDir();
        Directory.CreateDirectory(Path.Combine(d.Path, "assets"));
        File.WriteAllText(d.File("index.html"), "<!doctype html><title>app</title>");
        File.WriteAllText(d.File("favicon.svg"), "<svg/>");
        File.WriteAllText(Path.Combine(d.Path, "assets", "app-3f9a1c2b.js"), "console.log(1)");
        File.WriteAllBytes(Path.Combine(d.Path, "assets", "app-3f9a1c2b.js.br"), [1, 2, 3]);
        File.WriteAllText(Path.Combine(d.Path, "assets", "style-77aa88bb.css"), "body{}");
        return d;
    }

    private static async Task<HttpResponse> Get(StaticFiles s, string path, string? acceptEncoding = null, string? ifNoneMatch = null, string method = "GET")
    {
        var ctx = ModuleRegistryTests.Ctx(new WebAdminServices());
        var headers = new HttpHeaders();
        if (acceptEncoding is not null) headers.Add("Accept-Encoding", acceptEncoding);
        if (ifNoneMatch is not null) headers.Add("If-None-Match", ifNoneMatch);
        var req = new HttpRequest { Method = method, Target = path, Path = path, Headers = headers };
        ctx = new Kilnwarden.Routing.RequestContext
        {
            Request = req, Connection = ctx.Connection, Services = ctx.Services, ClientAddress = ctx.ClientAddress, Scheme = "https", Host = "localhost",
        };
        return await s.HandleAsync(ctx);
    }

    private static string Text(HttpResponse r) => Encoding.UTF8.GetString(r.Body.Span);

    [Fact]
    public async Task Serves_files_with_types_and_caching()
    {
        using var d = Web();
        var s = StaticFiles.Load(d.Path, new TestLog());
        Assert.False(s.IsPlaceholder);
        var js = await Get(s, "/assets/app-3f9a1c2b.js");
        Assert.Equal(200, js.Status);
        Assert.Equal("text/javascript; charset=utf-8", js.Headers["Content-Type"]);
        Assert.Equal("public, max-age=31536000, immutable", js.Headers["Cache-Control"]);
        Assert.Equal("Accept-Encoding", js.Headers["Vary"]);
        Assert.Equal("console.log(1)", Text(js));
        var index = await Get(s, "/");
        Assert.Equal("no-cache", index.Headers["Cache-Control"]);
        Assert.Equal("text/html; charset=utf-8", index.Headers["Content-Type"]);
        Assert.NotNull(index.Headers["ETag"]);
        Assert.Equal("image/svg+xml", (await Get(s, "/favicon.svg")).Headers["Content-Type"]);
    }

    [Theory]
    [InlineData("gzip, deflate, br, zstd", true)]
    [InlineData("br;q=0.5", true)]
    [InlineData("BR", true)]
    [InlineData("gzip", false)]
    [InlineData("br;q=0", false)]
    [InlineData("br;q=0.000", false)]
    [InlineData("*", true)]
    [InlineData("*, br;q=0", false)]
    [InlineData("br;q=bogus", false)]
    [InlineData("", false)]
    public async Task Brotli_when_accepted(string accept, bool br)
    {
        using var d = Web();
        var s = StaticFiles.Load(d.Path, new TestLog());
        var r = await Get(s, "/assets/app-3f9a1c2b.js", accept);
        Assert.Equal(br ? "br" : null, r.Headers["Content-Encoding"]);
        Assert.Equal(br ? 3 : 14, r.Body.Length);
    }

    [Fact]
    public async Task Etag_gives_304()
    {
        using var d = Web();
        var s = StaticFiles.Load(d.Path, new TestLog());
        var etag = (await Get(s, "/favicon.svg")).Headers["ETag"]!;
        Assert.Equal(304, (await Get(s, "/favicon.svg", ifNoneMatch: etag)).Status);
        Assert.Equal(304, (await Get(s, "/favicon.svg", ifNoneMatch: "\"x\", " + etag)).Status);
        Assert.Equal(200, (await Get(s, "/favicon.svg", ifNoneMatch: "\"other\"")).Status);
    }

    [Fact]
    public async Task Spa_fallback_but_never_for_api_or_files()
    {
        using var d = Web();
        var s = StaticFiles.Load(d.Path, new TestLog());
        Assert.Contains("<title>app</title>", Text(await Get(s, "/players/7")));
        Assert.Equal(404, (await Get(s, "/api/nope")).Status);
        Assert.Equal(404, (await Get(s, "/assets/missing-11111111.js")).Status);
        Assert.Equal(404, (await Get(s, "/assets/app-3f9a1c2b.js.br")).Status);
        Assert.Equal(404, (await Get(s, "/index.html", method: "POST")).Status);
        Assert.Equal(404, (await Get(s, "/x/../index.html")).Status);
    }

    [Fact]
    public async Task Head_has_no_body_but_the_length()
    {
        using var d = Web();
        var s = StaticFiles.Load(d.Path, new TestLog());
        var r = await Get(s, "/favicon.svg", method: "HEAD");
        Assert.Equal(200, r.Status);
        Assert.Equal(6, r.Body.Length); // the writer leaves the body out for HEAD and keeps the length
    }

    [Fact]
    public async Task Placeholder_without_a_built_app()
    {
        using var d = new TempDir();
        var s = StaticFiles.Load(d.Path, new TestLog());
        Assert.True(s.IsPlaceholder);
        var page = await Get(s, "/");
        Assert.Contains(Product.Name, Text(page));
        Assert.DoesNotContain("{{", Text(page));
        Assert.DoesNotContain("<script", Text(page));
        Assert.DoesNotContain("style=", Text(page));
        Assert.Equal("text/css; charset=utf-8", (await Get(s, "/_placeholder/style.css")).Headers["Content-Type"]);
        Assert.Contains(Product.Name, Text(await Get(s, "/some/page")));
    }

    [Fact]
    public void Missing_root_is_the_placeholder() => Assert.True(StaticFiles.Load("/nonexistent/web", new TestLog()).IsPlaceholder);
}
