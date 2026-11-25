// SPDX-License-Identifier: MIT
using System.Diagnostics.CodeAnalysis;

namespace Kilnwarden.Api;

/// <summary>The way in. A mod that adds to the web panel depends on kilnwarden (optionally, so it still loads without
/// it), and in its OnLoad calls <see cref="WhenAvailable"/>; it disposes the returned handle in OnUnload. The panel
/// starts on a background thread after the mods load, and again after a reload, so the callback runs once per panel
/// start (at once when the panel is already up). Everything a mod registers is removed when the panel stops, when the
/// mod disposes its <see cref="IWebAdminExtension"/>, and when the mod's assembly context unloads.</summary>
public static class WebPanel
{
    /// <summary>This API's version (semver). Additions bump the minor version, removals the major one.</summary>
    public const string ApiVersion = "1.0.0";

    private static readonly object Gate = new();
    private static readonly List<Waiter> Waiters = [];
    private static IWebAdmin? _current;

    /// <summary>The running panel, if it is up.</summary>
    public static bool TryGet([NotNullWhen(true)] out IWebAdmin? admin)
    {
        admin = Volatile.Read(ref _current);
        return admin is not null;
    }

    /// <summary>Runs <paramref name="onAvailable"/> now if the panel is up, and on every later panel start, until the
    /// returned handle is disposed. A callback that throws is skipped (the panel logs it).</summary>
    public static IDisposable WhenAvailable(Action<IWebAdmin> onAvailable)
    {
        ArgumentNullException.ThrowIfNull(onAvailable);
        var w = new Waiter(onAvailable);
        IWebAdmin? now;
        lock (Gate)
        {
            Waiters.Add(w);
            now = _current;
        }
        if (now is not null) w.Run(now);
        return w;
    }

    /// <summary>The panel's host publishes itself at start (internal).</summary>
    internal static void Publish(IWebAdmin admin, Action<Exception>? onError = null)
    {
        Waiter[] waiting;
        lock (Gate)
        {
            _current = admin;
            waiting = [.. Waiters];
        }
        foreach (var w in waiting)
        {
            try
            {
                w.Run(admin);
            }
            catch (Exception e)
            {
                onError?.Invoke(e);
            }
        }
    }

    /// <summary>The panel's host retracts itself at stop (internal).</summary>
    internal static void Retract(IWebAdmin admin)
    {
        lock (Gate)
            if (ReferenceEquals(_current, admin)) _current = null;
    }

    /// <summary>Drops the callbacks whose code lives in <paramref name="owner"/> (its mod unloaded without disposing).</summary>
    internal static int Forget(Func<Delegate, bool> owner)
    {
        lock (Gate) return Waiters.RemoveAll(w => owner(w.Callback));
    }

    internal static int WaiterCount
    {
        get
        {
            lock (Gate) return Waiters.Count;
        }
    }

    private sealed class Waiter(Action<IWebAdmin> callback) : IDisposable
    {
        public Action<IWebAdmin> Callback { get; } = callback;
        private int _disposed;

        public void Run(IWebAdmin admin)
        {
            if (Volatile.Read(ref _disposed) == 0) Callback(admin);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
            lock (Gate) Waiters.Remove(this);
        }
    }
}
