// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using Kilnwarden.Auth;

namespace Kilnwarden.Identity;

public enum IntentKind
{
    /// <summary>Sign in to the account linked to the identity.</summary>
    Login,

    /// <summary>Link the identity to the signed-in account that started it.</summary>
    Link,
}

/// <summary>One external sign-in in progress: created by /start, verified by the provider's callback, redeemed once by
/// /complete from the same browser (the binder cookie) within <see cref="LoginIntents.Lifetime"/>.</summary>
public sealed class LoginIntent
{
    public required string Provider { get; init; }
    public required IntentKind Kind { get; init; }

    /// <summary>SHA-256 of the binder (the HttpOnly cookie set by /start).</summary>
    public required string BinderHash { get; init; }

    /// <summary>For a link: the session that started it (it must be the one that completes it).</summary>
    public string? SessionHandle { get; init; }

    /// <summary>The exact return URL the provider must report (Steam's openid.return_to, the broker's "ret").</summary>
    public required string ReturnUrl { get; init; }

    /// <summary>A random value the provider echoes (the broker's assertion nonce).</summary>
    public required string Nonce { get; init; }

    public required DateTimeOffset Expires { get; init; }

    /// <summary>Set once by the callback: the verified identity or the reason it failed.</summary>
    public ExternalIdentity? Verified => Volatile.Read(ref _result)?.Identity;

    public string? Error => Volatile.Read(ref _result)?.Error;
    private sealed record Settlement(ExternalIdentity? Identity, string? Error);
    private Settlement? _result;

    public bool Settle(ExternalIdentity? id, string? error) =>
        Interlocked.CompareExchange(ref _result, new Settlement(id, id is null ? error ?? "failed" : null), null) is null;

    public bool Settled => Volatile.Read(ref _result) is not null;
}

/// <summary>The pending external sign-ins, keyed by the hash of their state. Short-lived, one use, bounded (a flood of
/// /start calls drops the oldest), and kept in memory only: a reload cancels sign-ins in progress.</summary>
public sealed class LoginIntents(TimeProvider time)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    public const int MaxPending = 1000;

    private readonly ConcurrentDictionary<string, LoginIntent> _all = new(StringComparer.Ordinal);

    public int Count => _all.Count;

    /// <summary>A new intent; returns its state (goes to the provider) and binder (goes in the browser's cookie).</summary>
    public (string State, string Binder, LoginIntent Intent) Create(string provider, IntentKind kind, string? sessionHandle, Func<string, string> returnUrl)
    {
        Purge();
        var state = SecretToken.New();
        var binder = SecretToken.New();
        var intent = new LoginIntent
        {
            Provider = provider, Kind = kind, BinderHash = SecretToken.Hash(binder), SessionHandle = sessionHandle, ReturnUrl = returnUrl(state),
            Nonce = SecretToken.New(), Expires = time.GetUtcNow() + Lifetime,
        };
        _all[SecretToken.Hash(state)] = intent;
        return (state, binder, intent);
    }

    /// <summary>The live intent of a state (not removed: the callback settles it, /complete takes it).</summary>
    public LoginIntent? Find(string? state)
    {
        if (!SecretToken.IsWellFormed(state) || !_all.TryGetValue(SecretToken.Hash(state!), out var i)) return null;
        if (i.Expires > time.GetUtcNow()) return i;
        _all.TryRemove(SecretToken.Hash(state!), out _);
        return null;
    }

    /// <summary>Removes and returns the intent when the binder matches; a wrong binder leaves it (the right browser may
    /// still complete it) and returns null.</summary>
    public LoginIntent? Take(string? state, string? binder)
    {
        if (Find(state) is not { } i || !SecretToken.IsWellFormed(binder) || !SecretToken.HashEquals(SecretToken.Hash(binder!), i.BinderHash)) return null;
        return _all.TryRemove(SecretToken.Hash(state!), out var taken) && ReferenceEquals(taken, i) ? i : null;
    }

    private void Purge()
    {
        var now = time.GetUtcNow();
        foreach (var (k, v) in _all)
            if (v.Expires <= now) _all.TryRemove(k, out _);
        if (_all.Count >= MaxPending)
            foreach (var k in _all.OrderBy(kv => kv.Value.Expires).Take(_all.Count - MaxPending + 1).Select(kv => kv.Key).ToList())
                _all.TryRemove(k, out _);
    }
}
