// SPDX-License-Identifier: MIT
using System.Text.Json;
using Kilnwarden.Identity;

namespace Kilnwarden.Offline.Identity;

/// <summary>The managed Ed25519 against vectors made outside it (ed25519-vectors.json): RFC 8032 §7.1 tests 1-3 and
/// SHA(abc) plus 48 random keys and messages (0 to 2047 bytes), all signed by OpenSSL; and Wycheproof-style edge cases:
/// S &gt;= L, the eight small-order points as keys (with a forged R = identity, S = 0 that OpenSSL accepts) and as R,
/// non-canonical y &gt;= p for A and R, x = 0 with the sign bit, points off the curve, bit flips, a mixed-order key.</summary>
public sealed class Ed25519VectorTests
{
    private static readonly JsonElement Vectors = Load();

    private static JsonElement Load()
    {
        using var s = typeof(Ed25519VectorTests).Assembly.GetManifestResourceStream("ed25519-vectors.json")!;
        return JsonDocument.Parse(s).RootElement.Clone();
    }

    private static byte[] Hex(JsonElement v, string n) => Convert.FromHexString(v.GetProperty(n).GetString()!);

    [Fact]
    public void Every_openssl_signature_verifies_and_signing_matches_it()
    {
        var n = 0;
        foreach (var v in Vectors.GetProperty("valid").EnumerateArray())
        {
            var name = v.GetProperty("name").GetString();
            Assert.True(Ed25519.Verify(Hex(v, "pub"), Hex(v, "msg"), Hex(v, "sig")), name);
            Assert.Equal(v.GetProperty("pub").GetString(), Convert.ToHexStringLower(Ed25519.PublicKey(Hex(v, "seed"))));
            Assert.Equal(v.GetProperty("sig").GetString(), Convert.ToHexStringLower(Ed25519.Sign(Hex(v, "seed"), Hex(v, "msg"))));
            n++;
        }
        Assert.Equal(52, n);
    }

    [Fact]
    public void Every_edge_case_is_refused()
    {
        var n = 0;
        foreach (var v in Vectors.GetProperty("invalid").EnumerateArray())
        {
            Assert.False(Ed25519.Verify(Hex(v, "pub"), Hex(v, "msg"), Hex(v, "sig")), v.GetProperty("name").GetString() + ": " + v.GetProperty("why").GetString());
            n++;
        }
        Assert.Equal(31, n);
    }

    [Fact]
    public void Small_order_keys_never_verify_anything()
    {
        var small = Vectors.GetProperty("smallOrder").EnumerateArray().Select(e => Convert.FromHexString(e.GetString()!)).ToArray();
        Assert.Equal(8, small.Length);
        foreach (var a in small)
            foreach (var r in small)
                Assert.False(Ed25519.Verify(a, "any message"u8, [.. r, .. new byte[32]]));
    }
}
