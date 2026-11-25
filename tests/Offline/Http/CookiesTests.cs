// SPDX-License-Identifier: MIT
using Kilnwarden.Http;

namespace Kilnwarden.Offline.Http;

public class CookiesTests
{
    [Fact]
    public void Parses_pairs_first_wins_and_unquotes()
    {
        var c = Cookies.Parse("a=1; b=\"two\"; a=3;c=x=y");
        Assert.Equal("1", c["a"]);
        Assert.Equal("two", c["b"]);
        Assert.Equal("x=y", c["c"]);
    }

    [Theory]
    [InlineData("bad name=1")]
    [InlineData("n=has space")]
    [InlineData("n=semi\\colon")]
    [InlineData("=novalue")]
    [InlineData("justtext")]
    [InlineData("n=\u00e9")]
    public void Skips_bad_pairs(string header) => Assert.Empty(Cookies.Parse(header));

    [Fact]
    public void Caps_the_number_of_cookies()
    {
        var header = string.Join("; ", Enumerable.Range(0, 500).Select(i => $"c{i}=v"));
        Assert.Equal(Cookies.MaxCookies, Cookies.Parse(header).Count);
    }

    [Fact]
    public void Null_and_empty_are_empty()
    {
        Assert.Empty(Cookies.Parse(null));
        Assert.Empty(Cookies.Parse(""));
    }

    [Fact]
    public void SetCookie_defaults_are_the_session_cookie()
    {
        var s = new SetCookie("__Host-kwa", "abc").ToString();
        Assert.Equal("__Host-kwa=abc; Path=/; Secure; HttpOnly; SameSite=Strict", s);
        Assert.Contains("Max-Age=0", SetCookie.Delete("x").ToString());
        Assert.Contains("Max-Age=3600", (new SetCookie("x", "1") { MaxAge = TimeSpan.FromHours(1) }).ToString());
    }

    [Fact]
    public void SetCookie_refuses_header_injection()
    {
        Assert.Throws<ArgumentException>(() => new SetCookie("x", "a\r\nSet-Cookie: evil=1").ToString());
        Assert.Throws<ArgumentException>(() => new SetCookie("x;y", "1").ToString());
    }
}
