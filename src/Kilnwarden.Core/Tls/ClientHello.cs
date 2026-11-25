// SPDX-License-Identifier: MIT
using System.Buffers.Binary;
using System.Text;

namespace Kilnwarden.Tls;

/// <summary>What a TLS ClientHello asks for: the server name (SNI) and the ALPN protocols.</summary>
public sealed record ClientHelloInfo(string? ServerName, IReadOnlyList<string> Alpn);

/// <summary>Reads the first TLS record of a connection and the ClientHello in it, before either TLS engine sees the
/// bytes (they are handed on unchanged), so the listener can answer an ACME TLS-ALPN-01 validation (RFC 8737) with the
/// challenge certificate. Bounded and defensive: anything that is not a well-formed ClientHello in the first record is
/// "no information", never an error (the engine then fails or serves it as usual). A fuzz target.</summary>
public static class ClientHello
{
    public const int MaxRecord = 16384 + 2048;

    /// <summary>Parses one TLS record (5-byte header and its body); null unless it is a ClientHello. A ClientHello
    /// longer than the record is read as far as the record goes.</summary>
    public static ClientHelloInfo? Parse(ReadOnlySpan<byte> record)
    {
        if (record.Length < 5 || record[0] != 0x16 || record[1] != 0x03) return null;
        var len = BinaryPrimitives.ReadUInt16BigEndian(record[3..]);
        if (len > MaxRecord || record.Length < 5 + len) return null;
        var hs = record.Slice(5, len);
        if (hs.Length < 4 || hs[0] != 0x01) return null;
        var bodyLen = (hs[1] << 16) | (hs[2] << 8) | hs[3];
        var body = hs[4..];
        if (body.Length > bodyLen) body = body[..bodyLen];
        var r = new Reader(body);
        if (!r.Skip(2 + 32)) return null; // legacy_version, random
        if (!r.Vector8(out _) || !r.Vector16(out _) || !r.Vector8(out _)) return null; // session id, cipher suites, compression
        if (r.Left == 0) return new ClientHelloInfo(null, []);
        if (!r.Vector16Partial(out var exts)) return null;
        string? sni = null;
        var alpn = new List<string>();
        var e = new Reader(exts);
        while (e.Left >= 4)
        {
            e.U16(out var type);
            if (!e.Vector16(out var data)) break;
            var d = new Reader(data);
            if (type == 0x0000 && sni is null && d.Vector16(out var list))
            {
                var l = new Reader(list);
                while (l.Left > 0 && l.U8(out var kind) && l.Vector16(out var name))
                    if (kind == 0 && name.Length is > 0 and <= 253 && IsHostName(name))
                    {
                        sni = Encoding.ASCII.GetString(name).ToLowerInvariant();
                        break;
                    }
            }
            else if (type == 0x0010 && alpn.Count == 0 && d.Vector16(out var protos))
            {
                var p = new Reader(protos);
                while (p.Left > 0 && alpn.Count < 32 && p.Vector8(out var proto))
                    if (proto.Length > 0 && proto.ToArray().All(b => b is > 0x20 and < 0x7f)) alpn.Add(Encoding.ASCII.GetString(proto));
            }
        }
        return new ClientHelloInfo(sni, alpn);
    }

    private static bool IsHostName(ReadOnlySpan<byte> s)
    {
        foreach (var b in s)
            if (!(b is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z' or >= (byte)'0' and <= (byte)'9' or (byte)'-' or (byte)'.')) return false;
        return true;
    }

    /// <summary>Reads the first record from <paramref name="s"/> (at most <see cref="MaxRecord"/> + 5 bytes): the bytes
    /// read (to replay to the engine) and the ClientHello in them, if any.</summary>
    public static async ValueTask<(byte[] Read, ClientHelloInfo? Hello)> PeekAsync(Stream s, CancellationToken ct)
    {
        var head = new byte[5];
        var got = await ReadAtLeastAsync(s, head, 5, ct).ConfigureAwait(false);
        if (got < 5 || head[0] != 0x16) return (head[..got], null);
        var len = BinaryPrimitives.ReadUInt16BigEndian(head.AsSpan(3));
        if (len > MaxRecord) return (head, null);
        var all = new byte[5 + len];
        head.CopyTo(all, 0);
        got = await ReadAtLeastAsync(s, all.AsMemory(5), len, ct).ConfigureAwait(false);
        if (got < len) return (all[..(5 + got)], null);
        return (all, Parse(all));
    }

    private static async ValueTask<int> ReadAtLeastAsync(Stream s, Memory<byte> buf, int need, CancellationToken ct)
    {
        var n = 0;
        while (n < need)
        {
            var r = await s.ReadAsync(buf[n..need], ct).ConfigureAwait(false);
            if (r == 0) break;
            n += r;
        }
        return n;
    }

    private ref struct Reader(ReadOnlySpan<byte> data)
    {
        private ReadOnlySpan<byte> _d = data;
        public readonly int Left => _d.Length;

        public bool Skip(int n)
        {
            if (_d.Length < n) return false;
            _d = _d[n..];
            return true;
        }

        public bool U8(out byte v)
        {
            v = 0;
            if (_d.Length < 1) return false;
            v = _d[0];
            _d = _d[1..];
            return true;
        }

        public bool U16(out ushort v)
        {
            v = 0;
            if (_d.Length < 2) return false;
            v = BinaryPrimitives.ReadUInt16BigEndian(_d);
            _d = _d[2..];
            return true;
        }

        public bool Vector8(out ReadOnlySpan<byte> v)
        {
            v = default;
            if (!U8(out var n) || _d.Length < n) return false;
            v = _d[..n];
            _d = _d[n..];
            return true;
        }

        public bool Vector16(out ReadOnlySpan<byte> v)
        {
            v = default;
            if (!U16(out var n) || _d.Length < n) return false;
            v = _d[..n];
            _d = _d[n..];
            return true;
        }

        /// <summary>A 16-bit vector that may be cut short by the end of the record (a ClientHello split over records).</summary>
        public bool Vector16Partial(out ReadOnlySpan<byte> v)
        {
            v = default;
            if (!U16(out var n)) return false;
            v = _d[..Math.Min(n, _d.Length)];
            _d = _d[v.Length..];
            return true;
        }
    }
}
