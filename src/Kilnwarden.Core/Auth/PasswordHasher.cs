// SPDX-License-Identifier: MIT
using System.Security.Cryptography;
using System.Text;
using Kilnwarden.Hosting;
using Kilnwarden.Http;

namespace Kilnwarden.Auth;

/// <summary>Local account passwords: PBKDF2-HMAC-SHA256 (DESIGN §3: 600,000 iterations; <see cref="Pbkdf2"/>: .NET's own,
/// or a managed engine where the platform has none, as under Wine), a 16-byte
/// random salt, a 32-byte hash, stored as "pbkdf2-sha256$&lt;iterations&gt;$&lt;salt b64&gt;$&lt;hash b64&gt;".
/// Hashing costs a few hundred milliseconds of CPU on purpose, which makes it a denial-of-service lever: at most
/// <c>maxConcurrent</c> run at once on pool threads, a caller that cannot get a slot within a few seconds gets 503, and
/// it never runs on the tick thread (it throws there).</summary>
public sealed class PasswordHasher : IDisposable
{
    public const string Scheme = "pbkdf2-sha256";
    public const int MinIterations = 600_000;
    public const int SaltBytes = 16, HashBytes = 32;

    /// <summary>Longer passwords are refused before hashing (PBKDF2 pre-hashes long keys anyway; this bounds the work).</summary>
    public const int MaxPasswordBytes = 1024;

    private readonly SemaphoreSlim _slots;
    private readonly IGameThread? _game;
    private readonly TimeSpan _wait;
    private readonly string _dummy;
    public const int MaxScheduledHashes = 16;
    private int _scheduled;

    public PasswordHasher(int iterations, int maxConcurrent, IGameThread? game = null, TimeSpan? wait = null)
        : this(iterations, maxConcurrent, game, wait, allowWeak: false)
    {
    }

    /// <param name="allowWeak">Tests only: fewer than <see cref="MinIterations"/> iterations.</param>
    internal PasswordHasher(int iterations, int maxConcurrent, IGameThread? game, TimeSpan? wait, bool allowWeak)
    {
        if (iterations < MinIterations && !allowWeak) throw new ArgumentOutOfRangeException(nameof(iterations), $"at least {MinIterations}");
        Iterations = iterations;
        _slots = new SemaphoreSlim(Math.Clamp(maxConcurrent, 1, 64));
        _game = game;
        _wait = wait ?? TimeSpan.FromSeconds(5);
        // what an unknown name is checked against, so it costs the same as a real one (no user enumeration by timing)
        _dummy = Format(iterations, RandomNumberGenerator.GetBytes(SaltBytes), RandomNumberGenerator.GetBytes(HashBytes));
    }

    public int Iterations { get; }

    /// <summary>A new hash of <paramref name="password"/>. Throws <see cref="HttpError"/> 503 when every slot stays busy.</summary>
    public async Task<string> HashAsync(string password, CancellationToken ct)
    {
        var pw = Bytes(password);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = await RunAsync(() => Pbkdf2.Derive(pw, salt, Iterations, HashBytes), pw, ct).ConfigureAwait(false);
        return Format(Iterations, salt, hash);
    }

    /// <summary>Whether <paramref name="password"/> matches <paramref name="stored"/> (constant-time compare). With
    /// <paramref name="stored"/> null or unreadable it is checked against a dummy hash at the same cost and is false.
    /// <c>Rehash</c>: it matched with fewer iterations than now.</summary>
    public async Task<(bool Ok, bool Rehash)> VerifyAsync(string password, string? stored, CancellationToken ct)
    {
        var real = stored is not null && TryParse(stored, out _, out _, out _);
        TryParse(real ? stored! : _dummy, out var iterations, out var salt, out var expected);
        // an attacker-written accounts.json could ask for a billion iterations: never more than 10x the current count
        if (iterations > Iterations * 10) (real, iterations) = (false, Iterations);
        var pw = Bytes(password);
        var got = await RunAsync(() => Pbkdf2.Derive(pw, salt, iterations, expected.Length), pw, ct).ConfigureAwait(false);
        var ok = CryptographicOperations.FixedTimeEquals(got, expected) && real;
        return (ok, ok && iterations < Iterations);
    }

    /// <summary>One dummy verification: a refused sign-in that skipped the real check takes as long as one that did not.</summary>
    public Task BurnAsync(string password, CancellationToken ct) => VerifyAsync(password, null, ct);

    private async Task<byte[]> RunAsync(Func<byte[]> work, byte[] pw, CancellationToken ct)
    {
        var scheduled = Interlocked.Increment(ref _scheduled) <= MaxScheduledHashes;
        try
        {
            if (!scheduled) throw new HttpError(503, "the password check queue is full: try again later");
            if (_game?.IsGameThread == true) throw new InvalidOperationException("password hashing must never run on the tick thread");
            if (!await _slots.WaitAsync(_wait, ct).ConfigureAwait(false))
                throw new HttpError(503, "the server is busy checking passwords: try again in a moment");
            try
            {
                return await Task.Run(work, ct).ConfigureAwait(false);
            }
            finally
            {
                _slots.Release();
            }
        }
        finally
        {
            Interlocked.Decrement(ref _scheduled);
            CryptographicOperations.ZeroMemory(pw);
        }
    }

    private static byte[] Bytes(string password)
    {
        var b = Encoding.UTF8.GetBytes(password);
        if (b.Length > MaxPasswordBytes)
        {
            CryptographicOperations.ZeroMemory(b);
            throw new HttpError(400, $"a password is at most {MaxPasswordBytes} bytes");
        }
        return b;
    }

    public static string Format(int iterations, byte[] salt, byte[] hash) =>
        $"{Scheme}${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";

    /// <summary>Parses a stored hash (fuzz target): false for anything malformed, never throws.</summary>
    public static bool TryParse(string s, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0;
        salt = hash = [];
        if (s.Length > 512) return false;
        var parts = s.Split('$');
        if (parts.Length != 4 || parts[0] != Scheme) return false;
        if (!int.TryParse(parts[1], System.Globalization.NumberStyles.None, null, out iterations) || iterations is < 1 or > 100_000_000) return false;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            hash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }
        return salt.Length is >= 8 and <= 64 && hash.Length is >= 16 and <= 64;
    }

    public void Dispose() => _slots.Dispose();
}
