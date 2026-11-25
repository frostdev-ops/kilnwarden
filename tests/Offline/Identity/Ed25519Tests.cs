// SPDX-License-Identifier: MIT
using System.Numerics;
using Kilnwarden.Identity;

namespace Kilnwarden.Offline.Identity;

public sealed class Ed25519Tests
{
    // RFC 8032 §7.1 test 1, and a vector made with Python cryptography (OpenSSL) for a non-empty message
    private const string Rfc1Seed = "9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60";
    private const string Rfc1Pub = "d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a";
    private const string Rfc1Sig = "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b";
    private const string PyPub = "03a107bff3ce10be1d70dd18e74bc09967e4d6309ba50d5f1ddc8664125531b8";
    private const string PySig = "c9774479a50bd6a2b50c80fb2b71f80652e08720f76316649c3dd894e06d318a11e44f2a37fc6a6cd6d7729d5025a6f25242ed1efe85e1a03d07c7f913dc320f";
    private static readonly byte[] PyMsg = "kiln webadmin broker test"u8.ToArray();

    private static byte[] Hex(string s) => Convert.FromHexString(s);

    [Fact]
    public void Rfc8032_vector_signs_and_verifies()
    {
        Assert.Equal(Rfc1Pub, Convert.ToHexStringLower(Ed25519.PublicKey(Hex(Rfc1Seed))));
        Assert.Equal(Rfc1Sig, Convert.ToHexStringLower(Ed25519.Sign(Hex(Rfc1Seed), [])));
        Assert.True(Ed25519.Verify(Hex(Rfc1Pub), [], Hex(Rfc1Sig)));
    }

    [Fact]
    public void Openssl_made_signature_verifies_and_matches()
    {
        var seed = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        Assert.Equal(PyPub, Convert.ToHexStringLower(Ed25519.PublicKey(seed)));
        Assert.True(Ed25519.Verify(Hex(PyPub), PyMsg, Hex(PySig)));
        Assert.Equal(PySig, Convert.ToHexStringLower(Ed25519.Sign(seed, PyMsg)));
    }

    [Fact]
    public void Tampering_and_malleability_are_refused()
    {
        var sig = Hex(PySig);
        Assert.False(Ed25519.Verify(Hex(PyPub), "kiln webadmin broker tesT"u8, sig));
        for (var i = 0; i < 64; i += 9)
        {
            var bad = (byte[])sig.Clone();
            bad[i] ^= 1;
            Assert.False(Ed25519.Verify(Hex(PyPub), PyMsg, bad));
        }
        Assert.False(Ed25519.Verify(Hex(Rfc1Pub), PyMsg, sig));
        // S + L: the same point equation, refused because S >= L
        var L = BigInteger.Pow(2, 252) + BigInteger.Parse("27742317777372353535851937790883648493");
        var s = new BigInteger(sig.AsSpan(32), isUnsigned: true) + L;
        var mall = (byte[])sig.Clone();
        Array.Clear(mall, 32, 32);
        s.TryWriteBytes(mall.AsSpan(32), out _, isUnsigned: true);
        Assert.False(Ed25519.Verify(Hex(PyPub), PyMsg, mall));
        Assert.False(Ed25519.Verify(Hex(PyPub)[..31], PyMsg, sig));
        Assert.False(Ed25519.Verify(Hex(PyPub), PyMsg, sig[..63]));
        // a public key that is not a curve point
        Assert.False(Ed25519.Verify(Enumerable.Repeat((byte)0xFF, 32).ToArray(), PyMsg, sig));
    }
}
