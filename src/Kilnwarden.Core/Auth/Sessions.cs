// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using System.Text.Json;
using Kilnwarden.Hosting;
using Kilnwarden.Storage;

namespace Kilnwarden.Auth;

/// <summary>One signed-in browser. The cookie holds a random token; only its SHA-256 (<see cref="Hash"/>) is kept, so
/// sessions.json holds nothing that signs anyone in. <see cref="Handle"/> is the public name used to list and revoke it.</summary>
public sealed record SessionRecord
{
    public string Hash { get; init; } = "";

    /// <summary>"s-" + 16 hex characters: shown in the session list, used to revoke; signs nobody in.</summary>
    public string Handle { get; init; } = "";

    public string AccountId { get; init; } = "";
    public long CredentialStamp { get; init; }
    public long PrivilegeStamp { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset LastSeen { get; init; }

    /// <summary>The absolute end (sign-in + sessionAbsoluteHours).</summary>
    public DateTimeOffset Expires { get; init; }

    public string Ip { get; init; } = "";
    public string UserAgent { get; init; } = "";

    /// <summary>password, password+2fa, recovery-code, setup, launcher.</summary>
    public string Via { get; init; } = "";

    /// <summary>Signed in with a second factor (or the launcher's admin-token login, which is stronger than one).</summary>
    public bool Mfa { get; init; }

    /// <summary>Explicit password/TOTP proof on this session only (never inferred from launcher login or recovery).</summary>
    public DateTimeOffset? ReauthenticatedUntil { get; init; }

    public bool HasRecentAuthentication(DateTimeOffset now) =>
        ReauthenticatedUntil is { } until && until > now && until <= now + AuthService.ReauthLifetime;
}

/// <summary>The server-side session store (DESIGN §3): opaque random ids, idle and absolute expiry, rotation, revoke,
/// a cap per account. Kept in memory and in sessions.json (a secret file, written a few seconds after a change and at
/// stop) so a mod reload does not sign everyone out.</summary>
public sealed class SessionStore : IDisposable
{
    public const string File = "sessions.json";

    private readonly DataStore _data;
    private readonly TimeProvider _time;
    private readonly Func<AuthSettings> _settings;
    private readonly IWebAdminLog _log;
    private readonly ConcurrentDictionary<string, SessionRecord> _byHash = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _handleToHash = new(StringComparer.Ordinal);
    private readonly object _lock = new();
    private ITimer? _flush;
    private int _dirty;

    public SessionStore(DataStore data, Func<AuthSettings> settings, IWebAdminLog log, TimeProvider? time = null)
    {
        _data = data;
        _settings = settings;
        _log = log;
        _time = time ?? TimeProvider.System;
        try
        {
            if (data.ReadJson<SessionsFile>(File) is { Format: 1, Sessions: { } list })
                foreach (var s in list)
                    if (s is { Hash.Length: 64, Handle: not null, AccountId: not null } && s.Handle.StartsWith("s-", StringComparison.Ordinal) && Alive(s, _time.GetUtcNow()))
                    {
                        _byHash[s.Hash] = s;
                        _handleToHash[s.Handle] = s.Hash;
                    }
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // sessions are disposable: everyone signs in again
            _log.Warn($"{File} is unreadable ({e.Message}): starting without sessions");
        }
    }

    /// <summary>Starts writing sessions.json every few seconds when something changed (the component's start: nothing
    /// runs before it, so a start that fails earlier leaves nothing behind).</summary>
    public void Start() => _flush ??= _time.CreateTimer(_ => Flush(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

    public int Count => _byHash.Count;

    /// <summary>A new session for <paramref name="account"/>: its token (for the cookie, never stored) and record. The
    /// account's oldest sessions end beyond maxSessionsPerAccount.</summary>
    public (string Token, SessionRecord Record) Create(Account account, string via, bool mfa, string ip, string userAgent)
    {
        var s = _settings();
        var now = _time.GetUtcNow();
        var token = SecretToken.New();
        var rec = new SessionRecord
        {
            Hash = SecretToken.Hash(token), Handle = "s-" + Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8)),
            AccountId = account.Id, CredentialStamp = account.CredentialStamp, PrivilegeStamp = account.PrivilegeStamp,
            Created = now, LastSeen = now, Expires = now.AddHours(s.SessionAbsoluteHours), Ip = ip, UserAgent = Clean(userAgent, 200), Via = via, Mfa = mfa,
        };
        lock (_lock)
        {
            _byHash[rec.Hash] = rec;
            _handleToHash[rec.Handle] = rec.Hash;
            var mine = _byHash.Values.Where(x => x.AccountId == account.Id).OrderBy(x => x.Created).ToList();
            for (var i = 0; i < mine.Count - s.MaxSessionsPerAccount; i++) RemoveLocked(mine[i]);
        }
        MarkDirty();
        return (token, rec);
    }

    /// <summary>The live session of a cookie token; null for a malformed, unknown, idle or expired one (an expired one
    /// is removed).</summary>
    public SessionRecord? Find(string? token)
    {
        if (!SecretToken.IsWellFormed(token)) return null;
        return _byHash.TryGetValue(SecretToken.Hash(token!), out var rec) ? Live(rec) : null;
    }

