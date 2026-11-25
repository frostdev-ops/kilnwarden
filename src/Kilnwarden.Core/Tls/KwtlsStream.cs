// SPDX-License-Identifier: MIT
using System.Buffers;
using System.Text;

namespace Kilnwarden.Tls;

/// <summary>The plaintext side of one kwtls connection. It moves ciphertext between the socket's stream and the native
/// connection. The SslStream contract: one reader and one writer may run at once (a WebSocket receives while it sends).
/// Every native call is under one lock; ciphertext writes to the socket are serialized by a gate. Buffers come from the
/// pool and go back at dispose, unless an operation is still in flight then (they are left to the GC instead).</summary>
internal sealed class KwtlsStream : TlsStream
{
    private const int CipherChunk = 17 * 1024;   // one full TLS record and its overhead
    private readonly Stream _inner;
    private readonly KwtlsLibrary _lib;
    private readonly object _lock = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private byte[] _rbuf = ArrayPool<byte>.Shared.Rent(CipherChunk);
    private byte[] _wbuf = ArrayPool<byte>.Shared.Rent(CipherChunk * 2);
    private IntPtr _conn;
    private int _rStart, _rEnd;   // ciphertext read from the socket that the library has not taken yet
    private int _busy;            // reads and writes in flight
    private TlsConnectionInfo _info = new("", "", null, null, "kwtls");

    public KwtlsStream(Stream inner, KwtlsLibrary lib, IntPtr conn)
    {
        _inner = inner;
        _lib = lib;
        _conn = conn;
    }

    public override TlsConnectionInfo Info => _info;

    public async Task HandshakeAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref _busy);
        try
        {
            while (true)
            {
                await FlushTlsAsync(ct).ConfigureAwait(false);
                if (!Handshaking()) break;
                if (_rStart == _rEnd && !await FillAsync(ct).ConfigureAwait(false)) throw new IOException("closed during the TLS handshake");
                var err = Feed();
                if (err is not null)
                {
                    await FlushTlsAsync(ct).ConfigureAwait(false); // the alert
                    throw new IOException("TLS handshake: " + err);
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref _busy);
        }
        _info = ReadInfo();
    }

    private async ValueTask<bool> FillAsync(CancellationToken ct)
    {
        var n = await _inner.ReadAsync(_rbuf.AsMemory(0, CipherChunk), ct).ConfigureAwait(false);
        if (n == 0) return false;
        _rStart = 0;
        _rEnd = n;
        return true;
    }

    private unsafe bool Handshaking()
    {
        lock (_lock) return _conn != IntPtr.Zero && _lib.IsHandshaking(_conn) != 0;
    }

    /// <summary>Gives the library what ciphertext it takes; null or the error text.</summary>
    private unsafe string? Feed()
    {
        lock (_lock)
        {
            if (_conn == IntPtr.Zero) return "closed";
            fixed (byte* p = _rbuf)
            {
                var used = _lib.ReadTls(_conn, p + _rStart, (nuint)(_rEnd - _rStart));
                if (used < 0) return _lib.LastError();
                _rStart += (int)used;
                return null;
            }
        }
    }

    private unsafe TlsConnectionInfo ReadInfo()
    {
        var b = stackalloc byte[512];
        string text;
        lock (_lock)
        {
            if (_conn == IntPtr.Zero) return _info;
            var n = _lib.Info(_conn, b, 512);
            text = n > 0 ? Encoding.UTF8.GetString(b, (int)n) : "";
        }
        var f = text.Split('\t');
        string At(int i) => i < f.Length ? f[i] : "";
        return new TlsConnectionInfo(At(0), At(1), At(2) is { Length: > 0 } a ? a : null, At(3) is { Length: > 0 } s ? s : null, "kwtls");
    }

    /// <summary>Sends whatever ciphertext the library has pending (handshake messages, records, alerts, tickets).</summary>
    private async Task FlushTlsAsync(CancellationToken ct)
    {
        await _sendGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            while (true)
            {
                var n = TakeTls();
                if (n <= 0) break;
                await _inner.WriteAsync(_wbuf.AsMemory(0, n), ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private unsafe int TakeTls()
    {
        lock (_lock)
        {
            if (_conn == IntPtr.Zero) return 0;
            fixed (byte* p = _wbuf)
            {
                var n = _lib.WriteTls(_conn, p, (nuint)_wbuf.Length);
                if (n < 0) throw new IOException("TLS: " + _lib.LastError());
                return (int)n;
            }
        }
    }

    private unsafe nint TryRead(Span<byte> dst)
    {
        lock (_lock)
        {
            if (_conn == IntPtr.Zero) return 0;
            fixed (byte* p = dst)
                return _lib.Read(_conn, p, (nuint)dst.Length);
        }
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> dst, CancellationToken ct = default)
    {
        if (dst.IsEmpty) return 0;
        Interlocked.Increment(ref _busy);
        try
        {
            while (true)
            {
                var r = TryRead(dst.Span);
                if (r >= 0) return (int)r;
                if (r == -1) throw new IOException("TLS: " + _lib.LastError());
                if (_rStart == _rEnd && !await FillAsync(ct).ConfigureAwait(false)) return 0;
                var err = Feed();
                await FlushTlsAsync(ct).ConfigureAwait(false);
                if (err is not null) throw new IOException("TLS: " + err);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _busy);
        }
    }

    private unsafe int PutPlain(ReadOnlySpan<byte> src)
    {
        lock (_lock)
        {
            if (_conn == IntPtr.Zero) throw new ObjectDisposedException(nameof(KwtlsStream));
            fixed (byte* p = src)
            {
                var n = _lib.Write(_conn, p, (nuint)src.Length);
                if (n < 0) throw new IOException("TLS: " + _lib.LastError());
                return (int)n;
            }
        }
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> src, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _busy);
        try
        {
            while (!src.IsEmpty)
            {
                var n = PutPlain(src.Span);
                src = src[n..];
                await FlushTlsAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _busy);
        }
    }

    public override Task WriteAsync(byte[] b, int o, int c, CancellationToken ct) => WriteAsync(b.AsMemory(o, c), ct).AsTask();
    public override Task<int> ReadAsync(byte[] b, int o, int c, CancellationToken ct) => ReadAsync(b.AsMemory(o, c), ct).AsTask();
    public override int Read(byte[] b, int o, int c) => ReadAsync(b.AsMemory(o, c)).AsTask().GetAwaiter().GetResult();
    public override void Write(byte[] b, int o, int c) => WriteAsync(b.AsMemory(o, c)).AsTask().GetAwaiter().GetResult();
    public override Task FlushAsync(CancellationToken ct) => FlushTlsAsync(ct);
    public override void Flush() { }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
    public override void SetLength(long v) => throw new NotSupportedException();

    private int _disposed;

    protected override unsafe void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            lock (_lock)
            {
                if (_conn != IntPtr.Zero)
                {
                    _lib.ConnFree(_conn);
                    _conn = IntPtr.Zero;
                }
            }
            // Disposal has no cancellation token. Never write to a potentially stalled transport here; normal
            // application/WebSocket close frames already ran under deadlines. Abort the underlying transport now.
            _inner.Dispose();
            if (Volatile.Read(ref _busy) == 0)
            {
                ArrayPool<byte>.Shared.Return(Interlocked.Exchange(ref _rbuf, []));
                ArrayPool<byte>.Shared.Return(Interlocked.Exchange(ref _wbuf, []));
            }
        }
        base.Dispose(disposing);
    }
}
