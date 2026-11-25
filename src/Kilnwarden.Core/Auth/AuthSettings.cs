// SPDX-License-Identifier: MIT
namespace Kilnwarden.Auth;

/// <summary>auth.json in the data folder (hand-editable, hot-reloaded; the hashing settings apply at the next start).</summary>
public sealed class AuthSettings
{
    public int Format { get; set; } = 1;

    /// <summary>A session ends after this long without a request.</summary>
    public int SessionIdleMinutes { get; set; } = 60;

    /// <summary>A session ends this long after sign-in, whatever its use.</summary>
    public int SessionAbsoluteHours { get; set; } = 24;

    /// <summary>The oldest session of an account ends when it signs in once more than this.</summary>
    public int MaxSessionsPerAccount { get; set; } = 10;

    public int PasswordMinLength { get; set; } = 12;

    /// <summary>PBKDF2-SHA256 iterations for new hashes (at least 600,000). Applies at the next start.</summary>
    public int Pbkdf2Iterations { get; set; } = 600_000;

    /// <summary>Password checks that may run at once (each uses one core for a few hundred ms). Applies at the next start.</summary>
    public int MaxConcurrentHashes { get; set; } = 2;

    public SignInLimitSettings SignIn { get; set; } = new();

    /// <summary>The launcher's "Open web panel": a one-time secret minted over KAP (login_mint) signs in as the owner.</summary>
    public bool LauncherLogin { get; set; } = true;

    /// <summary>WebSocket (KAP) connections per account at once.</summary>
    public int MaxSocketsPerAccount { get; set; } = 8;

    public static IReadOnlyList<string> Validate(AuthSettings s)
    {
        var p = new List<string>();
        if (s.Format != 1) p.Add($"format {s.Format} is not supported (1)");
        if (s.SessionIdleMinutes is < 5 or > 24 * 60) p.Add("sessionIdleMinutes must be 5..1440");
        if (s.SessionAbsoluteHours is < 1 or > 24 * 30) p.Add("sessionAbsoluteHours must be 1..720");
        if (s.MaxSessionsPerAccount is < 1 or > 100) p.Add("maxSessionsPerAccount must be 1..100");
        if (s.PasswordMinLength is < 8 or > 128) p.Add("passwordMinLength must be 8..128");
        if (s.Pbkdf2Iterations is < PasswordHasher.MinIterations or > 10_000_000) p.Add($"pbkdf2Iterations must be {PasswordHasher.MinIterations}..10000000");
        if (s.MaxConcurrentHashes is < 1 or > 16) p.Add("maxConcurrentHashes must be 1..16");
        if (s.MaxSocketsPerAccount is < 1 or > 64) p.Add("maxSocketsPerAccount must be 1..64");
        if (s.SignIn is null) { p.Add("signIn must be an object"); return p; }
        var l = s.SignIn;
        if (l.AttemptsPerMinutePerAccount is < 1 or > 60) p.Add("signIn.attemptsPerMinutePerAccount must be 1..60");
        if (l.AttemptsPerMinutePerIp is < 1 or > 600) p.Add("signIn.attemptsPerMinutePerIp must be 1..600");
        if (l.AccountFreeFailures is < 1 or > 100 || l.IpFreeFailures is < 1 or > 1000) p.Add("signIn: free failures must be positive (account 1..100, IP 1..1000)");
        if (l.LockoutBaseSeconds is < 1 or > 3600 || l.LockoutMaxSeconds < l.LockoutBaseSeconds || l.LockoutMaxSeconds > 24 * 3600)
            p.Add("signIn: lockoutBaseSeconds 1..3600, lockoutMaxSeconds from it to 86400");
        return p;
    }
}

/// <summary>Sign-in rate limits and lockouts (per client address and per account name, with backoff).</summary>
public sealed class SignInLimitSettings
{
    /// <summary>Sign-in, two-factor, setup and launcher attempts per minute from one address (IPv6: per /64).</summary>
    public int AttemptsPerMinutePerIp { get; set; } = 30;

    /// <summary>Strict per-account attempt bucket across addresses and proof methods; success never refills it.</summary>
    public int AttemptsPerMinutePerAccount { get; set; } = 10;

    /// <summary>Failures for one account name that lock it (the next attempt waits); each further failure doubles the lock.</summary>
    public int AccountFreeFailures { get; set; } = 5;

    /// <summary>Failures from one address (any names) that lock it.</summary>
    public int IpFreeFailures { get; set; } = 20;

    public int LockoutBaseSeconds { get; set; } = 30;
    public int LockoutMaxSeconds { get; set; } = 900;
}