    /// <summary>The live session with a public handle (WebSockets re-check theirs by handle: the cookie may have rotated).</summary>
    public SessionRecord? FindByHandle(string? handle) =>
        handle is not null && _handleToHash.TryGetValue(handle, out var h) && _byHash.TryGetValue(h, out var rec) ? Live(rec) : null;

    private SessionRecord? Live(SessionRecord rec)
    {
        if (Alive(rec, _time.GetUtcNow())) return rec;
        Revoke(rec.Handle);
        return null;
    }

    private bool Alive(SessionRecord r, DateTimeOffset now) =>
        now < r.Expires && now - r.LastSeen < TimeSpan.FromMinutes(_settings().SessionIdleMinutes);

    /// <summary>A new token for the same session (sign-in step-ups, a privilege change): the old token stops working.</summary>
    public (string Token, SessionRecord Record) Rotate(SessionRecord rec, Account account, bool? mfa = null)
    {
        var token = SecretToken.New();
        SessionRecord next;
        lock (_lock)
        {
            if (!_byHash.TryRemove(rec.Hash, out var current)) throw new InvalidOperationException("the session ended");
            next = current with
            {
                Hash = SecretToken.Hash(token), PrivilegeStamp = account.PrivilegeStamp, CredentialStamp = account.CredentialStamp,
                ReauthenticatedUntil = null, Mfa = mfa ?? current.Mfa, LastSeen = _time.GetUtcNow(),
            };
            _byHash[next.Hash] = next;
            _handleToHash[next.Handle] = next.Hash;
        }
        MarkDirty();
        return (token, next);
    }

    /// <summary>Notes activity (at most once a minute per session, to keep the file quiet).</summary>
    public SessionRecord Touch(SessionRecord rec)
    {
        var now = _time.GetUtcNow();
        if (now - rec.LastSeen < TimeSpan.FromMinutes(1)) return rec;
        SessionRecord next;
        lock (_lock)
        {
            if (!_byHash.TryGetValue(rec.Hash, out var cur) || cur.Hash != rec.Hash) return rec;
            next = cur with { LastSeen = now };
            _byHash[rec.Hash] = next;
        }
        MarkDirty();
        return next;
    }

    /// <summary>Marks only a still-live, unchanged session; proof completion never revives a revoked session.</summary>
    public bool MarkReauthenticated(SessionRecord expected, Account account, DateTimeOffset until)
    {
        lock (_lock)
        {
            if (!_byHash.TryGetValue(expected.Hash, out var current) || !Alive(current, _time.GetUtcNow()) ||
                current.CredentialStamp != account.CredentialStamp || current.PrivilegeStamp != account.PrivilegeStamp ||
                expected.CredentialStamp != account.CredentialStamp || expected.PrivilegeStamp != account.PrivilegeStamp) return false;
            _byHash[current.Hash] = current with { ReauthenticatedUntil = until };
        }
        MarkDirty();
        return true;
    }

    public bool Revoke(string handle)
    {
        lock (_lock)
        {
            if (!_handleToHash.TryGetValue(handle, out var h) || !_byHash.TryGetValue(h, out var rec)) return false;
            RemoveLocked(rec);
        }
        MarkDirty();
        return true;
    }

    /// <summary>Ends every session of an account (but <paramref name="exceptHandle"/>); the number ended.</summary>
    public int RevokeAccount(string accountId, string? exceptHandle = null)
    {
        int n;
        lock (_lock)
        {
            var list = _byHash.Values.Where(x => x.AccountId == accountId && x.Handle != exceptHandle).ToList();
            foreach (var r in list) RemoveLocked(r);
            n = list.Count;
        }
        if (n > 0) MarkDirty();
        return n;
    }

    public IReadOnlyList<SessionRecord> ForAccount(string accountId)
    {
        var now = _time.GetUtcNow();
        return [.. _byHash.Values.Where(x => x.AccountId == accountId && Alive(x, now)).OrderByDescending(x => x.LastSeen)];
    }

    private void RemoveLocked(SessionRecord rec)
    {
        if (_byHash.TryRemove(new KeyValuePair<string, SessionRecord>(rec.Hash, rec)))
            _handleToHash.TryRemove(new KeyValuePair<string, string>(rec.Handle, rec.Hash));
    }

    private void MarkDirty() => Interlocked.Exchange(ref _dirty, 1);

    /// <summary>Writes sessions.json when something changed (timer, stop). Also drops dead sessions.</summary>
    public void Flush()
    {
        try
        {
            var now = _time.GetUtcNow();
            foreach (var r in _byHash.Values)
                if (!Alive(r, now))
                    lock (_lock) RemoveLocked(r);
            if (Interlocked.Exchange(ref _dirty, 0) == 0) return;
            List<SessionRecord> snapshot;
            lock (_lock) snapshot = [.. _byHash.Values];
            _data.WriteJson(File, new SessionsFile { Sessions = snapshot }, secret: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            MarkDirty();
            _log.Warn($"{File}: cannot save ({e.Message}); retrying");
        }
    }

    internal static string Clean(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var chars = s.Where(c => c >= 0x20 && c < 0x7F).Take(max).ToArray();
        return new string(chars);
    }

    public void Dispose()
    {
        _flush?.Dispose();
        Flush();
    }

    private sealed class SessionsFile
    {
        public int Format { get; set; } = 1;
        public List<SessionRecord>? Sessions { get; set; } = [];
    }
}
