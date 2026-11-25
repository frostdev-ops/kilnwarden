// SPDX-License-Identifier: MIT
using System.Text;
using Kilnwarden.Http;

namespace Kilnwarden.Offline.Http;

public class HttpParserTests
{
    private static readonly HttpLimits Limits = new(MaxHeaderBytes: 2048, MaxHeaderCount: 20, MaxTargetBytes: 256);

    private static (ParseStatus Status, HttpRequest? Req, int Consumed) Parse(string raw, HttpLimits? limits = null)
    {
        var bytes = Encoding.Latin1.GetBytes(raw);
        var st = HttpParser.TryParseHead(bytes, limits ?? Limits, out var req, out var consumed);
        return (st, req, consumed);
    }

    private static int Status(string raw, HttpLimits? limits = null) =>
        Assert.Throws<HttpError>(() => Parse(raw, limits)).Status;

    [Fact]
    public void Parses_a_get()
    {
        var raw = "GET /api/x?a=1&b=2 HTTP/1.1\r\nHost: localhost:15737\r\nUser-Agent: t\r\nCookie: a=1\r\n\r\nNEXT";
        var (st, req, consumed) = Parse(raw);
        Assert.Equal(ParseStatus.Complete, st);
        Assert.Equal(raw.Length - 4, consumed);
        Assert.Equal("GET", req!.Method);
        Assert.Equal("/api/x", req.Path);
        Assert.Equal("a=1&b=2", req.Query);
        Assert.Equal("localhost:15737", req.Host);
        Assert.Equal(1, req.MinorVersion);
        Assert.Equal("t", req.Headers["user-agent"]);
        Assert.True(req.KeepAlive);
        Assert.Equal(0, req.ContentLength);
    }

    [Fact]
    public void Trims_optional_whitespace_and_keeps_inner_spaces()
    {
        var (_, req, _) = Parse("GET / HTTP/1.1\r\nHost: h\r\nX-A: \t a b \t\r\nX-Empty:\r\n\r\n");
        Assert.Equal("a b", req!.Headers["X-A"]);
        Assert.Equal("", req.Headers["X-Empty"]);
    }

    [Fact]
    public void Incomplete_until_the_blank_line()
    {
        Assert.Equal(ParseStatus.Incomplete, Parse("GET / HTTP/1.1\r\nHost: h\r\n").Status);
        Assert.Equal(ParseStatus.Incomplete, Parse("GET / HT").Status);
        Assert.Equal(ParseStatus.Incomplete, Parse("").Status);
        Assert.Equal(ParseStatus.Incomplete, Parse("\r\n").Status);
    }

    [Fact]
    public void Skips_leading_blank_lines()
    {
        var (st, req, consumed) = Parse("\r\n\r\nGET / HTTP/1.1\r\nHost: h\r\n\r\n");
        Assert.Equal(ParseStatus.Complete, st);
        Assert.Equal("/", req!.Path);
        Assert.Equal(31, consumed);
    }

    [Fact]
    public void Content_length_and_http10()
    {
        var (_, req, _) = Parse("POST /a HTTP/1.0\r\nContent-Length: 12\r\n\r\n");
        Assert.Equal(12, req!.ContentLength);
        Assert.Equal(0, req.MinorVersion);
        Assert.False(req.KeepAlive);
        Assert.Equal("", req.Host);
        Assert.True(Parse("GET / HTTP/1.0\r\nConnection: keep-alive\r\n\r\n").Req!.KeepAlive);
        Assert.False(Parse("GET / HTTP/1.1\r\nHost: h\r\nConnection: close\r\n\r\n").Req!.KeepAlive);
    }

    [Fact]
    public void WebSocket_upgrade_is_recognised()
    {
        var req = Parse("GET /ws HTTP/1.1\r\nHost: h\r\nUpgrade: websocket\r\nConnection: keep-alive, Upgrade\r\n\r\n").Req!;
        Assert.True(req.IsWebSocketUpgrade);
    }

