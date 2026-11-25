// SPDX-License-Identifier: MIT
using System.Buffers;
using Kilnwarden.Http;

namespace Kilnwarden.Server;

/// <summary>Reads request heads and bodies from a connection through one pooled buffer; bytes past a request stay for
/// the next one (pipelining) or for the WebSocket (<see cref="TakeLeftover"/>). Deadlines: an idle connection waits at
/// most keepAliveMs for a request's first byte, then the whole head must arrive within headerMs of it.</summary>
internal sealed class RequestReader(Stream stream) : IDisposable
{
    private byte[] _buf = ArrayPool<byte>.Shared.Rent(8 * 1024);
    private int _start, _end;

    public bool HasBuffered => _end > _start;

    /// <summary>The next request's head, or null when the peer closed cleanly between requests. Throws
    /// <see cref="HttpError"/> (refuse), <see cref="TimeoutException"/> (a deadline passed), IOException.</summary>
    public async ValueTask<HttpRequest?> ReadHeadAsync(HttpLimits limits, int keepAliveMs, int headerMs, CancellationToken ct)
    {
        var need = limits.MaxHeaderBytes * 2 + 8;
        if (_buf.Length < need) Grow(need);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var started = HasBuffered;
        deadline.CancelAfter(started ? headerMs : keepAliveMs);
        while (true)
        {
            if (HasBuffered)
            {
                if (HttpParser.TryParseHead(_buf.AsSpan(_start, _end - _start), limits, out var req, out var consumed) == ParseStatus.Complete)
                {
                    _start += consumed;
                    if (_start == _end) _start = _end = 0;
                    return req;
                }
            }
            Compact();
            int n;
            try
            {
                n = await stream.ReadAsync(_buf.AsMemory(_end), deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException(started ? "request head too slow" : "idle");
            }
            if (n == 0)
            {
                if (!HasBuffered) return null;
                throw new EndOfStreamException("closed inside a request head");
            }
            _end += n;
            if (!started)
            {
                started = true;
                deadline.CancelAfter(headerMs);
            }
        }
    }

    /// <summary>Reads exactly <paramref name="length"/> body bytes within <paramref name="timeoutMs"/>.</summary>
    public async ValueTask<byte[]> ReadBodyAsync(long length, int timeoutMs, CancellationToken ct)
    {
        if (length < 0 || length > Array.MaxLength) throw new HttpError(413, "request body too large");
        // the buffer grows with the bytes that actually arrive (review I4): a declared length alone costs at most 64 KiB
        var body = new byte[(int)Math.Min(length, Math.Max(64 << 10, _end - _start))];
        var have = (int)Math.Min(length, _end - _start);
        _buf.AsSpan(_start, have).CopyTo(body);
        _start += have;
        if (_start == _end) _start = _end = 0;
        var off = have;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeoutMs);
        while (off < length)
        {
            if (off == body.Length) Array.Resize(ref body, (int)Math.Min(length, (long)body.Length * 2));
            int n;
            try
            {
                n = await stream.ReadAsync(body.AsMemory(off), deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException("request body too slow");
            }
            if (n == 0) throw new EndOfStreamException("closed inside a request body");
            off += n;
        }
        return body;
    }

    /// <summary>The bytes read past the last request (a WebSocket's first frames); the reader is empty after.</summary>
    public ReadOnlyMemory<byte> TakeLeftover()
    {
        var copy = _buf.AsSpan(_start, _end - _start).ToArray();
        _start = _end = 0;
        return copy;
    }

    private void Compact()
    {
        if (_start == 0) return;
        Buffer.BlockCopy(_buf, _start, _buf, 0, _end - _start);
        _end -= _start;
        _start = 0;
    }

    private void Grow(int size)
    {
        var next = ArrayPool<byte>.Shared.Rent(size);
        Buffer.BlockCopy(_buf, _start, next, 0, _end - _start);
        _end -= _start;
        _start = 0;
        ArrayPool<byte>.Shared.Return(_buf);
        _buf = next;
    }

    public void Dispose()
    {
        var b = Interlocked.Exchange(ref _buf, []);
        if (b.Length > 0) ArrayPool<byte>.Shared.Return(b);
    }
}
