// SPDX-License-Identifier: MIT
using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;

namespace Kilnwarden.Auth;

/// <summary>PBKDF2-HMAC-SHA256 (RFC 8018), two engines with the same output: the platform's
/// (Rfc2898DeriveBytes.Pbkdf2: BCrypt on Windows, OpenSSL on Linux) and a managed one, because Wine's bcrypt has no
/// BCryptDeriveKeyPBKDF2 (the 30.8 lab: CryptographicException 0xC1000008 at the first sign-in). <see cref="Derive"/>
/// uses the platform's when a probe at start gives the managed engine's answer, else the managed one. The managed
/// engine is plain SHA-256 (FIPS 180-4) with the HMAC pads compressed once, so each iteration costs two compressions;
/// it is checked against RFC 7914's vectors and the platform engine (tests/Offline/Auth/Pbkdf2Tests.cs).</summary>
public static class Pbkdf2
{
    private static readonly Lazy<(bool Platform, string Description)> Choice = new(Choose);

    /// <summary>"platform" or "managed (...why...)".</summary>
    public static string Engine => Choice.Value.Description;

    public static byte[] Derive(ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt, int iterations, int length)
    {
        if (Choice.Value.Platform)
            return Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, length);
        var dest = new byte[length];
        Managed(password, salt, iterations, dest);
        return dest;
    }

    private static (bool, string) Choose()
    {
        ReadOnlySpan<byte> pw = "kilnwarden probe"u8, salt = "probe salt 0123456789"u8;
        var managed = new byte[40];
        Managed(pw, salt, 3, managed);
        try
        {
            var platform = Rfc2898DeriveBytes.Pbkdf2(pw, salt, 3, HashAlgorithmName.SHA256, 40);
            return platform.AsSpan().SequenceEqual(managed) ? (true, "platform") : (false, "managed (the platform's answer differs)");
        }
        catch (Exception e) when (e is CryptographicException or PlatformNotSupportedException or EntryPointNotFoundException or DllNotFoundException)
        {
            return (false, $"managed (the platform's failed: {e.Message.Trim()})");
        }
    }

    /// <summary>The managed engine.</summary>
    public static void Managed(ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt, int iterations, Span<byte> dest)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);
        Span<byte> key = stackalloc byte[64];
        key.Clear();
        if (password.Length > 64) Sha256.Hash(Sha256.IV, 0, password, key[..32]);
        else password.CopyTo(key);
        Span<byte> pad = stackalloc byte[64];
        Span<uint> inner = stackalloc uint[8], outer = stackalloc uint[8];
        for (var i = 0; i < 64; i++) pad[i] = (byte)(key[i] ^ 0x36);
        Sha256.IV.CopyTo(inner);
        Sha256.Compress(inner, pad);
        for (var i = 0; i < 64; i++) pad[i] = (byte)(key[i] ^ 0x5c);
        Sha256.IV.CopyTo(outer);
        Sha256.Compress(outer, pad);
        CryptographicOperations.ZeroMemory(key);
        CryptographicOperations.ZeroMemory(pad);

        var msg = new byte[salt.Length + 4];
        salt.CopyTo(msg);
        Span<byte> digest = stackalloc byte[32];
        Span<uint> u = stackalloc uint[8], t = stackalloc uint[8], h = stackalloc uint[8];
        // a 32-byte message after a 64-byte pad block: one block, the padding fixed (0x80, length 96 bytes = 768 bits)
        Span<uint> w = stackalloc uint[16];
        w.Clear();
        w[8] = 0x80000000;
        w[15] = 768;
        var blocks = (dest.Length + 31) / 32;
        Span<byte> word = stackalloc byte[4];
        for (var b = 1; b <= blocks; b++)
        {
            BinaryPrimitives.WriteInt32BigEndian(msg.AsSpan(salt.Length), b);
            Sha256.Hash(inner, 64, msg, digest);
            Sha256.Hash(outer, 64, digest, digest);
            for (var i = 0; i < 8; i++) u[i] = t[i] = BinaryPrimitives.ReadUInt32BigEndian(digest[(i * 4)..]);
            for (var j = 1; j < iterations; j++)
            {
                u.CopyTo(w);
                inner.CopyTo(h);
                Sha256.CompressWords(h, w);
                h.CopyTo(w);
                outer.CopyTo(u);
                Sha256.CompressWords(u, w);
                for (var i = 0; i < 8; i++) t[i] ^= u[i];
            }
            var off = (b - 1) * 32;
            for (var i = 0; i < 8 && off + i * 4 < dest.Length; i++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(word, t[i]);
                var n = Math.Min(4, dest.Length - off - i * 4);
                word[..n].CopyTo(dest[(off + i * 4)..]);
            }
        }
        u.Clear();
        t.Clear();
        h.Clear();
        inner.Clear();
        outer.Clear();
        CryptographicOperations.ZeroMemory(digest);
        CryptographicOperations.ZeroMemory(msg);
    }
}

