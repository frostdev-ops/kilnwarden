// SPDX-License-Identifier: MIT
using System.Net.WebSockets;
using Kilnwarden.Routing;

namespace Kilnwarden.WebSockets;

/// <summary>Runs one accepted WebSocket until it closes. The socket comes from WebSocket.CreateFromStream (server
/// side, 20 s keep-alive pings). When the server stops, <see cref="WebSocketSession.Stopping"/> fires and the host
/// sends close 1001 itself; the handler should return soon after.</summary>
public delegate Task WebSocketHandler(WebSocketSession ctx);

public sealed class WebSocketSession
{
    public required RequestContext Request { get; init; }
    public required WebSocket Socket { get; init; }

    /// <summary>Cancelled at server stop or when the connection drops.</summary>
    public CancellationToken Stopping { get; init; }

    /// <summary>The cap for one incoming message (settings: limits.maxWebSocketMessageBytes).</summary>
    public int MaxMessageBytes { get; init; } = 1 << 20;

    private readonly SemaphoreSlim _send = new(1, 1);

    /// <summary>Sends one message; safe to call from several tasks (sends are serialized).</summary>
    public async Task SendAsync(ReadOnlyMemory<byte> message, WebSocketMessageType type = WebSocketMessageType.Text, CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, Stopping);
        await _send.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            if (Socket.State == WebSocketState.Open)
                await Socket.SendAsync(message, type, true, linked.Token).ConfigureAwait(false);
        }
        finally
        {
            _send.Release();
        }
    }

    /// <summary>Receives one whole message into a buffer of its own. Null when the peer closed (the close is answered).
    /// A message over <see cref="MaxMessageBytes"/> closes the socket with 1009 and returns null.</summary>
    public async Task<WebSocketMessage?> ReceiveAsync(CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, Stopping);
        var buf = new byte[Math.Min(4096, MaxMessageBytes)];
        var len = 0;
        while (true)
        {
            if (len == buf.Length)
            {
                if (buf.Length >= MaxMessageBytes)
                {
                    await CloseAsync(WebSocketCloseStatus.MessageTooBig, "message too big").ConfigureAwait(false);
                    return null;
                }
                Array.Resize(ref buf, (int)Math.Min((long)buf.Length * 2, MaxMessageBytes));
            }
            var r = await Socket.ReceiveAsync(buf.AsMemory(len), linked.Token).ConfigureAwait(false);
            if (r.MessageType == WebSocketMessageType.Close)
            {
                if (Socket.State == WebSocketState.CloseReceived)
                    await CloseAsync(WebSocketCloseStatus.NormalClosure, "").ConfigureAwait(false);
                return null;
            }
            len += r.Count;
            if (r.EndOfMessage) return new WebSocketMessage(r.MessageType, buf.AsMemory(0, len));
        }
    }

    /// <summary>Sends a close frame (under the send lock) unless one went out already.</summary>
    public async Task CloseAsync(WebSocketCloseStatus status, string reason)
    {
        using var t = new CancellationTokenSource(1000);
        try
        {
            await _send.WaitAsync(t.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Socket.Abort();
            return;
        }
        try
        {
            if (Socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                await Socket.CloseOutputAsync(status, reason, t.Token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is WebSocketException or OperationCanceledException or IOException or ObjectDisposedException)
        {
            Socket.Abort();
        }
        finally
        {
            _send.Release();
        }
    }
}

public readonly record struct WebSocketMessage(WebSocketMessageType Type, ReadOnlyMemory<byte> Data);
