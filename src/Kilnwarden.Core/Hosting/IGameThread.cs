// SPDX-License-Identifier: MIT
namespace Kilnwarden.Hosting;

/// <summary>Runs work on the server's tick thread (the mod project drains a queue in Context.Tick.Pre). Everything else in
/// the web admin runs on its own threads; game state is only touched through here (or through Kiln.Admin over KAP).
/// Work items run in order, a bounded number per step; a failed item faults its task. Pending items fail with
/// <see cref="OperationCanceledException"/> when the mod unloads.</summary>
public interface IGameThread
{
    bool IsGameThread { get; }
    Task<T> InvokeAsync<T>(Func<T> work, CancellationToken ct = default);
}
