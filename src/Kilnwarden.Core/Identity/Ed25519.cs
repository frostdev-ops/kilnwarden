// SPDX-License-Identifier: MIT
using System.Numerics;
using System.Security.Cryptography;

namespace Kilnwarden.Identity;

/// <summary>Ed25519 (RFC 8032) in managed code: .NET has no Ed25519, and the frostdev.io broker's assertions are signed
/// with it (DESIGN §3). Verification is what the panel needs; signing exists for the mock broker in the tests. Not
/// constant-time: only public keys and public signatures pass through <see cref="Verify"/>, and <see cref="Sign"/>
/// never sees a production key (the panel holds none).
/// <para>Verification rules (RFC 8032 §5.1.7, plus the libsodium-style strict checks; reviewed in the step 2 integration):
/// A and R must be canonical encodings (y &lt; p, no x = 0 with the sign bit) of curve points that are not of small order
/// (the eight torsion points), S must be below L, and the cofactorless equation [S]B = R + [k]A must hold, compared as
/// canonical encodings. The small-order rule refuses keys and signatures that verify without any secret (for example the
/// identity key with R = identity, S = 0, which RFC 8032 and OpenSSL accept). Known answers and edge cases:
/// tests/Offline/Identity/ed25519-vectors.json.</para></summary>
public static class Ed25519
{
    public const int PublicKeySize = 32, SignatureSize = 64, SeedSize = 32;

    private static readonly BigInteger P = BigInteger.Pow(2, 255) - 19;
    private static readonly BigInteger L = BigInteger.Pow(2, 252) + BigInteger.Parse("27742317777372353535851937790883648493");
    private static readonly BigInteger D = Mod(-121665 * Inv(121666));
    private static readonly BigInteger SqrtM1 = BigInteger.ModPow(2, (P - 1) / 4, P);
    private static readonly Point B = MakeBase();
    private static readonly byte[] IdentityEncoding = Encode(new Point(0, 1, 1, 0));

    private readonly record struct Point(BigInteger X, BigInteger Y, BigInteger Z, BigInteger T);

    private static Point MakeBase()
    {
        var y = Mod(4 * Inv(5));
        var x = RecoverX(y, 0) ?? throw new InvalidOperationException("ed25519 base point");
        return new Point(x, y, 1, Mod(x * y));
    }

    private static BigInteger Mod(BigInteger a)
    {
        var r = a % P;
        return r.Sign < 0 ? r + P : r;
    }

    private static BigInteger Inv(BigInteger a) => BigInteger.ModPow(Mod(a), P - 2, P);

    private static Point Add(Point a, Point b)
    {
        var A = Mod((a.Y - a.X) * (b.Y - b.X));
        var Bv = Mod((a.Y + a.X) * (b.Y + b.X));
        var C = Mod(a.T * 2 * D * b.T);
        var Dv = Mod(a.Z * 2 * b.Z);
        var E = Bv - A;
        var F = Dv - C;
        var G = Dv + C;
        var H = Bv + A;
        return new Point(Mod(E * F), Mod(G * H), Mod(F * G), Mod(E * H));
    }

    private static Point Mul(BigInteger s, Point p)
    {
        var q = new Point(0, 1, 1, 0);
        while (s.Sign > 0)
        {
            if (!s.IsEven) q = Add(q, p);
            p = Add(p, p);
            s >>= 1;
        }
        return q;
    }

    private static byte[] Encode(Point p)
    {
        var zi = Inv(p.Z);
        var x = Mod(p.X * zi);
        var y = Mod(p.Y * zi);
        var bytes = new byte[32];
        y.TryWriteBytes(bytes, out _, isUnsigned: true, isBigEndian: false);
        if (!x.IsEven) bytes[31] |= 0x80;
        return bytes;
    }

    private static BigInteger? RecoverX(BigInteger y, int sign)
    {
        if (y >= P) return null;
        var x2 = Mod((y * y - 1) * Inv(D * y * y + 1));
        if (x2.IsZero) return sign == 0 ? BigInteger.Zero : null;
        var x = BigInteger.ModPow(x2, (P + 3) / 8, P);
        if (Mod(x * x - x2) != 0) x = Mod(x * SqrtM1);
        if (Mod(x * x - x2) != 0) return null;
        if ((int)(x & 1) != sign) x = P - x;
        return x;
    }

    private static Point? Decode(ReadOnlySpan<byte> s)
    {
        if (s.Length != 32) return null;
        Span<byte> b = stackalloc byte[32];
        s.CopyTo(b);
        var sign = b[31] >> 7;
        b[31] &= 0x7F;
        var y = new BigInteger(b, isUnsigned: true, isBigEndian: false);
        if (RecoverX(y, sign) is not { } x) return null;
        return new Point(x, y, 1, Mod(x * y));
    }

    /// <summary>Whether [8]P is the identity: P is one of the eight small-order (torsion) points.</summary>
    private static bool IsSmallOrder(Point p)
    {
        var p8 = p;
        for (var i = 0; i < 3; i++) p8 = Add(p8, p8);
        return Encode(p8).AsSpan().SequenceEqual(IdentityEncoding);
    }

    private static BigInteger HashModL(params byte[][] parts)
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
        foreach (var p in parts) h.AppendData(p);
        return new BigInteger(h.GetHashAndReset(), isUnsigned: true, isBigEndian: false) % L;
    }

    /// <summary>True when <paramref name="signature"/> is a valid Ed25519 signature of <paramref name="message"/> by
    /// <paramref name="publicKey"/> (cofactorless check, S &lt; L enforced: no malleable signatures; small-order A or R refused).</summary>
    public static bool Verify(ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature)
    {
        if (publicKey.Length != PublicKeySize || signature.Length != SignatureSize) return false;
        if (Decode(publicKey) is not { } a || Decode(signature[..32]) is not { } r) return false;
        if (IsSmallOrder(a) || IsSmallOrder(r)) return false;
        var s = new BigInteger(signature[32..], isUnsigned: true, isBigEndian: false);
        if (s >= L) return false;
        var k = HashModL(signature[..32].ToArray(), publicKey.ToArray(), message.ToArray());
        var left = Encode(Mul(s, B));
        var right = Encode(Add(r, Mul(k, a)));
        return CryptographicOperations.FixedTimeEquals(left, right);
    }

    /// <summary>The public key of a 32-byte seed.</summary>
    public static byte[] PublicKey(ReadOnlySpan<byte> seed) => Encode(Mul(Scalar(seed, out _), B));

    /// <summary>Signs (the mock broker in tests; the panel never signs).</summary>
    public static byte[] Sign(ReadOnlySpan<byte> seed, ReadOnlySpan<byte> message)
    {
        var a = Scalar(seed, out var prefix);
        var pub = Encode(Mul(a, B));
        var r = HashModL(prefix, message.ToArray());
        var R = Encode(Mul(r, B));
        var k = HashModL(R, pub, message.ToArray());
        var s = (r + k * a) % L;
        var sig = new byte[64];
        R.CopyTo(sig, 0);
        s.TryWriteBytes(sig.AsSpan(32), out _, isUnsigned: true, isBigEndian: false);
        return sig;
    }

    private static BigInteger Scalar(ReadOnlySpan<byte> seed, out byte[] prefix)
    {
        if (seed.Length != SeedSize) throw new ArgumentException("an Ed25519 seed is 32 bytes");
        var h = SHA512.HashData(seed);
        h[0] &= 248;
        h[31] &= 127;
        h[31] |= 64;
        prefix = h[32..];
        return new BigInteger(h.AsSpan(0, 32), isUnsigned: true, isBigEndian: false);
    }
}
