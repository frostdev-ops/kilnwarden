// SPDX-License-Identifier: MIT
using Kilnwarden.Http;
using Kilnwarden.Routing;

namespace Kilnwarden.Offline.Routing;

public class RouteTableTests
{
    private static RouteSpec R(string method, string pattern) => new()
    {
        Method = method, Pattern = pattern, Access = RouteAccess.Public, ModuleId = "m",
        Handler = _ => ValueTask.FromResult(HttpResponse.Empty()),
    };

    private static string[] S(string path) => PathSegments.TryParse(path, out var s) ? s : throw new Exception(path);

    [Fact]
    public void Literal_beats_parameter_beats_catch_all()
    {
        var t = new RouteTable([R("GET", "/api/m/{*rest}"), R("GET", "/api/m/{id}"), R("GET", "/api/m/me")]);
        Assert.Equal("/api/m/me", t.Lookup("GET", S("/api/m/me")).Match!.Route.Pattern);
        var byId = t.Lookup("GET", S("/api/m/7")).Match!;
        Assert.Equal("/api/m/{id}", byId.Route.Pattern);
        Assert.Equal("7", byId.Values["id"]);
        var rest = t.Lookup("GET", S("/api/m/a/b/c")).Match!;
        Assert.Equal("a/b/c", rest.Values["rest"]);
    }

    [Fact]
    public void Method_mismatch_is_405_with_allowed_methods()
    {
        var t = new RouteTable([R("GET", "/api/m/x"), R("POST", "/api/m/x")]);
        var l = t.Lookup("DELETE", S("/api/m/x"));
        Assert.Equal(LookupResult.MethodNotAllowed, l.Result);
        Assert.Equal(["GET", "POST"], l.Allowed.Order());
        Assert.Equal(LookupResult.NotFound, t.Lookup("GET", S("/api/m/y")).Result);
    }

    [Fact]
    public void Head_matches_get()
    {
        var t = new RouteTable([R("GET", "/api/m/x")]);
        Assert.Equal(LookupResult.Found, t.Lookup("HEAD", S("/api/m/x")).Result);
    }

    [Fact]
    public void Disabled_modules_do_not_match()
    {
        var t = new RouteTable([R("GET", "/api/m/x")]);
        Assert.Equal(LookupResult.NotFound, t.Lookup("GET", S("/api/m/x"), _ => false).Result);
    }

    [Fact]
    public void Exact_length_only_without_catch_all()
    {
        var t = new RouteTable([R("GET", "/api/m/{id}")]);
        Assert.Equal(LookupResult.NotFound, t.Lookup("GET", S("/api/m")).Result);
        Assert.Equal(LookupResult.NotFound, t.Lookup("GET", S("/api/m/1/2")).Result);
    }

    [Theory]
    [InlineData("api/x")]
    [InlineData("/a//b")]
    [InlineData("/a/{}")]
    [InlineData("/a/{x")]
    [InlineData("/a/{*r}/b")]
    [InlineData("/a/{x}/{x}")]
    [InlineData("/a/../b")]
    [InlineData("/a/%2f")]
    public void Bad_patterns_throw(string pattern) => Assert.Throws<ArgumentException>(() => new RouteTable([R("GET", pattern)]));

    [Fact]
    public void Same_shape_ignores_parameter_names()
    {
        Assert.True(RouteTable.SameShape("/a/{x}", "/a/{y}"));
        Assert.False(RouteTable.SameShape("/a/{x}", "/a/b"));
    }
}
