// SPDX-License-Identifier: MIT
using System.Net.WebSockets;

namespace Kilnwarden.Relay;

// The TLS engine reads the original encrypted byte stream from the relay, independent of its WebSocket boundaries.
internal sealed class RelayStream(WebSocket socket) : Stream
{
    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty) return 0;
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close) return 0;
            if (result.MessageType != WebSocketMessageType.Binary) throw new IOException("Expected relay binary data.");
            if (result.Count != 0) return result.Count;
        }
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (!buffer.IsEmpty)
        {
            var count = Math.Min(buffer.Length, 16 * 1024);
            await socket.SendAsync(buffer[..count], WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
            buffer = buffer[count..];
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) socket.Dispose();
        base.Dispose(disposing);
    }
}
