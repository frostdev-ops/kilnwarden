// SPDX-License-Identifier: MIT
using Kilnwarden.Http;

namespace Kilnwarden.Offline.Http;

public class QueryStringTests
{
    [Fact]
    public void Decodes_plus_percent_and_utf8()
    {
        var q = QueryString.Parse("a=1&b=hello+world&c=%C3%A9t%C3%A9&a=2&flag&&d=");
        Assert.Equal("1", q["a"]);
        Assert.Equal("hello world", q["b"]);
        Assert.Equal("été", q["c"]);
        Assert.Equal("", q["flag"]);
        Assert.Equal("", q["d"]);
    }

    [Theory]
    [InlineData("a=%")]
    [InlineData("a=%4")]
    [InlineData("a=%zz")]
    [InlineData("a=%C3")]          // truncated UTF-8
    [InlineData("a=%FF%FE")]       // not UTF-8
    [InlineData("%=1")]
    public void Skips_unreadable_pairs(string query) => Assert.False(QueryString.Parse(query).ContainsKey("a"));

    [Fact]
    public void Caps_pairs()
    {
        var q = QueryString.Parse(string.Join("&", Enumerable.Range(0, 1000).Select(i => $"k{i}=v")));
        Assert.Equal(QueryString.MaxPairs, q.Count);
    }

    [Fact]
    public void Path_decoding_keeps_plus()
    {
        Assert.True(QueryString.TryDecode("a+b%20c", plusIsSpace: false, out var s));
        Assert.Equal("a+b c", s);
    }
}
