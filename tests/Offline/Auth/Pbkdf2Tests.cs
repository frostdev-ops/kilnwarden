// SPDX-License-Identifier: MIT
using System.Security.Cryptography;
using System.Text;
using Kilnwarden.Auth;

namespace Kilnwarden.Offline.Auth;

/// <summary>The managed PBKDF2-HMAC-SHA256 (used where the platform has none, as under Wine) against RFC 7914 §11 and
/// the platform engine.</summary>
public class Pbkdf2Tests
{
    [Theory]
    [InlineData("passwd", "salt", 1, "55ac046e56e3089fec1691c22544b605f94185216dde0465e68b9d57c20dacbc49ca9cccf179b645991664b39d77ef317c71b845b1e30bd509112041d3a19783")]
    [InlineData("Password", "NaCl", 80000, "4ddcd8f60b98be21830cee5ef22701f9641a4418d04c0414aeff08876b34ab56a1d425a1225833549adb841b51c9b3176a272bdebba1d078478f62b397f33c8d")]
    public void Managed_engine_matches_rfc7914(string password, string salt, int iterations, string hex)
    {
        var dest = new byte[64];
        Pbkdf2.Managed(Encoding.ASCII.GetBytes(password), Encoding.ASCII.GetBytes(salt), iterations, dest);
        Assert.Equal(hex, Convert.ToHexStringLower(dest));
    }

    [Fact]
    public void Managed_engine_matches_the_platform_for_any_shape()
    {
        var rng = new Random(9);
        foreach (var pwLen in new[] { 0, 1, 31, 32, 55, 56, 63, 64, 65, 100, 200 })
        foreach (var saltLen in new[] { 0, 1, 16, 51, 52, 55, 56, 60, 64, 120 })
        foreach (var (iterations, length) in new[] { (1, 32), (2, 16), (3, 33), (7, 64), (1000, 20) })
        {
            var pw = new byte[pwLen];
            var salt = new byte[saltLen];
            rng.NextBytes(pw);
            rng.NextBytes(salt);
            var managed = new byte[length];
            Pbkdf2.Managed(pw, salt, iterations, managed);
            Assert.Equal(Rfc2898DeriveBytes.Pbkdf2(pw, salt, iterations, HashAlgorithmName.SHA256, length), managed);
        }
    }

    [Fact]
    public void Sha256_padding_edges_match_the_platform()
    {
        for (var n = 0; n < 200; n++)
        {
            var data = new byte[n];
            new Random(n).NextBytes(data);
            var digest = new byte[32];
            Sha256.Hash(Sha256.IV, 0, data, digest);
            Assert.Equal(SHA256.HashData(data), digest);
        }
    }

    [Fact]
    public void The_platform_engine_is_used_where_it_works()
    {
        // Linux and Windows have one; under Wine the probe fails and the managed engine answers (the 30.8 lab)
        Assert.Equal("platform", Pbkdf2.Engine);
        Assert.Equal(Rfc2898DeriveBytes.Pbkdf2("a"u8, "b"u8, 5, HashAlgorithmName.SHA256, 32), Pbkdf2.Derive("a"u8, "b"u8, 5, 32));
    }

    [Fact]
    public void The_managed_engine_is_fast_enough()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var dest = new byte[32];
        Pbkdf2.Managed("a password"u8, new byte[16], 100_000, dest);
        // a sanity bound only (Debug build, other tests on the CPU): a Release build does 600k in about 340 ms here
        // (the platform engine: about 100 ms)
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"100k iterations took {sw.Elapsed.TotalMilliseconds:F0} ms");
    }
}