/// <summary>SHA-256's compression function and padding (FIPS 180-4), for <see cref="Pbkdf2"/>'s managed engine.</summary>
internal static class Sha256
{
    public static ReadOnlySpan<uint> IV => [0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19];

    private static ReadOnlySpan<uint> K =>
    [
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5, 0xd807aa98, 0x12835b01, 0x243185be,
        0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174, 0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa,
        0x5cb0a9dc, 0x76f988da, 0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967, 0x27b70a85,
        0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85, 0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3,
        0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070, 0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f,
        0x682e6ff3, 0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
    ];

    /// <summary>One 64-byte block into <paramref name="h"/>.</summary>
    public static void Compress(Span<uint> h, ReadOnlySpan<byte> block)
    {
        Span<uint> w = stackalloc uint[16];
        for (var i = 0; i < 16; i++) w[i] = BinaryPrimitives.ReadUInt32BigEndian(block[(i * 4)..]);
        CompressWords(h, w);
    }

    /// <summary>One block given as 16 big-endian words into <paramref name="h"/>.</summary>
    public static void CompressWords(Span<uint> h, ReadOnlySpan<uint> block)
    {
        Span<uint> w = stackalloc uint[64];
        block[..16].CopyTo(w);
        for (var t = 16; t < 64; t++)
        {
            uint a = w[t - 15], b = w[t - 2];
            var s0 = BitOperations.RotateRight(a, 7) ^ BitOperations.RotateRight(a, 18) ^ (a >> 3);
            var s1 = BitOperations.RotateRight(b, 17) ^ BitOperations.RotateRight(b, 19) ^ (b >> 10);
            w[t] = w[t - 16] + s0 + w[t - 7] + s1;
        }
        uint x0 = h[0], x1 = h[1], x2 = h[2], x3 = h[3], x4 = h[4], x5 = h[5], x6 = h[6], x7 = h[7];
        var k = K;
        for (var t = 0; t < 64; t++)
        {
            var S1 = BitOperations.RotateRight(x4, 6) ^ BitOperations.RotateRight(x4, 11) ^ BitOperations.RotateRight(x4, 25);
            var ch = (x4 & x5) ^ (~x4 & x6);
            var t1 = x7 + S1 + ch + k[t] + w[t];
            var S0 = BitOperations.RotateRight(x0, 2) ^ BitOperations.RotateRight(x0, 13) ^ BitOperations.RotateRight(x0, 22);
            var maj = (x0 & x1) ^ (x0 & x2) ^ (x1 & x2);
            x7 = x6;
            x6 = x5;
            x5 = x4;
            x4 = x3 + t1;
            x3 = x2;
            x2 = x1;
            x1 = x0;
            x0 = t1 + S0 + maj;
        }
        h[0] += x0;
        h[1] += x1;
        h[2] += x2;
        h[3] += x3;
        h[4] += x4;
        h[5] += x5;
        h[6] += x6;
        h[7] += x7;
    }

    /// <summary>The digest of <paramref name="data"/> continuing from <paramref name="state"/>, which has already taken
    /// <paramref name="prefixBytes"/> bytes (a multiple of 64). <paramref name="digest"/> may overlap
    /// <paramref name="data"/>.</summary>
    public static void Hash(ReadOnlySpan<uint> state, long prefixBytes, ReadOnlySpan<byte> data, Span<byte> digest)
    {
        Span<uint> h = stackalloc uint[8];
        state.CopyTo(h);
        var off = 0;
        for (; data.Length - off >= 64; off += 64) Compress(h, data.Slice(off, 64));
        Span<byte> last = stackalloc byte[128];
        last.Clear();
        var rem = data.Length - off;
        data[off..].CopyTo(last);
        last[rem] = 0x80;
        var size = rem + 9 <= 64 ? 64 : 128;
        BinaryPrimitives.WriteUInt64BigEndian(last[(size - 8)..], (ulong)(prefixBytes + data.Length) * 8);
        Compress(h, last[..64]);
        if (size == 128) Compress(h, last[64..]);
        for (var i = 0; i < 8; i++) BinaryPrimitives.WriteUInt32BigEndian(digest[(i * 4)..], h[i]);
        CryptographicOperations.ZeroMemory(last);
    }
}
