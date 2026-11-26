// SPDX-License-Identifier: MIT
namespace Kilnwarden.Setup;

/// <summary>When a passed check of the panel's address runs again (0.1.4): 5 minutes after the first pass, then 10, 20
/// and at most 30 while each check passes. A failed check, or any change of what the check stands for (the certificate,
/// the relay link, the name, the listener: <see cref="Context"/>), starts it again at 5; a failed check itself runs
/// again after 10 seconds. A passed check counts for the launcher's address (<see cref="Holds"/>) until its recheck is
/// <see cref="Grace"/> overdue, so a pass whose recheck did not run in time (a pause in the checks) names nothing; a
/// pause that ends before then leaves the pass counting, as it would have anyway. Thread-safe.</summary>
internal sealed class RecheckSchedule
{
    public static readonly TimeSpan First = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Longest = TimeSpan.FromMinutes(30);

    /// <summary>The 15-second round that starts a recheck and the check's own 12 seconds fit in it.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(1);

    public static readonly TimeSpan AfterFailure = TimeSpan.FromSeconds(10);

    private readonly TimeSpan _first;
    private readonly TimeSpan _longest;
    private readonly TimeSpan _grace;
    private readonly Lock _lock = new();
    private Context? _for;
    private int _passes;

    /// <param name="longest">Default: six times <paramref name="first"/> (5 and 30 minutes).</param>
    public RecheckSchedule(TimeSpan? first = null, TimeSpan? longest = null, TimeSpan? grace = null)
    {
        _first = first ?? First;
        _longest = longest ?? _first * (Longest / First);
        _grace = grace ?? Grace;
    }

    /// <summary>What a check stands for: its key (the route, the name, the port, the certificate, the trust), how many
    /// times the relay link has come up or gone down, and the listener that serves the panel (compared by reference).</summary>
    public readonly record struct Context(string Key, int Link, object? Listener);

    /// <summary>How long a passed check holds before it runs again, for what the check stands for now.</summary>
    public TimeSpan Every(Context now)
    {
        lock (_lock)
        {
            if (_passes == 0 || _for != now) return _first;
            var t = _first;
            for (var i = 1; i < _passes && t < _longest; i++) t *= 2;
            return t < _longest ? t : _longest;
        }
    }

    /// <summary>A check for <paramref name="c"/> ended: a pass grows the schedule (or starts it, for a new context), a
    /// failure starts it again.</summary>
    public void Checked(Context c, bool ok)
    {
        lock (_lock)
        {
            _passes = !ok ? 0 : _for == c ? Math.Min(_passes + 1, 64) : 1;
            _for = c;
        }
    }

    /// <summary>Whether the check runs again now: <see cref="AfterFailure"/> after a failure, else after <see cref="Every"/>.</summary>
    public bool Due(Context c, SelfProbe.Result last, DateTimeOffset now) => now - last.At >= (last.Ok ? Every(c) : AfterFailure);

    /// <summary>Whether <paramref name="last"/> passed and still counts: no more than <see cref="Grace"/> past its recheck.</summary>
    public bool Holds(Context c, SelfProbe.Result last, DateTimeOffset now) => last.Ok && now - last.At <= Every(c) + _grace;
}
