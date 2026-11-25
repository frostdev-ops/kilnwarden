// SPDX-License-Identifier: MIT
using System.Text;
using System.Text.Json.Nodes;
using Kilnwarden.Http;

namespace Kilnwarden.Offline.Http;

public class JsonBodyTests
{
    private sealed record Login(string User, string Password);

    private static HttpRequest Req(string body, string? type = "application/json; charset=utf-8")
    {
        var r = new HttpRequest { Method = "POST", Target = "/x", Path = "/x", Body = Encoding.UTF8.GetBytes(body) };
        if (type is not null) r.Headers.Add("Content-Type", type);
        return r;
    }

    [Fact]
    public void Reads_camel_case_json() => Assert.Equal(new Login("ada", "pw"), JsonBody.Read<Login>(Req("{\"user\":\"ada\",\"password\":\"pw\"}")));

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/jsonx")]
    [InlineData(null)]
    public void Wrong_media_type_is_415(string? type) => Assert.Equal(415, Assert.Throws<HttpError>(() => JsonBody.Read<Login>(Req("{}", type))).Status);

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("{\"user\":1,}")]
    [InlineData("/*c*/{}")]
    [InlineData("null")]
    [InlineData("[1,2]")]
    [InlineData("{\"user\": \"\\ud800\"}")]
    public void Bad_bodies_are_400(string body) => Assert.Equal(400, Assert.Throws<HttpError>(() => JsonBody.Read<Login>(Req(body))).Status);

    [Fact]
    public void Deep_nesting_is_400()
    {
        var deep = new string('[', 100) + new string(']', 100);
        Assert.Equal(400, Assert.Throws<HttpError>(() => JsonBody.Read<JsonNode>(Req(deep))).Status);
    }

    /// <summary>Found by the player-state fuzz target: System.Text.Json reads a JsonObject lazily, so a duplicate property
    /// or invalid UTF-8 inside a string only throws when a handler touches it (a 500). The body parser touches every node
    /// first, so such a body is a 400 like any other malformed one.</summary>
    [Theory]
    [InlineData("{\"a\":1,\"a\":2}")]
    [InlineData("{\"a\":{\"b\":[1,{\"c\":1,\"c\":2}]}}")]
    public void Lazily_bad_objects_are_refused_up_front(string json)
    {
        var e = Assert.Throws<HttpError>(() => JsonBody.Parse<System.Text.Json.Nodes.JsonObject>(System.Text.Encoding.UTF8.GetBytes(json)));
        Assert.Equal(400, e.Status);
    }

    [Fact]
    public void Invalid_utf8_in_a_string_is_refused_up_front()
    {
        byte[] body = [.. "{\"a\":\""u8, 0xC3, 0x28, .. "\"}"u8];
        Assert.Equal(400, Assert.Throws<HttpError>(() => JsonBody.Parse<System.Text.Json.Nodes.JsonObject>(body)).Status);
    }
}
