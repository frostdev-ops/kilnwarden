// SPDX-License-Identifier: MIT
namespace Kilnwarden.Server;

/// <summary>A stream that first returns bytes already read (past the upgrade request), then the inner stream's.</summary>
internal sealed class PrefixStream(ReadOnlyMemory<byte> prefix, Stream inner) : Stream
{
    private ReadOnlyMemory<byte> _prefix = prefix;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => inner.Flush();
    public override Task FlushAsync(CancellationToken ct) => inner.FlushAsync(ct);
    public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
    public override void SetLength(long v) => throw new NotSupportedException();
    public override int Read(byte[] b, int o, int c) => Read(b.AsSpan(o, c));

    public override int Read(Span<byte> dst)
    {
        if (_prefix.IsEmpty) return inner.Read(dst);
        var n = Math.Min(dst.Length, _prefix.Length);
        _prefix.Span[..n].CopyTo(dst);
        _prefix = _prefix[n..];
        return n;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> dst, CancellationToken ct = default)
    {
        if (_prefix.IsEmpty) return inner.ReadAsync(dst, ct);
        var n = Math.Min(dst.Length, _prefix.Length);
        _prefix[..n].CopyTo(dst);
        _prefix = _prefix[n..];
        return ValueTask.FromResult(n);
    }

    public override Task<int> ReadAsync(byte[] b, int o, int c, CancellationToken ct) => ReadAsync(b.AsMemory(o, c), ct).AsTask();
    public override void Write(byte[] b, int o, int c) => inner.Write(b, o, c);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> src, CancellationToken ct = default) => inner.WriteAsync(src, ct);
    public override Task WriteAsync(byte[] b, int o, int c, CancellationToken ct) => inner.WriteAsync(b, o, c, ct);

    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }
}