    [Theory]
    // request line
    [InlineData("GET  / HTTP/1.1\r\nHost: h\r\n\r\n", 400)]
    [InlineData("GET / HTTP/1.1 \r\nHost: h\r\n\r\n", 400)]
    [InlineData(" GET / HTTP/1.1\r\nHost: h\r\n\r\n", 400)]
    [InlineData("GET /\tx HTTP/1.1\r\nHost: h\r\n\r\n", 400)]
    [InlineData("G(T / HTTP/1.1\r\nHost: h\r\n\r\n", 400)]
    [InlineData("GET http://evil/ HTTP/1.1\r\nHost: h\r\n\r\n", 400)]
    [InlineData("OPTIONS * HTTP/1.1\r\nHost: h\r\n\r\n", 400)]
    [InlineData("GET /#frag HTTP/1.1\r\nHost: h\r\n\r\n", 400)]
    [InlineData("GET / HTTP/2.0\r\nHost: h\r\n\r\n", 505)]
    [InlineData("GET / HTTP/1.2\r\nHost: h\r\n\r\n", 505)]
    [InlineData("GET / HTTPS/1.1\r\nHost: h\r\n\r\n", 400)]
    [InlineData("GET / http/1.1\r\nHost: h\r\n\r\n", 400)]
    [InlineData("BREW / HTTP/1.1\r\nHost: h\r\n\r\n", 501)]
    [InlineData("CONNECT / HTTP/1.1\r\nHost: h\r\n\r\n", 501)]
    [InlineData("get / HTTP/1.1\r\nHost: h\r\n\r\n", 501)]
    // line endings
    [InlineData("GET / HTTP/1.1\nHost: h\r\n\r\n", 400)]
    [InlineData("GET / HTTP/1.1\r\nHost: h\n\r\n", 400)]
    [InlineData("GET / HTTP/1.1\r\nHost: h\rX: y\r\n\r\n", 400)]
    // header fields
    [InlineData("GET / HTTP/1.1\r\nHost : h\r\n\r\n", 400)]
    [InlineData("GET / HTTP/1.1\r\nHost: h\r\n folded\r\n\r\n", 400)]
    [InlineData("GET / HTTP/1.1\r\nHost: h\r\nNo colon\r\n\r\n", 400)]
    [InlineData("GET / HTTP/1.1\r\nHost: h\r\n: empty\r\n\r\n", 400)]
    [InlineData("GET / HTTP/1.1\r\nHost: h\r\nX: a\u0000b\r\n\r\n", 400)]
    [InlineData("GET / HTTP/1.1\r\nHost: h\r\nX: a\u007fb\r\n\r\n", 400)]
    [InlineData("GET / HTTP/1.1\r\nHost: h\r\nXé: v\r\n\r\n", 400)]
    // Host
    [InlineData("GET / HTTP/1.1\r\n\r\n", 400)]
    [InlineData("GET / HTTP/1.1\r\nHost: a\r\nHost: b\r\n\r\n", 400)]
    [InlineData("GET / HTTP/1.1\r\nHost: \r\n\r\n", 400)]
    [InlineData("GET / HTTP/1.1\r\nHost: a/b\r\n\r\n", 400)]
    [InlineData("GET / HTTP/1.1\r\nHost: user@h\r\n\r\n", 400)]
    [InlineData("GET / HTTP/1.1\r\nHost: a b\r\n\r\n", 400)]
    // framing (request smuggling)
    [InlineData("POST / HTTP/1.1\r\nHost: h\r\nTransfer-Encoding: chunked\r\n\r\n", 501)]
    [InlineData("POST / HTTP/1.1\r\nHost: h\r\nTransfer-Encoding: chunked\r\nContent-Length: 3\r\n\r\n", 400)]
    [InlineData("POST / HTTP/1.1\r\nHost: h\r\nContent-Length: 3\r\nContent-Length: 3\r\n\r\n", 400)]
    [InlineData("POST / HTTP/1.1\r\nHost: h\r\nContent-Length: 3, 3\r\n\r\n", 400)]
    [InlineData("POST / HTTP/1.1\r\nHost: h\r\nContent-Length: +3\r\n\r\n", 400)]
    [InlineData("POST / HTTP/1.1\r\nHost: h\r\nContent-Length: -1\r\n\r\n", 400)]
    [InlineData("POST / HTTP/1.1\r\nHost: h\r\nContent-Length: 0x10\r\n\r\n", 400)]
    [InlineData("POST / HTTP/1.1\r\nHost: h\r\nContent-Length: 99999999999999999999\r\n\r\n", 400)]
    [InlineData("POST / HTTP/1.1\r\nHost: h\r\nContent-Length: \r\n\r\n", 400)]
    [InlineData("POST / HTTP/1.1\r\nHost: h\r\nExpect: something\r\n\r\n", 417)]
    public void Refuses(string raw, int status) => Assert.Equal(status, Status(raw));

    [Fact]
    public void Caps()
    {
        var longTarget = "GET /" + new string('a', 300) + " HTTP/1.1\r\nHost: h\r\n\r\n";
        Assert.Equal(414, Status(longTarget));
        Assert.Equal(414, Status("GET /" + new string('a', 300)));               // still arriving, already too long
        var many = "GET / HTTP/1.1\r\nHost: h\r\n" + string.Concat(Enumerable.Range(0, 30).Select(i => $"X-{i}: v\r\n")) + "\r\n";
        Assert.Equal(431, Status(many));
        var big = "GET / HTTP/1.1\r\nHost: h\r\nX: " + new string('v', 3000) + "\r\n\r\n";
        Assert.Equal(431, Status(big));
        Assert.Equal(431, Status("GET / HTTP/1.1\r\nHost: h\r\nX: " + new string('v', 3000)));   // incomplete but over the cap
        Assert.Equal(431, Status(new string('\r', 1) + new string('\n', 1) + "GET / HTTP/1.1\r\n" + new string('x', 3000)));
    }

    [Fact]
    public void Expect_100_continue_is_flagged()
    {
        var req = Parse("PUT /a HTTP/1.1\r\nHost: h\r\nContent-Length: 5\r\nExpect: 100-Continue\r\n\r\n").Req!;
        Assert.True(HttpParser.ExpectsContinue(req));
    }

    [Fact]
    public void Obs_text_in_values_is_latin1()
    {
        var req = Parse("GET / HTTP/1.1\r\nHost: h\r\nX: café\r\n\r\n").Req!;
        Assert.Equal("café", req.Headers["X"]);
    }

    [Fact]
    public void Ipv6_and_port_hosts()
    {
        Assert.Equal("[::1]:15737", Parse("GET / HTTP/1.1\r\nHost: [::1]:15737\r\n\r\n").Req!.Host);
        Assert.Equal("panel.example.com", Parse("GET / HTTP/1.1\r\nHost: panel.example.com\r\n\r\n").Req!.Host);
    }
}
