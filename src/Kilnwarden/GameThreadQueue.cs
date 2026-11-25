// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using Kilnwarden.Hosting;

namespace Kilnwarden;

/// <summary><see cref="IGameThread"/> for the server: work queued from any thread runs in Tick.Pre, oldest first, at
/// most <see cref="PerStep"/> items per step so a burst never stretches one step. Dispose fails what is still queued.</summary>
internal sealed class GameThreadQueue : IGameThread, IDisposable
{
    public const int PerStep = 16;
    private readonly ConcurrentQueue<Action<bool>> _queue = new();
    private volatile bool _disposed;

    public bool IsGameThread => Game.IsTickThread;

    public Task<T> InvokeAsync<T>(Func<T> work, CancellationToken ct = default)
    {
        if (_disposed) return Task.FromException<T>(new OperationCanceledException("the mod is unloading"));
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reg = ct.CanBeCanceled ? ct.Register(() => tcs.TrySetCanceled(ct)) : default;
        _queue.Enqueue(run =>
        {
            reg.Dispose();
            if (!run)
            {
                tcs.TrySetCanceled();
                return;
            }
            if (tcs.Task.IsCompleted) return;
            try
            {
                tcs.TrySetResult(work());
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
            }
        });
        if (_disposed) Drain(run: false);
        return tcs.Task;
    }

    /// <summary>Tick.Pre: runs up to <see cref="PerStep"/> items.</summary>
    public void Drain() => Drain(run: true);

    private void Drain(bool run)
    {
        for (var i = 0; (i < PerStep || !run) && _queue.TryDequeue(out var item); i++) item(run);
    }

    public void Dispose()
    {
        _disposed = true;
        Drain(run: false);
    }
}
