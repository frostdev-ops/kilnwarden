// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Sockets;

namespace Kilnwarden.Auth;

/// <summary>Rate limits and lockouts for everything that checks a secret (sign-in, two-factor, setup, the launcher's
/// one-time login): a per-address bucket of attempts, and failure counters per address and per account name that lock
/// with doubling backoff past their free failures. Unknown names are counted exactly like real ones, so a lockout says
/// nothing about whether an account exists. IPv6 clients count per /64. Bounded memory (oldest entries go first).</summary>
public sealed class SignInLimiter
{
    public const int MaxEntries = 10_000;

    private readonly TimeProvider _time;
    private readonly Func<SignInLimitSettings> _settings;
    private readonly object _lock = new();
    private readonly Dictionary<string, Bucket> _buckets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Failures> _failures = new(StringComparer.Ordinal);

    public SignInLimiter(Func<SignInLimitSettings> settings, TimeProvider? time = null)
    {
        _settings = settings;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Counts one attempt from <paramref name="ip"/> against its bucket and checks the address's and the
    /// account key's locks. Returns how long to wait, or null when the attempt may go ahead.</summary>
    public TimeSpan? Attempt(IPAddress ip, string? accountKey)
    {
        var now = _time.GetUtcNow();
        var s = _settings();
        var ipKey = "ip:" + Prefix(ip);
        lock (_lock)
        {
            if (Locked(ipKey, now) is { } ipWait) return ipWait;
            if (accountKey is not null && Locked("acct:" + accountKey, now) is { } acctWait) return acctWait;
            if (Take(ipKey, s.AttemptsPerMinutePerIp, now) is { } ipBudget) return ipBudget;
            if (accountKey is not null && Take("acct:" + accountKey, s.AttemptsPerMinutePerAccount, now) is { } accountBudget) return accountBudget;
            Trim(_buckets, now);
        }
        return null;
    }

    private TimeSpan? Take(string key, int capacity, DateTimeOffset now)
    {
        var rate = capacity / 60.0;
        var b = _buckets.GetValueOrDefault(key) ?? new Bucket(capacity, now);
        var tokens = Math.Clamp(b.Tokens + Math.Max(0, (now - b.At).TotalSeconds) * rate, 0, capacity);
        _buckets[key] = new Bucket(tokens >= 1 ? tokens - 1 : tokens, now);
        // Refused attempts must not grow the table without bound either.
        Trim(_buckets, now);
        return tokens >= 1 ? null : TimeSpan.FromSeconds(Math.Ceiling((1 - tokens) / rate));
    }

    /// <summary>A failed check: counts against the address and the account key (an unknown name too).</summary>
    public void Fail(IPAddress ip, string? accountKey)
    {
        var now = _time.GetUtcNow();
        var s = _settings();
        lock (_lock)
        {
            Count("ip:" + Prefix(ip), s.IpFreeFailures, s, now);
            if (accountKey is not null) Count("acct:" + accountKey, s.AccountFreeFailures, s, now);
            Trim(_failures, now);
        }
    }

    /// <summary>A good check: the account key starts over (the address keeps its count: one good account must not clear
    /// the failures of guessing others).</summary>
    public void Succeed(string? accountKey)
    {
        if (accountKey is null) return;
        lock (_lock) _failures.Remove("acct:" + accountKey);
    }

    private void Count(string key, int free, SignInLimitSettings s, DateTimeOffset now)
    {
        var f = _failures.GetValueOrDefault(key);
        // a counter forgets after a quiet day
        var n = f is null || now - f.Last > TimeSpan.FromDays(1) ? 1 : f.Count + 1;
        var lockedUntil = f?.LockedUntil ?? DateTimeOffset.MinValue;
        if (n >= free)
        {
            var seconds = Math.Min(s.LockoutMaxSeconds, s.LockoutBaseSeconds * Math.Pow(2, Math.Min(30, n - free)));
            lockedUntil = now.AddSeconds(seconds);
        }
        _failures[key] = new Failures(n, lockedUntil, now);
    }

    private TimeSpan? Locked(string key, DateTimeOffset now) =>
        _failures.TryGetValue(key, out var f) && f.LockedUntil > now ? f.LockedUntil - now : null;

    private static void Trim<T>(Dictionary<string, T> d, DateTimeOffset now) where T : IAged
    {
        if (d.Count <= MaxEntries) return;
        foreach (var k in d.Where(kv => now - kv.Value.Age > TimeSpan.FromHours(1)).Select(kv => kv.Key).ToList()) d.Remove(k);
        if (d.Count <= MaxEntries) return;
        foreach (var k in d.OrderBy(kv => kv.Value.Age).Take(d.Count - MaxEntries * 9 / 10).Select(kv => kv.Key).ToList()) d.Remove(k);
    }

    /// <summary>The address as counted: IPv4 itself, IPv6 its /64.</summary>
    public static string Prefix(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (ip.AddressFamily != AddressFamily.InterNetworkV6) return ip.ToString();
        var b = ip.GetAddressBytes();
        Array.Clear(b, 8, 8);
        return new IPAddress(b) + "/64";
    }

    private interface IAged
    {
        DateTimeOffset Age { get; }
    }

    private sealed record Bucket(double Tokens, DateTimeOffset At) : IAged
    {
        public DateTimeOffset Age => At;
    }

    private sealed record Failures(int Count, DateTimeOffset LockedUntil, DateTimeOffset Last) : IAged
    {
        // a locked entry is never the first to go
        public DateTimeOffset Age => LockedUntil > Last ? LockedUntil : Last;
    }
}
