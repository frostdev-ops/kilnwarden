// SPDX-License-Identifier: MIT
// The schedule of the panel's check of its own address (0.1.4): a passed check runs again after 5 minutes, then 10, 20
// and at most 30 while each one passes. Any failure, and any change of what the check stands for (the certificate, the
// relay link, the name, the listener), starts again at 5. A passed check counts for the launcher's address until its
// recheck is a minute overdue, so a check that stopped running (no owner, the launcher's login off) names nothing.
using Kilnwarden.Setup;

namespace Kilnwarden.Offline.Setup;

public sealed class RecheckScheduleTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly object Listener = new();
    private static readonly RecheckSchedule.Context Hosted = new("hosted:alpha.kiln.nexus:AB:CD:trusted", 1, Listener);

    private static SelfProbe.Result Pass(DateTimeOffset at) => new(true, "ok", null, at);
    private static SelfProbe.Result Fail(DateTimeOffset at) => new(false, "connect", "cannot connect", at);
    private static TimeSpan Min(double m) => TimeSpan.FromMinutes(m);

    [Fact]
    public void A_passed_check_runs_again_after_5_then_10_20_and_at_most_30_minutes_while_each_passes()
    {
        var s = new RecheckSchedule();
        Assert.Equal(Min(5), s.Every(Hosted));
        var seen = new List<TimeSpan>();
        for (var i = 0; i < 8; i++)
        {
            s.Checked(Hosted, true);
            seen.Add(s.Every(Hosted));
        }
        Assert.Equal([Min(5), Min(10), Min(20), Min(30), Min(30), Min(30), Min(30), Min(30)], seen);
        // due exactly when its time is up, measured from the last check
        Assert.False(s.Due(Hosted, Pass(T0), T0 + Min(30) - TimeSpan.FromSeconds(1)));
        Assert.True(s.Due(Hosted, Pass(T0), T0 + Min(30)));
        // many passes never overflow the schedule
        for (var i = 0; i < 200; i++) s.Checked(Hosted, true);
        Assert.Equal(Min(30), s.Every(Hosted));
    }

    [Fact]
    public void A_failed_check_starts_the_schedule_again_at_5_minutes()
    {
        var s = new RecheckSchedule();
        for (var i = 0; i < 3; i++) s.Checked(Hosted, true);
        Assert.Equal(Min(20), s.Every(Hosted));
        s.Checked(Hosted, false);
        Assert.Equal(Min(5), s.Every(Hosted));
        // a failed check itself runs again after 10 seconds, as before
        Assert.False(s.Due(Hosted, Fail(T0), T0 + TimeSpan.FromSeconds(9)));
        Assert.True(s.Due(Hosted, Fail(T0), T0 + TimeSpan.FromSeconds(10)));
        s.Checked(Hosted, true);
        Assert.Equal(Min(5), s.Every(Hosted));
        s.Checked(Hosted, true);
        Assert.Equal(Min(10), s.Every(Hosted));
    }

    public static TheoryData<string> Changes => ["certificate", "name", "relay link", "listener"];

    private static RecheckSchedule.Context Changed(string what) => what switch
    {
        "certificate" => Hosted with { Key = "hosted:alpha.kiln.nexus:EF:01:trusted" },
        "name" => Hosted with { Key = "hosted:beta.kiln.nexus:AB:CD:trusted" },
        "relay link" => Hosted with { Link = Hosted.Link + 1 },
        "listener" => Hosted with { Listener = new object() },
        _ => throw new ArgumentOutOfRangeException(nameof(what)),
    };

    [Theory]
    [MemberData(nameof(Changes))]
    public void Each_kind_of_change_starts_the_schedule_again_at_5_minutes(string what)
    {
        var s = new RecheckSchedule();
        for (var i = 0; i < 5; i++) s.Checked(Hosted, true);
        Assert.Equal(Min(30), s.Every(Hosted));
        var now = Changed(what);
        Assert.NotEqual(Hosted, now);
        // the last pass is 6 minutes old: due at once after the change, not 24 minutes later
        Assert.Equal(Min(5), s.Every(now));
        Assert.True(s.Due(now, Pass(T0), T0 + Min(6)));
        Assert.False(s.Due(Hosted, Pass(T0), T0 + Min(6)));
        // and it grows again from there
        s.Checked(now, true);
        Assert.Equal(Min(5), s.Every(now));
        s.Checked(now, true);
        Assert.Equal(Min(10), s.Every(now));
    }

    [Fact]
    public void The_same_check_again_is_no_change()
    {
        var s = new RecheckSchedule();
        s.Checked(Hosted, true);
        s.Checked(new RecheckSchedule.Context(Hosted.Key, Hosted.Link, Listener), true);
        Assert.Equal(Min(10), s.Every(Hosted));
    }

    [Fact]
    public void A_pass_counts_for_the_launchers_address_until_its_recheck_is_a_minute_overdue()
    {
        var s = new RecheckSchedule();
        s.Checked(Hosted, true);
        s.Checked(Hosted, true); // every 10 minutes now
        Assert.True(s.Holds(Hosted, Pass(T0), T0 + Min(10)));
        Assert.True(s.Holds(Hosted, Pass(T0), T0 + Min(11)));
        Assert.False(s.Holds(Hosted, Pass(T0), T0 + Min(11) + TimeSpan.FromSeconds(1)));
        // a failed check never counts
        Assert.False(s.Holds(Hosted, Fail(T0), T0));
        // after a change the last pass is measured against 5 minutes: a 7-minute-old pass waits for a new check
        Assert.False(s.Holds(Changed("relay link"), Pass(T0), T0 + Min(7)));
        Assert.True(s.Holds(Changed("relay link"), Pass(T0), T0 + Min(6)));
    }

    [Fact]
    public void Test_intervals_keep_the_same_shape()
    {
        var s = new RecheckSchedule(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(600), TimeSpan.FromMilliseconds(50));
        var seen = new List<TimeSpan>();
        for (var i = 0; i < 5; i++)
        {
            s.Checked(Hosted, true);
            seen.Add(s.Every(Hosted));
        }
        Assert.Equal([100, 200, 400, 600, 600], seen.Select(t => (int)t.TotalMilliseconds));
        Assert.False(s.Holds(Hosted, Pass(T0), T0 + TimeSpan.FromMilliseconds(651)));
        // without a longest interval of their own, tests get six times the first, as 5 and 30 minutes
        var scaled = new RecheckSchedule(TimeSpan.FromMilliseconds(100));
        for (var i = 0; i < 6; i++) scaled.Checked(Hosted, true);
        Assert.Equal(TimeSpan.FromMilliseconds(600), scaled.Every(Hosted));
    }
}
