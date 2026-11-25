// SPDX-License-Identifier: MIT
using System.Diagnostics;
using System.Text;
using Kilnwarden.Auth;
using Kilnwarden.Hosting;
using Kilnwarden.Http;

namespace Kilnwarden.Offline.Auth;

public class PasswordHasherTests
{
    [Fact]
    public async Task Defaults_are_pbkdf2_sha256_600k_and_hashes_verify()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PasswordHasher(599_999, 1));
        using var h = new PasswordHasher(PasswordHasher.MinIterations, 2);
        var s = await h.HashAsync("hunter2 hunter2", CancellationToken.None);
        Assert.StartsWith("pbkdf2-sha256$600000$", s);
        Assert.True(PasswordHasher.TryParse(s, out var it, out var salt, out var hash));
        Assert.Equal(600_000, it);
        Assert.Equal(16, salt.Length);
        Assert.Equal(32, hash.Length);
        Assert.Equal((true, false), await h.VerifyAsync("hunter2 hunter2", s, CancellationToken.None));
        Assert.Equal((false, false), await h.VerifyAsync("hunter2 hunter3", s, CancellationToken.None));
        Assert.NotEqual(s, await h.HashAsync("hunter2 hunter2", CancellationToken.None)); // salted
    }

    [Fact]
    public async Task Weaker_hashes_verify_and_ask_for_a_rehash()
    {
        using var weak = new PasswordHasher(1000, 1, null, null, allowWeak: true);
        using var strong = new PasswordHasher(2000, 1, null, null, allowWeak: true);
        var s = await weak.HashAsync("pw pw pw pw", CancellationToken.None);
        Assert.Equal((true, true), await strong.VerifyAsync("pw pw pw pw", s, CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_accounts_cost_the_same_as_real_ones()
    {
        using var h = new PasswordHasher(100_000, 1, null, null, allowWeak: true);
        var s = await h.HashAsync("x", CancellationToken.None);
        // the fastest of several runs each: other tests share the CPU, the minimum is what the work itself costs
        async Task<double> Fastest(string? stored)
        {
            var best = double.MaxValue;
            for (var i = 0; i < 7; i++)
            {
                var sw = Stopwatch.StartNew();
                Assert.False((await h.VerifyAsync("guess", stored, CancellationToken.None)).Ok);
                best = Math.Min(best, sw.Elapsed.TotalMilliseconds);
            }
            return best;
        }
        var real = await Fastest(s);
        var unknown = await Fastest(null);
        var unreadable = await Fastest("pbkdf2-sha256$x$y$z");
        Assert.InRange(unknown / real, 0.6, 1.6);
        Assert.InRange(unreadable / real, 0.6, 1.6);
    }

    [Fact]
    public async Task A_hand_edited_iteration_count_cannot_burn_the_cpu()
    {
        using var h = new PasswordHasher(1000, 1, null, null, allowWeak: true);
        var evil = PasswordHasher.Format(90_000_000, new byte[16], new byte[32]);
        var sw = Stopwatch.StartNew();
        Assert.False((await h.VerifyAsync("x", evil, CancellationToken.None)).Ok);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Hashing_is_bounded_and_never_runs_on_the_tick_thread()
    {
        // no wait for a slot: the second call is refused while the first (about half a second) holds the only one
        using var h = new PasswordHasher(2_000_000, 1, null, TimeSpan.Zero, allowWeak: true);
        var running = h.HashAsync("a long one", CancellationToken.None);
        var e = await Assert.ThrowsAsync<HttpError>(() => h.HashAsync("second", CancellationToken.None));
        Assert.Equal(503, e.Status);
        await running;
        using var onTick = new PasswordHasher(1000, 1, new TickThread(), null, allowWeak: true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => onTick.HashAsync("x", CancellationToken.None));
        await Assert.ThrowsAsync<HttpError>(() => h.HashAsync(new string('x', 2000), CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("pbkdf2-sha256$600000$$")]
    [InlineData("pbkdf2-sha1$600000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("pbkdf2-sha256$-1$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("pbkdf2-sha256$600000$!!$AAAA")]
    [InlineData("pbkdf2-sha256$600000$AAAAAAAAAAAAAAAAAAAAAA==$AAAA$x")]
    public void Malformed_hashes_parse_as_false(string s) => Assert.False(PasswordHasher.TryParse(s, out _, out _, out _));

    private sealed class TickThread : IGameThread
    {
        public bool IsGameThread => true;
        public Task<T> InvokeAsync<T>(Func<T> work, CancellationToken ct = default) => Task.FromResult(work());
    }
}

public class TotpTests
{
    // RFC 6238 appendix B (SHA-1 key "12345678901234567890"), last 6 of the 8 digits
    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    public void Codes_match_rfc6238(long unix, string code)
    {
        var key = Encoding.ASCII.GetBytes("12345678901234567890");
        Assert.Equal(code, Totp.Code(key, unix / 30));
    }

    [Fact]
    public void Verify_allows_one_step_of_drift_and_never_the_same_step_twice()
    {
        var key = Totp.NewSecret();
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var step = Totp.StepAt(now);
        Assert.Equal(step, Totp.Verify(key, Totp.Code(key, step), now, 0));
        Assert.Equal(step - 1, Totp.Verify(key, Totp.Code(key, step - 1), now, 0));
        Assert.Equal(step + 1, Totp.Verify(key, Totp.Code(key, step + 1), now, 0));
        Assert.Null(Totp.Verify(key, Totp.Code(key, step - 2), now, 0));
        Assert.Null(Totp.Verify(key, Totp.Code(key, step), now, step));      // replay
        Assert.Null(Totp.Verify(key, Totp.Code(key, step - 1), now, step - 1));
        Assert.Equal(step, Totp.Verify(key, Totp.Code(key, step)[..3] + " " + Totp.Code(key, step)[3..], now, 0));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12345a")]
    [InlineData("１２３４５６")]
    public void Codes_are_exactly_six_ascii_digits(string? code) => Assert.False(Totp.TryNormalize(code, out _));

    [Fact]
    public void Base32_round_trips_and_the_uri_names_issuer_and_account()
    {
        var key = Totp.NewSecret();
        var b32 = Base32.Encode(key);
        Assert.Equal(key, Base32.Decode(b32));
        Assert.Equal(key, Base32.Decode(b32.ToLowerInvariant()));
        Assert.Null(Base32.Decode("AB1"));
        Assert.Equal("MZXW6YTBOI", Base32.Encode("foobar"u8));
        var uri = Totp.Uri(key, "Kilnwarden", "alice");
        Assert.StartsWith("otpauth://totp/Kilnwarden:alice?secret=" + b32, uri);
        Assert.Contains("period=30", uri);
    }
}

public class RecoveryCodeTests
{
    [Fact]
    public void Ten_single_use_codes_stored_as_hashes()
    {
        var (codes, hashes) = RecoveryCodes.Generate();
        Assert.Equal(10, codes.Count);
        Assert.Equal(10, codes.Distinct().Count());
        Assert.All(codes, c => Assert.Matches("^[A-Z2-9]{5}-[A-Z2-9]{5}$", c));
        Assert.DoesNotContain(codes[0], string.Join(",", hashes));
        Assert.Equal(3, RecoveryCodes.Match(hashes, codes[3]));
        Assert.Equal(3, RecoveryCodes.Match(hashes, codes[3].ToLowerInvariant().Replace("-", " ")));
        Assert.Equal(-1, RecoveryCodes.Match(hashes, "AAAAA-AAAAA"));
        Assert.Equal(-1, RecoveryCodes.Match(hashes, null));
        Assert.Equal(-1, RecoveryCodes.Match(hashes, codes[3] + "A"));
    }

    [Fact]
    public void Tokens_are_43_base64url_characters()
    {
        var t = SecretToken.New();
        Assert.True(SecretToken.IsWellFormed(t));
        Assert.False(SecretToken.IsWellFormed(t + "A"));
        Assert.False(SecretToken.IsWellFormed(t[..42] + "="));
        Assert.False(SecretToken.IsWellFormed(null));
        Assert.Equal(64, SecretToken.Hash(t).Length);
        Assert.NotEqual(SecretToken.New(), SecretToken.New());
    }
}
