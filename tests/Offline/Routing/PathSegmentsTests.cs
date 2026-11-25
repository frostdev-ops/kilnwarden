// SPDX-License-Identifier: MIT
using Kilnwarden.Routing;

namespace Kilnwarden.Offline.Routing;

public class PathSegmentsTests
{
    [Theory]
    [InlineData("/", new string[0])]
    [InlineData("/api/players", new[] { "api", "players" })]
    [InlineData("/api/players/", new[] { "api", "players" })]
    [InlineData("/a%20b/c+d", new[] { "a b", "c+d" })]
    [InlineData("/caf%C3%A9", new[] { "café" })]
    public void Splits_and_decodes(string path, string[] expected)
    {
        Assert.True(PathSegments.TryParse(path, out var segs));
        Assert.Equal(expected, segs);
    }

    [Theory]
    [InlineData("")]
    [InlineData("api")]
    [InlineData("//")]
    [InlineData("/a//b")]
    [InlineData("/a/../b")]
    [InlineData("/a/./b")]
    [InlineData("/%2e%2e/x")]
    [InlineData("/%2E%2E")]
    [InlineData("/a%2Fb")]
    [InlineData("/a%5Cb")]
    [InlineData("/a%00b")]
    [InlineData("/%")]
    [InlineData("/%C3")]
    [InlineData("/a/\u00e9")]
    public void Refuses(string path) => Assert.False(PathSegments.TryParse(path, out _));

    [Fact]
    public void Caps_segments() => Assert.False(PathSegments.TryParse(string.Concat(Enumerable.Repeat("/a", 100)), out _));
}
