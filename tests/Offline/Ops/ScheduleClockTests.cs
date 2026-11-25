// SPDX-License-Identifier: MIT
using Kilnwarden.Ops;

namespace Kilnwarden.Offline.Ops;

public class ScheduleClockTests
{
    private static DateTimeOffset U(int d, int h, int m = 0) => new(2026, 3, d, h, m, 0, TimeSpan.Zero);

    /// <summary>+01:00, summer time +02:00 from the last Sunday of March 02:00 to the last Sunday of October 03:00 (as
    /// central Europe), built here so the test does not depend on the OS's zone files.</summary>
    internal static TimeZoneInfo Euro()
    {
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 5, DayOfWeek.Sunday),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 5, DayOfWeek.Sunday));
        return TimeZoneInfo.CreateCustomTimeZone("Test/Euro", TimeSpan.FromHours(1), "Test", "Test", "Test summer", [rule]);
    }

    [Fact]
    public void Daily_utc_next_and_latest()
    {
        var w = new ScheduleWhen { Type = "daily", Times = ["06:00", "18:30"] };
        Assert.Equal(U(10, 6), ScheduleClock.Next(w, TimeZoneInfo.Utc, U(10, 5, 59)));
        Assert.Equal(U(10, 18, 30), ScheduleClock.Next(w, TimeZoneInfo.Utc, U(10, 6)));
        Assert.Equal(U(11, 6), ScheduleClock.Next(w, TimeZoneInfo.Utc, U(10, 18, 30)));
        Assert.Equal(U(10, 18, 30), ScheduleClock.Latest(w, TimeZoneInfo.Utc, U(1, 0), U(11, 5)));
        Assert.Null(ScheduleClock.Latest(w, TimeZoneInfo.Utc, U(10, 18, 30), U(11, 5)));
    }

    [Fact]
    public void Weekdays_filter_the_days()
    {
        // 2026-03-10 is a Tuesday
        var w = new ScheduleWhen { Type = "daily", Times = ["06:00"], Days = ["sat"] };
        Assert.Equal(U(14, 6), ScheduleClock.Next(w, TimeZoneInfo.Utc, U(10, 7)));
        Assert.Equal(U(7, 6), ScheduleClock.Latest(w, TimeZoneInfo.Utc, U(1, 0), U(13, 23)));
    }

    [Fact]
    public void A_wall_clock_time_in_a_zone_follows_summer_time()
    {
        var tz = Euro();
        var w = new ScheduleWhen { Type = "daily", Times = ["06:00"] };
        // winter: 06:00 +01 = 05:00 UTC; summer (after 2026-03-29): 06:00 +02 = 04:00 UTC
        Assert.Equal(U(28, 5), ScheduleClock.Next(w, tz, U(28, 0)));
        Assert.Equal(U(29, 4), ScheduleClock.Next(w, tz, U(28, 6)));
    }

    [Fact]
    public void A_time_that_does_not_exist_runs_at_the_first_minute_after_and_one_that_exists_twice_runs_once()
    {
        var tz = Euro();
        var spring = new ScheduleWhen { Type = "daily", Times = ["02:30"] };
        // 2026-03-29 02:30 local does not exist (02:00 -> 03:00): 03:00 +02 = 01:00 UTC
        Assert.Equal(U(29, 1), ScheduleClock.Next(spring, tz, U(28, 12)));
        // 2026-10-25 02:30 local exists twice (+02 then +01): the first, 00:30 UTC, and the next one is the day after
        var fall = new ScheduleWhen { Type = "daily", Times = ["02:30"] };
        var first = new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero);
        Assert.Equal(first, ScheduleClock.Next(fall, tz, new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero)));
        Assert.Equal(new DateTimeOffset(2026, 10, 26, 1, 30, 0, TimeSpan.Zero), ScheduleClock.Next(fall, tz, first));
    }

    [Fact]
    public void Interval_and_once()
    {
        var w = new ScheduleWhen { Type = "interval", EveryMinutes = 90, Anchor = U(10, 0) };
        Assert.Equal(U(10, 0), ScheduleClock.Next(w, TimeZoneInfo.Utc, U(9, 0)));
        Assert.Equal(U(10, 1, 30), ScheduleClock.Next(w, TimeZoneInfo.Utc, U(10, 0)));
        Assert.Equal(U(10, 4, 30), ScheduleClock.Latest(w, TimeZoneInfo.Utc, U(10, 0), U(10, 5)));
        var once = new ScheduleWhen { Type = "once", At = U(12, 8) };
        Assert.Equal(U(12, 8), ScheduleClock.Next(once, TimeZoneInfo.Utc, U(1, 0)));
        Assert.Null(ScheduleClock.Next(once, TimeZoneInfo.Utc, U(12, 8)));
    }

    [Fact]
    public void Zones_are_utc_local_or_known_ids_only()
    {
        var time = new OpsTime(U(1, 0), Euro());
        Assert.Same(TimeZoneInfo.Utc, ScheduleClock.Zone("UTC", time));
        Assert.Equal("Test/Euro", ScheduleClock.Zone("local", time)!.Id);
        Assert.Null(ScheduleClock.Zone("Mars/Olympus_Mons", time));
        Assert.Null(ScheduleClock.Zone(new string('a', 100), time));
    }
}
