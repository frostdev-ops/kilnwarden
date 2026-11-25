// SPDX-License-Identifier: MIT
using System.Globalization;

namespace Kilnwarden.Ops;

/// <summary>When a schedule's moments are (docs/step2-lane-b-ops.md): daily wall-clock times in a time zone (optionally on
/// some weekdays), every N minutes from an anchor, or once. Pure functions of the rule, the zone and an instant.
/// A daily time that does not exist (spring forward) maps to the first minute that exists after it; one that exists twice
/// (fall back) maps to its first occurrence, so it happens once.</summary>
public static class ScheduleClock
{
    public static readonly string[] DayNames = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];

    /// <summary>The first moment strictly after <paramref name="after"/>, or null (a once rule that has passed).</summary>
    public static DateTimeOffset? Next(ScheduleWhen w, TimeZoneInfo tz, DateTimeOffset after)
    {
        switch (w.Type)
        {
            case "once":
                return w.At is { } at && at > after ? at.ToUniversalTime() : null;
            case "interval":
            {
                var every = TimeSpan.FromMinutes(w.EveryMinutes ?? 0);
                if (every <= TimeSpan.Zero || w.Anchor is not { } anchor) return null;
                if (after < anchor) return anchor.ToUniversalTime();
                var k = (after - anchor).Ticks / every.Ticks + 1;
                return (anchor + TimeSpan.FromTicks(every.Ticks * k)).ToUniversalTime();
            }
            case "daily":
            {
                DateTimeOffset? best = null;
                var start = TimeZoneInfo.ConvertTime(after, tz).Date.AddDays(-1);
                for (var d = 0; d <= 9; d++)
                    foreach (var m in DailyMoments(w, tz, start.AddDays(d)))
                        if (m > after && (best is null || m < best)) best = m;
                return best;
            }
            default:
                return null;
        }
    }

    /// <summary>The newest moment in (<paramref name="after"/>, <paramref name="at"/>], or null.</summary>
    public static DateTimeOffset? Latest(ScheduleWhen w, TimeZoneInfo tz, DateTimeOffset after, DateTimeOffset at)
    {
        if (at <= after) return null;
        switch (w.Type)
        {
            case "once":
                return w.At is { } once && once > after && once <= at ? once.ToUniversalTime() : null;
            case "interval":
            {
                var every = TimeSpan.FromMinutes(w.EveryMinutes ?? 0);
                if (every <= TimeSpan.Zero || w.Anchor is not { } anchor || at < anchor) return null;
                var m = anchor + TimeSpan.FromTicks(every.Ticks * ((at - anchor).Ticks / every.Ticks));
                return m > after ? m.ToUniversalTime() : null;
            }
            case "daily":
            {
                DateTimeOffset? best = null;
                var end = TimeZoneInfo.ConvertTime(at, tz).Date.AddDays(1);
                for (var d = 0; d <= 9; d++)
                    foreach (var m in DailyMoments(w, tz, end.AddDays(-d)))
                        if (m > after && m <= at && (best is null || m > best)) best = m;
                return best;
            }
            default:
                return null;
        }
    }

    private static IEnumerable<DateTimeOffset> DailyMoments(ScheduleWhen w, TimeZoneInfo tz, DateTime localDate)
    {
        var day = DayNames[(int)localDate.DayOfWeek];
        if (w.Days is { Count: > 0 } days && !days.Contains(day)) yield break;
        foreach (var t in w.Times ?? [])
            if (TryParseTime(t, out var tod)) yield return ToUtc(localDate.Date + tod, tz);
    }

    /// <summary>A wall-clock time in <paramref name="tz"/> as an instant (the rules above).</summary>
    public static DateTimeOffset ToUtc(DateTime local, TimeZoneInfo tz)
    {
        var dt = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        for (var i = 0; i < 24 * 60 && tz.IsInvalidTime(dt); i++) dt = dt.AddMinutes(1);
        if (tz.IsAmbiguousTime(dt)) return new DateTimeOffset(dt, tz.GetAmbiguousTimeOffsets(dt).Max()).ToUniversalTime();
        return new DateTimeOffset(dt, tz.GetUtcOffset(dt)).ToUniversalTime();
    }

    public static bool TryParseTime(string? s, out TimeSpan time)
    {
        time = default;
        if (s is not { Length: 5 } || s[2] != ':') return false;
        if (!TimeSpan.TryParseExact(s, @"hh\:mm", CultureInfo.InvariantCulture, out time)) return false;
        return time < TimeSpan.FromDays(1);
    }

    /// <summary>"UTC", "local" (the server's zone, from <paramref name="time"/>) or a zone id this OS knows; null when unknown.</summary>
    public static TimeZoneInfo? Zone(string? id, TimeProvider time)
    {
        switch (id)
        {
            case null or "" or "UTC":
                return TimeZoneInfo.Utc;
            case "local":
                return time.LocalTimeZone;
        }
        if (id.Length > 64) return null;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            return null;
        }
    }
}
