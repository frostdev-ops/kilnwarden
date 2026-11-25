// SPDX-License-Identifier: MIT
using System.Buffers.Binary;
using System.Numerics;

namespace Kilnwarden.Ops;

/// <summary>BLAKE2b-512 (RFC 7693), unkeyed, streaming: what minisign hashes a file with before signing it ("ED"
/// signatures). .NET has no BLAKE2; this is the RFC's reference algorithm, checked against hashlib's output
/// (tests/Offline/Ops/minisign-vectors.json).</summary>
public sealed class Blake2b
{
    private static readonly ulong[] Iv =
    [
        0x6a09e667f3bcc908UL, 0xbb67ae8584caa73bUL, 0x3c6ef372fe94f82bUL, 0xa54ff53a5f1d36f1UL,
        0x510e527fade682d1UL, 0x9b05688c2b3e6c1fUL, 0x1f83d9abfb41bd6bUL, 0x5be0cd19137e2179UL,
    ];

    private static readonly byte[][] Sigma =
    [
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15],
        [14, 10, 4, 8, 9, 15, 13, 6, 1, 12, 0, 2, 11, 7, 5, 3],
        [11, 8, 12, 0, 5, 2, 15, 13, 10, 14, 3, 6, 7, 1, 9, 4],
        [7, 9, 3, 1, 13, 12, 11, 14, 2, 6, 5, 10, 4, 0, 15, 8],
        [9, 0, 5, 7, 2, 4, 10, 15, 14, 1, 11, 12, 6, 8, 3, 13],
        [2, 12, 6, 10, 0, 11, 8, 3, 4, 13, 7, 5, 15, 14, 1, 9],
        [12, 5, 1, 15, 14, 13, 4, 10, 0, 7, 6, 3, 9, 2, 8, 11],
        [13, 11, 7, 14, 12, 1, 3, 9, 5, 0, 15, 4, 8, 6, 2, 10],
        [6, 15, 14, 9, 11, 3, 0, 8, 12, 2, 13, 7, 1, 4, 10, 5],
        [10, 2, 8, 4, 7, 6, 1, 5, 15, 11, 9, 14, 3, 12, 13, 0],
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15],
        [14, 10, 4, 8, 9, 15, 13, 6, 1, 12, 0, 2, 11, 7, 5, 3],
    ];

    public const int OutBytes = 64, BlockBytes = 128;

    private readonly ulong[] _h = new ulong[8];
    private readonly byte[] _buf = new byte[BlockBytes];
    private int _fill;
    private ulong _t0, _t1;
    private bool _done;

    public Blake2b()
    {
        Iv.CopyTo(_h, 0);
        _h[0] ^= 0x01010000UL ^ OutBytes; // fanout 1, depth 1, no key, 64-byte digest
    }

    public static byte[] Hash(ReadOnlySpan<byte> data)
    {
        var b = new Blake2b();
        b.Update(data);
        return b.Final();
    }

    public static byte[] HashFile(string path)
    {
        var b = new Blake2b();
        using var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
        var chunk = new byte[1 << 16];
        int n;
        while ((n = f.Read(chunk, 0, chunk.Length)) > 0) b.Update(chunk.AsSpan(0, n));
        return b.Final();
    }

    public void Update(ReadOnlySpan<byte> data)
    {
        if (_done) throw new InvalidOperationException("the digest is final");
        while (data.Length > 0)
        {
            // the last block is compressed in Final (with the final flag), so a full buffer waits for more input
            if (_fill == BlockBytes)
            {
                Count(BlockBytes);
                Compress(_buf, last: false);
                _fill = 0;
            }
            var n = Math.Min(BlockBytes - _fill, data.Length);
            data[..n].CopyTo(_buf.AsSpan(_fill));
            _fill += n;
            data = data[n..];
        }
    }

    public byte[] Final()
    {
        if (_done) throw new InvalidOperationException("the digest is final");
        _done = true;
        Count((ulong)_fill);
        _buf.AsSpan(_fill).Clear();
        Compress(_buf, last: true);
        var o = new byte[OutBytes];
        for (var i = 0; i < 8; i++) BinaryPrimitives.WriteUInt64LittleEndian(o.AsSpan(i * 8), _h[i]);
        return o;
    }

    private void Count(ulong n)
    {
        _t0 += n;
        if (_t0 < n) _t1++;
    }

    private void Compress(ReadOnlySpan<byte> block, bool last)
    {
        Span<ulong> m = stackalloc ulong[16];
        Span<ulong> v = stackalloc ulong[16];
        for (var i = 0; i < 16; i++) m[i] = BinaryPrimitives.ReadUInt64LittleEndian(block[(i * 8)..]);
        for (var i = 0; i < 8; i++)
        {
            v[i] = _h[i];
            v[i + 8] = Iv[i];
        }
        v[12] ^= _t0;
        v[13] ^= _t1;
        if (last) v[14] = ~v[14];
        for (var r = 0; r < 12; r++)
        {
            var s = Sigma[r];
            G(v, 0, 4, 8, 12, m[s[0]], m[s[1]]);
            G(v, 1, 5, 9, 13, m[s[2]], m[s[3]]);
            G(v, 2, 6, 10, 14, m[s[4]], m[s[5]]);
            G(v, 3, 7, 11, 15, m[s[6]], m[s[7]]);
            G(v, 0, 5, 10, 15, m[s[8]], m[s[9]]);
            G(v, 1, 6, 11, 12, m[s[10]], m[s[11]]);
            G(v, 2, 7, 8, 13, m[s[12]], m[s[13]]);
            G(v, 3, 4, 9, 14, m[s[14]], m[s[15]]);
        }
        for (var i = 0; i < 8; i++) _h[i] ^= v[i] ^ v[i + 8];
    }

    private static void G(Span<ulong> v, int a, int b, int c, int d, ulong x, ulong y)
    {
        v[a] = v[a] + v[b] + x;
        v[d] = BitOperations.RotateRight(v[d] ^ v[a], 32);
        v[c] = v[c] + v[d];
        v[b] = BitOperations.RotateRight(v[b] ^ v[c], 24);
        v[a] = v[a] + v[b] + y;
        v[d] = BitOperations.RotateRight(v[d] ^ v[a], 16);
        v[c] = v[c] + v[d];
        v[b] = BitOperations.RotateRight(v[b] ^ v[c], 63);
    }
}
