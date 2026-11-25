// SPDX-License-Identifier: MIT
using System.Text;
using System.Text.Json.Nodes;
using Kilnwarden.Kap;

namespace Kilnwarden.Offline.Kap;

public class KapFramesTests
{
    [Fact]
    public void Encode_prefixes_the_little_endian_length_and_round_trips()
    {
        var buf = KapFrames.Encode(new JsonObject { ["t"] = "ping", ["id"] = 7 });
        Assert.Equal(buf.Length - 4, BitConverter.ToInt32(buf, 0));
        var f = KapFrames.Decode(buf.AsSpan(4));
        Assert.Equal("ping", KapFrames.Str(f, "t"));
        Assert.Equal(7UL, KapFrames.Id(f));
    }

    [Fact]
    public void Lengths_of_zero_or_over_the_cap_are_refused()
    {
        Assert.Equal("bad_frame", Assert.Throws<KapProtocolException>(() => KapFrames.Length(new byte[4], 100)).Code);
        Assert.Equal("too_large", Assert.Throws<KapProtocolException>(() => KapFrames.Length(BitConverter.GetBytes(101), 100)).Code);
        Assert.Equal(100, KapFrames.Length(BitConverter.GetBytes(100), 100));
        Assert.Equal("too_large", Assert.Throws<KapProtocolException>(() => KapFrames.Length(BitConverter.GetBytes(uint.MaxValue), int.MaxValue)).Code);
        Assert.Throws<KapProtocolException>(() => KapFrames.Encode(new JsonObject { ["t"] = new string('x', 200) }, 100));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("\"t\"")]
    [InlineData("{}")]
    [InlineData("{\"t\":1}")]
    [InlineData("{\"t\":\"req\",\"t\":\"res\"}")]
    [InlineData("{\"t\":\"req\",}")]
    [InlineData("{\"t\":\"req\"/*c*/}")]
    [InlineData("{\"t\":\"req\"} x")]
    [InlineData("{\"t\":\"req\",\"op\":\"\\ud800\"}")]       // fuzz finding: a lone surrogate escape threw later, on read
    [InlineData("{\"t\":\"req\",\"\\udc00\":1}")]
    public void Decode_is_strict(string json)
    {
        Assert.Equal("bad_frame", Assert.Throws<KapProtocolException>(() => KapFrames.Decode(Encoding.UTF8.GetBytes(json))).Code);
    }

    [Fact]
    public void Decode_refuses_invalid_utf8_and_deep_nesting()
    {
        Assert.Throws<KapProtocolException>(() => KapFrames.Decode([(byte)'{', (byte)'"', 0xFF, (byte)'"', (byte)':', (byte)'1', (byte)'}']));
        var deep = "{\"t\":\"x\",\"a\":" + new string('[', 100) + new string(']', 100) + "}";
        Assert.Throws<KapProtocolException>(() => KapFrames.Decode(Encoding.UTF8.GetBytes(deep)));
    }

    [Fact]
    public void Ids_are_unsigned_integers_only()
    {
        Assert.Null(KapFrames.Id(new JsonObject { ["id"] = -1 }));
        Assert.Null(KapFrames.Id(new JsonObject { ["id"] = 1.5 }));
        Assert.Null(KapFrames.Id(new JsonObject { ["id"] = "1" }));
        Assert.Equal(ulong.MaxValue, KapFrames.Id(KapFrames.Decode(Encoding.UTF8.GetBytes("{\"t\":\"x\",\"id\":18446744073709551615}"))));
    }

    [Fact]
    public void Limits_are_clamped()
    {
        var l = KapLimits.From(new JsonObject { ["maxFrame"] = int.MaxValue, ["maxClientFrame"] = 1, ["inflight"] = 100000, ["pingS"] = 0, ["idleS"] = 1 });
        Assert.Equal(KapFrames.MaxServerFrame, l.MaxFrame);
        Assert.Equal(4 << 10, l.MaxClientFrame);
        Assert.Equal(256, l.Inflight);
        Assert.Equal(1, l.PingS);
        Assert.Equal(2, l.IdleS);
    }
}

public class KapEndpointTests
{
    [Fact]
    public void Only_loopback_endpoints_are_used()
    {
        Assert.Equal(15637, KapEndpoint.Parse("{\"protocol\":\"kap/1\",\"host\":\"127.0.0.1\",\"port\":15637}"u8).Port);
        Assert.Equal(1, KapEndpoint.Parse("{\"port\":1}"u8).Port);
        Assert.Throws<KapUnavailableException>(() => KapEndpoint.Parse("{\"host\":\"10.0.0.5\",\"port\":15637}"u8));
        Assert.Throws<KapUnavailableException>(() => KapEndpoint.Parse("{\"host\":\"evil.example\",\"port\":15637}"u8));
        Assert.Throws<KapUnavailableException>(() => KapEndpoint.Parse("{\"protocol\":\"kap/2\",\"port\":15637}"u8));
        Assert.Throws<KapUnavailableException>(() => KapEndpoint.Parse("{\"port\":0}"u8));
        Assert.Throws<KapUnavailableException>(() => KapEndpoint.Parse("{\"port\":70000}"u8));
        Assert.Throws<KapUnavailableException>(() => KapEndpoint.Parse("nope"u8));
    }

    [Fact]
    public void Token_file_is_64_hex_with_an_optional_newline()
    {
        var hex = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";
        Assert.Equal(Convert.FromHexString(hex), KapEndpoint.ParseToken(Encoding.ASCII.GetBytes(hex)));
        Assert.Equal(Convert.FromHexString(hex), KapEndpoint.ParseToken(Encoding.ASCII.GetBytes(hex + "\n")));
        Assert.Equal(Convert.FromHexString(hex), KapEndpoint.ParseToken(Encoding.ASCII.GetBytes(hex + "\r\n")));
        Assert.Throws<KapUnavailableException>(() => KapEndpoint.ParseToken(Encoding.ASCII.GetBytes(hex + "\n\n")));
        Assert.Throws<KapUnavailableException>(() => KapEndpoint.ParseToken(Encoding.ASCII.GetBytes(hex[..62])));
        Assert.Throws<KapUnavailableException>(() => KapEndpoint.ParseToken(Encoding.ASCII.GetBytes("zz" + hex[2..])));
    }

    [Fact]
    public void Handshake_macs_match_the_protocol_test_vector()
    {
        // schema/fixtures/kap/handshake.json in the kiln repo
        var token = Convert.FromHexString("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f");
        const string nonce = "202122232425262728292a2b2c2d2e2f303132333435363738393a3b3c3d3e3f";
        const string cnonce = "404142434445464748494a4b4c4d4e4f505152535455565758595a5b5c5d5e5f";
        Assert.Equal("ef7614078851373ad50d58a39bd104753602237a30159343d9423520226e9a1f", Convert.ToHexStringLower(KapClient.Mac(token, "kap1-auth", nonce, cnonce)));
        Assert.Equal("091f206355c6b66354a7fe484eef85df7919a5f105220f4877a9d6d21eeaf4cc", Convert.ToHexStringLower(KapClient.Mac(token, "kap1-ready", nonce, cnonce)));
    }
}
