// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kilnwarden.Activity;
using Kilnwarden.Ops;
using Kilnwarden.PublicPage;
using Kilnwarden.Tls;

namespace Kilnwarden.Fuzz;

/// <summary>Parsers added for the release: the TLS ClientHello reader (TLS-ALPN-01), minisign signatures and keys, the
/// public page's markdown, Kiln.Admin's state as Player activity reads it, and relay peer announcements.</summary>
public static class CompleteTargets
{
    private static string Utf8(byte[] b) => Encoding.UTF8.GetString(b);

    private static void Check(bool ok, string what)
    {
        if (!ok) throw new FuzzFinding(what);
    }

    // a ClientHello for acme-tls/1 to panel.example.test (what a TLS-ALPN-01 validation sends), as Latin-1 text
    private static readonly string Hello = Encoding.Latin1.GetString(Convert.FromHexString(
        "16030100a5010000a103031111111111111111111111111111111111111111111111111111111111111111110000041301130201000074" +
        "0000001700150000127061" + "6e656c2e6578616d706c652e74657374" + "0010000d000b0a61636d652d746c732f31" +
        "000a00040002001d000d000400020403002b0003020304003300260024001d0020" + new string('2', 64) + "0016000000170000ff01000100"));

    public static readonly Target[] All =
    [
        new("client-hello", ClientHelloTarget, [], [Hello, "\x16\x03\x01\x00\x05\x01\x00\x00\x01\x03"],
            ["\x16\x03\x01", "\x16\x03\x03", "\x01\x00\x00", "\x00\x10", "\x00\x00", "acme-tls/1", "h2", "http/1.1", "\x00\xff", "\xff\xff", "\x00"]),
        new("minisign", MinisignTarget, [],
            ["untrusted comment: x\nRUQRIjNEVWZ3iAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\ntrusted comment: timestamp:1\nAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==\n",
             "RWQRIjNEVWZ3iDuDHkJQDSbPzacTPz7RnPy7iEwGx6ZK/5Omnk7b4tRb"],
            ["untrusted comment: ", "trusted comment: ", "\n", "\r\n", "RW", "RU", "ED", "Ed", "=", "/", "+", "A"]),
        new("markdown", MarkdownTarget, [], ["# Rules\n\nBe **kind**.\n- a\n- b\n1. x\n[l](https://e.x) `c` _i_", "<script>x</script>"],
            ["#", "##", "- ", "* ", "1. ", "**", "*", "_", "`", "[", "](", "https://", "javascript:", ")", "<", ">", "\"", "&", "\n", "\n\n"]),
        new("player-state", PlayerStateTarget, [typeof(JsonException)],
            ["{\"players\":[{\"name\":\"Ann\",\"steamId\":\"76561198000000001\",\"entity\":7,\"pos\":{\"x\":1,\"y\":2,\"z\":3}}],\"combatDeaths\":[{\"entity\":7,\"time\":100,\"victimName\":\"Ann\",\"killerName\":\"Wolf\"}],\"deaths\":[{\"entity\":7,\"clock\":1.5}]}"],
            ["{", "}", "[", "]", "\"name\"", "\"steamId\"", "\"entity\"", "\"time\"", "\"clock\"", "null", "-1", "1e308", "\"\\u0000\"", "4294967296", ","]),
        new("relay-peer", b => _ = IPAddress.TryParse(Utf8(b), out var ip) && Relay.RelayComponent.AcceptableAnnouncement(ip), [],
            ["8.8.8.8", "::ffff:127.0.0.1", "2001:4860:4860::8888", "fe80::1%eth0"], [":", ".", "::", "ffff", "%", "0", "255", "/"]),
    ];

    private static void ClientHelloTarget(byte[] b)
    {
        var h = ClientHello.Parse(b);
        if (h is null) return;
        Check(h.ServerName is null || (h.ServerName.Length <= 253 && h.ServerName == h.ServerName.ToLowerInvariant()), "bad server name");
        Check(h.Alpn.Count <= 32 && h.Alpn.All(a => a.Length is > 0 and <= 255 && a.All(c => c is > ' ' and < '\x7f')), "bad ALPN entry");
    }

    private static readonly IReadOnlyList<SigningKey> Keys =
        [new("k", [0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88], new byte[32])];

    private static void MinisignTarget(byte[] b)
    {
        var text = Utf8(b);
        var r = Minisign.Verify(text, "payload"u8, Keys);
        Check(r.State is "verified" or "unsigned" or "unknown_key" or "invalid", "unknown state");
        Check(r.State != "verified", "a junk signature verified");
        if (Minisign.ParsePublicKey(text) is { } k) Check(k.KeyId.Length == 8 && k.PublicKey.Length == 32, "bad key sizes");
    }

    private static void MarkdownTarget(byte[] b)
    {
        var html = PublicMarkdown.Render(Utf8(b));
        // (a "javascript:" inside an https URL, like https://e.xjavascript:, is only text in a host name: the href check below
        // is what matters)
        Check(!html.Contains("<script", StringComparison.OrdinalIgnoreCase), "script in output");
        // every tag the renderer writes is one of its own
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(html, "<(/?)([a-z0-9]+)"))
            Check(m.Groups[2].Value is "p" or "br" or "ul" or "ol" or "li" or "h3" or "h4" or "strong" or "em" or "code" or "a", "unexpected tag " + m.Groups[2].Value);
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(html, "href=\"([^\"]*)\""))
            Check(m.Groups[1].Value.StartsWith("https://", StringComparison.Ordinal) || m.Groups[1].Value.StartsWith("http://", StringComparison.Ordinal), "a link that is not http(s)");
    }

    private static void PlayerStateTarget(byte[] b)
    {
        var node = JsonNode.Parse(b, documentOptions: new JsonDocumentOptions { MaxDepth = 32 });
        var t = new PlayerTracker();
        var now = DateTimeOffset.UnixEpoch.AddYears(56);
        t.Observe(node, now);
        foreach (var c in t.Observe(node, now.AddMinutes(1)))
            Check(c.Entry.Name.Length is > 0 and <= 64 && !c.Entry.Name.Any(char.IsControl), "a bad name in an entry");
        Check(t.Online.Count <= PlayerTracker.MaxPlayers, "unbounded");
    }
}
