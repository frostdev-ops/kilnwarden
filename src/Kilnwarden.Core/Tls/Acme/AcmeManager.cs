// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Kilnwarden.Config;
using Kilnwarden.Hosting;
using Kilnwarden.Relay;
using Kilnwarden.Storage;

namespace Kilnwarden.Tls.Acme;

/// <summary>The listener's hook for tls.mode acme: wraps the self-signed fallback so the ACME certificate is served once
/// there is one (ListenerManager asks the services for it).</summary>
public interface IAcmeCertificates
{
    ICertificateSource Wrap(ICertificateSource fallback);

    /// <summary>TLS-ALPN-01 challenges in progress: the listener answers them (AlpnChallengeServer).</summary>
    AlpnChallenges Alpn { get; }
}

/// <summary>Test hooks (internal).</summary>
internal sealed class AcmeOptions
{
    public TimeProvider? Time { get; init; }
    public HttpMessageInvoker? Http { get; init; }
    public AcmePolling? Polling { get; init; }

    /// <summary>The first check after start (default 10 s) and the period (default 1 h).</summary>
    public TimeSpan? FirstCheck { get; init; }

    public TimeSpan? Period { get; init; }

    /// <summary>The http-01 answerer's address when acme.json leaves it empty (default: all addresses).</summary>
    public IPAddress? DefaultBind { get; init; }
}

/// <summary>Local ACME certificates (DESIGN §2): orders and renews in process with http-01, keeps the last good
/// certificate when a renewal fails, rate-limits itself (backoff after failures, a weekly order budget, both kept across
/// restarts in acme-state.json), and only ever answers challenges for the configured names. Runs only while tls.mode is
/// acme, the owner exists and the CA's terms are accepted. Files: tls/acme/account.json (key, account URL) and
/// tls/acme/cert.json (chain and key in one file, so a swap is one atomic write), both owner-only.</summary>
public sealed class AcmeManager : IAcmeCertificates, IDisposable
{
    public const string AccountFile = "tls/acme/account.json", CertFile = "tls/acme/cert.json", StateFile = "acme-state.json";

    private readonly DataStore _data;
    private readonly SettingsStore<AcmeSettings> _settings;
    private readonly Func<ServerSettings> _server;
    private readonly Func<bool> _ownerExists;
    private readonly IWebAdminLog _log;
    private readonly TimeProvider _time;
    private readonly AcmeOptions _o;
    private readonly HttpMessageInvoker _http;
    private readonly bool _ownsHttp;
    private readonly Func<RelaySettings?> _relay;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _single = new(1, 1);
    private readonly object _stateLock = new();
    private ITimer? _timer;
    private CertificateMaterial? _good;
    private State _state;
    private volatile bool _disposed;
    private volatile string _phase = "idle";

    internal sealed class State
    {
        public int Failures { get; set; }
        public DateTimeOffset? LastAttempt { get; set; }
        public DateTimeOffset? LastSuccess { get; set; }
        public DateTimeOffset? NextAttempt { get; set; }
        public string? LastError { get; set; }
        public List<DateTimeOffset> Orders { get; set; } = [];
    }

    internal sealed class Bundle
    {
        public string ChainPem { get; set; } = "";
        public string KeyPem { get; set; } = "";
    }

    internal sealed class Account
    {
        public string Directory { get; set; } = "";
        public string KeyPem { get; set; } = "";
        public string? Kid { get; set; }
    }

    internal AcmeManager(DataStore data, SettingsStore<AcmeSettings> settings, Func<ServerSettings> server, Func<bool> ownerExists, IWebAdminLog log,
        AcmeOptions? options = null, Func<RelaySettings?>? relay = null)
    {
        _data = data;
        _settings = settings;
        _server = server;
        _ownerExists = ownerExists;
        _log = log;
        _o = options ?? new AcmeOptions();
        _relay = relay ?? (() => null);
        _time = _o.Time ?? TimeProvider.System;
        if (_o.Http is { } h) _http = h;
        else
        {
            _ownsHttp = true;
            _http = new HttpMessageInvoker(new SocketsHttpHandler
            {
                AllowAutoRedirect = false, UseCookies = false, UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(15),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5), MaxResponseHeadersLength = 32,
            });
        }
        _state = ReadState();
        _good = LoadStored();
    }

    /// <summary>A new certificate passed the checks (any thread).</summary>
    public event Action<CertificateMaterial>? Issued;

    /// <summary>The ACME certificate being served, or null (the fallback serves).</summary>
    public CertificateMaterial? Good => Volatile.Read(ref _good);

    public bool Active => _server().Tls.Mode == TlsMode.Acme;

    /// <summary>The names to certify: acme.json names, else the DNS names in settings.json tls.hostnames.</summary>
    public IReadOnlyList<string> Names
    {
        get
        {
            var s = _settings.Current;
            var names = s.Names.Count > 0 ? s.Names : _server().Tls.Hostnames.Where(AcmeSettings.IsDnsName).ToList();
            return [.. names.Select(n => n.Trim().ToLowerInvariant()).Distinct()];
        }
    }

    public ICertificateSource Wrap(ICertificateSource fallback) => new AcmeCertificateSource(this, fallback);

    private State ReadState()
    {
        try
        {
            return _data.ReadJson<State>(StateFile) ?? new State();
        }
        catch (System.Text.Json.JsonException)
        {
            return new State();
        }
    }

    private void SaveState()
    {
        lock (_stateLock) _data.WriteJson(StateFile, _state);
    }

    private CertificateMaterial? LoadStored()
    {
        try
        {
            if (_data.ReadJson<Bundle>(CertFile) is not { } b) return null;
            var chain = Encoding.ASCII.GetBytes(b.ChainPem);
            var key = Encoding.ASCII.GetBytes(b.KeyPem);
            // the stored one is served while valid even if names changed since; a renewal follows
            var info = CertificateCheck.Check(chain, key, [], _time.GetUtcNow());
            return new CertificateMaterial(chain, key, info);
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or CryptographicException or FormatException or IOException)
        {
            _log.Warn($"ACME: the stored certificate cannot be used ({e.Message}); a new one is ordered when possible");
            return null;
        }
    }

    public void Start()
    {
        var first = _o.FirstCheck ?? TimeSpan.FromSeconds(10);
        var period = _o.Period ?? TimeSpan.FromHours(1);
        _timer = _time.CreateTimer(_ => Kick(), null, first, period);
        _settings.Changed += OnSettings;
    }

    private void OnSettings(AcmeSettings a, AcmeSettings b) => Kick();

    /// <summary>Runs a check in the background (no-op while one runs).</summary>
    public void Kick() => _ = CheckAsync(force: false, CancellationToken.None);

    /// <summary>The owner's "renew now": orders even though the certificate is still good, within the rate limits.
    /// Null when started, else why not.</summary>
    public string? RenewNow()
    {
        if (Blocked(force: true) is { } why) return why;
        _ = CheckAsync(force: true, CancellationToken.None);
        return null;
    }

    /// <summary>Why no order would run now (null: one would).</summary>
    private string? Blocked(bool force)
    {
        if (!Active) return "tls.mode is not acme";
        if (!_ownerExists()) return "no owner account yet (settings.json remoteSetup orders before it)";
        var s = _settings.Current;
        if (!s.AcceptTerms) return "the CA's terms are not accepted (acme.json acceptTerms)";
        var names = Names;
        if (names.Count == 0) return "no names to certify (acme.json names or settings.json tls.hostnames)";
        if (s.Challenge == "dns-01" && (RelayFor(names) is null || !_data.Exists("relay-key.pem")))
            return "dns-01 needs the enabled relay and its registered hostname as the only ACME name";
        var now = _time.GetUtcNow();
        if (!force && Good is { } g && names.All(n => g.Info.Names.Contains(n, StringComparer.OrdinalIgnoreCase)) &&
            g.Info.NotAfter - now > TimeSpan.FromDays(s.RenewDaysBefore)) return "current";
        lock (_stateLock)
        {
            if (_state.NextAttempt is { } next && now < next) return $"waiting after a failure until {next:O}";
            _state.Orders.RemoveAll(o => now - o > TimeSpan.FromDays(7));
            if (_state.Orders.Count >= s.MaxOrdersPerWeek) return $"the weekly order budget ({s.MaxOrdersPerWeek}) is used up";
        }
        return null;
    }

    /// <summary>One check: orders when needed and allowed. Single-flight. True when a new certificate was installed.</summary>
    internal async Task<bool> CheckAsync(bool force, CancellationToken ct)
    {
        using var orderLife = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
        ct = orderLife.Token;
        if (_disposed || !await _single.WaitAsync(0, ct).ConfigureAwait(false)) return false;
        try
        {
            if (Blocked(force) is { } why)
            {
                _phase = why == "current" ? "idle" : "blocked: " + why;
                return false;
            }
            var s = _settings.Current;
            var names = Names;
            var now = _time.GetUtcNow();
            lock (_stateLock)
            {
                _state.Orders.Add(now);
                _state.LastAttempt = now;
            }
            SaveState();
            _phase = "ordering";
            try
            {
                var m = await OrderAsync(s, names, ct).ConfigureAwait(false);
                Volatile.Write(ref _good, m);
                lock (_stateLock)
                {
                    _state.Failures = 0;
                    _state.NextAttempt = null;
                    _state.LastError = null;
                    _state.LastSuccess = _time.GetUtcNow();
                }
                SaveState();
                _phase = "idle";
                _log.Info($"ACME: new certificate for {string.Join(", ", names)} until {m.Info.NotAfter:yyyy-MM-dd} (SHA-256 {m.Info.Sha256})");
                Issued?.Invoke(m);
                return true;
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception e) when (e is AcmeException or CryptographicException or IOException or SocketException or FormatException
                                          or OperationCanceledException or System.Text.Json.JsonException or InvalidOperationException or ArgumentException
                                          or HttpRequestException)
            {
                DateTimeOffset next;
                lock (_stateLock)
                {
                    _state.Failures++;
                    var wait = TimeSpan.FromMinutes(s.MinRetryMinutes * Math.Pow(2, Math.Min(_state.Failures - 1, 16)));
                    if (wait > TimeSpan.FromHours(s.MaxRetryHours)) wait = TimeSpan.FromHours(s.MaxRetryHours);
                    next = _time.GetUtcNow() + wait;
                    _state.NextAttempt = next;
                    _state.LastError = e.Message.Length > 400 ? e.Message[..400] : e.Message;
                }
                SaveState();
                _phase = "failed";
                _log.Warn($"ACME: ordering a certificate for {string.Join(", ", names)} failed ({e.Message}); " +
                          (Good is null ? "serving the self-signed certificate" : "keeping the current certificate") + $"; next try after {next:yyyy-MM-dd HH:mm} UTC");
                return false;
            }
        }
        finally
        {
            _single.Release();
        }
    }

    private async Task<CertificateMaterial> OrderAsync(AcmeSettings s, IReadOnlyList<string> names, CancellationToken ct)
    {
        var dir = new Uri(s.Directory);
        using var accountKey = RSA.Create();
        var acct = _data.ReadJson<Account>(AccountFile);
        if (acct is { KeyPem.Length: > 0 } && acct.Directory == s.Directory) accountKey.ImportFromPem(acct.KeyPem);
        else
        {
            accountKey.KeySize = 2048;
            acct = new Account { Directory = s.Directory, KeyPem = accountKey.ExportPkcs8PrivateKeyPem() };
        }
        var client = new AcmeClient(_http, dir, accountKey, acct.Kid, _o.Polling);
        var kid = await client.EnsureAccountAsync(s.Email, ct).ConfigureAwait(false);
        if (acct.Kid != kid)
        {
            acct.Kid = kid;
            _data.WriteJson(AccountFile, acct, secret: true);
        }
        using var certKey = RSA.Create(2048);
        var csr = AcmeClient.Csr(certKey, names);
        var chain = s.Challenge switch
        {
            "dns-01" => await OrderViaRelayAsync(client, s, names, csr, ct).ConfigureAwait(false),
            "tls-alpn-01" => await OrderViaAlpnAsync(client, names, csr, ct).ConfigureAwait(false),
            _ => await OrderViaHttpAsync(client, s, names, csr, ct).ConfigureAwait(false),
        };
        ct.ThrowIfCancellationRequested();
        var chainBytes = Encoding.ASCII.GetBytes(chain);
        var keyPem = certKey.ExportPkcs8PrivateKeyPem();
        var keyBytes = Encoding.ASCII.GetBytes(keyPem);
        var info = CertificateCheck.Check(chainBytes, keyBytes, names, _time.GetUtcNow());
        // one file for the pair: a crash leaves the old pair or the new one, never a mix
        ct.ThrowIfCancellationRequested();
        _data.WriteJson(CertFile, new Bundle { ChainPem = chain, KeyPem = keyPem }, secret: true);
        return new CertificateMaterial(chainBytes, keyBytes, info);
    }

    /// <summary>The TLS-ALPN-01 challenges in progress (the listener serves them while tls.mode is acme).</summary>
    public AlpnChallenges Alpn { get; } = new();

    // TLS-ALPN-01 (RFC 8737): the CA connects to port 443 of each name with ALPN "acme-tls/1"; the panel's listener
    // answers with the challenge certificate. Port 443 must reach the panel (it listens there, or 443 is forwarded).
    private async Task<string> OrderViaAlpnAsync(AcmeClient client, IReadOnlyList<string> names, byte[] csr, CancellationToken ct)
    {
        var port = _server().Port;
        if (port != 443) _log.Info($"ACME tls-alpn-01: the CA connects to port 443 of {string.Join(", ", names)}; this panel listens on {(port == 0 ? "the query port + 100" : port)}, so 443 must be forwarded to it");
        try
        {
            return await client.OrderAsync(names, csr, "tls-alpn-01", (name, _, keyAuthorization, _) =>
            {
                Alpn.Publish(name, keyAuthorization);
                return Task.CompletedTask;
            }, ct).ConfigureAwait(false);
        }
        finally
        {
            Alpn.Clear();
        }
    }

    private async Task<string> OrderViaHttpAsync(AcmeClient client, AcmeSettings s, IReadOnlyList<string> names,
        byte[] csr, CancellationToken ct)
    {
        var bind = s.Http01Bind is { Length: > 0 } b ? IPAddress.Parse(b) : _o.DefaultBind ?? IPAddress.Any;
        using var responder = new Http01Responder(new IPEndPoint(bind, s.Http01Port), _log);
        Responder = responder;
        try
        {
            return await client.OrderAsync(names, csr, responder, ct).ConfigureAwait(false);
        }
        finally
        {
            Responder = null;
        }
    }

    private RelaySettings? RelayFor(IReadOnlyList<string> names) =>
        _relay() is { Enabled: true } relay && names.Count == 1 && names[0] == relay.Host ? relay : null;

    private async Task<string> OrderViaRelayAsync(AcmeClient client, AcmeSettings settings, IReadOnlyList<string> names,
        byte[] csr, CancellationToken ct)
    {
        var relay = RelayFor(names) ?? throw new AcmeException("the ACME name is not the registered relay name");
        using var key = new RelayKey(_data);
        var uri = new Uri(new Uri(relay.Origin), "/kiln/webadmin/relay/dns");
        string? challenge = null;
        async Task ChangeRecordAsync(bool add, string value, CancellationToken token)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(20));
            using var request = new HttpRequestMessage(add ? HttpMethod.Put : HttpMethod.Delete, uri);
            key.Authorize((k, v) => request.Headers.Add(k, v), add ? "dns-add" : "dns-remove", relay.Host, value, _time);
            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, deadline.Token).ConfigureAwait(false);
            }
            catch (HttpRequestException e)
            {
                // an ordinary order failure (backoff, counted, logged), never an exception out of the check (review S2)
                throw new AcmeException($"the relay at {relay.Origin} cannot be reached for the DNS challenge: {e.Message}");
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                throw new AcmeException($"the relay at {relay.Origin} did not answer the DNS challenge in time");
            }
            using var _ = response;
            if (!response.IsSuccessStatusCode) throw new AcmeException($"relay DNS update returned {(int)response.StatusCode}");
        }
        try
        {
            return await client.OrderAsync(names, csr, "dns-01", async (_, _, authorization, token) =>
            {
                if (challenge is not null) throw new AcmeException("multiple relay DNS challenges were supplied for one name");
                challenge = AcmeJws.B64(SHA256.HashData(Encoding.ASCII.GetBytes(authorization)));
                await ChangeRecordAsync(true, challenge, token).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(settings.Dns01PropagationSeconds), _time, token).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);
        }
        finally
        {
            if (!ct.IsCancellationRequested && challenge is not null)
            {
                try { await ChangeRecordAsync(false, challenge, ct).ConfigureAwait(false); }
                catch (Exception e) when (e is AcmeException or HttpRequestException or OperationCanceledException)
                {
                    _log.Warn("ACME: the relay could not remove the last DNS challenge");
                }
            }
        }
    }

    /// <summary>The http-01 answerer of the order in progress (tests read its port).</summary>
    internal Http01Responder? Responder { get; private set; }

    public JsonObject Status()
    {
        var s = _settings.Current;
        State st;
        lock (_stateLock) st = _state;
        var g = Good;
        return new JsonObject
        {
            ["active"] = Active,
            ["phase"] = _phase,
            ["blocked"] = Blocked(false) is { } b && b != "current" ? b : null,
            ["directory"] = s.Directory,
            ["challenge"] = s.Challenge,
            ["acceptTerms"] = s.AcceptTerms,
            ["names"] = new JsonArray([.. Names.Select(n => (JsonNode)n)]),
            ["certificate"] = g is null ? null : new JsonObject
            {
                ["sha256"] = g.Info.Sha256, ["names"] = new JsonArray([.. g.Info.Names.Select(n => (JsonNode)n)]),
                ["notAfter"] = g.Info.NotAfter.ToString("O"), ["keyType"] = g.Info.KeyType, ["chainLength"] = g.Info.ChainLength,
            },
            ["lastAttempt"] = st.LastAttempt?.ToString("O"),
            ["lastSuccess"] = st.LastSuccess?.ToString("O"),
            ["nextAttempt"] = st.NextAttempt?.ToString("O"),
            ["lastError"] = st.LastError,
            ["failures"] = st.Failures,
            ["ordersThisWeek"] = st.Orders.Count(o => _time.GetUtcNow() - o <= TimeSpan.FromDays(7)),
            ["maxOrdersPerWeek"] = s.MaxOrdersPerWeek,
        };
    }

    public void Dispose()
    {
        _disposed = true;
        _lifetime.Cancel();
        _settings.Changed -= OnSettings;
        _timer?.Dispose();
        if (_ownsHttp) _http.Dispose();
    }
}

/// <summary>What the listener serves in acme mode: the ACME certificate once there is one, the self-signed fallback
/// until then. The ACME material belongs to the manager (shared across listener restarts), so only the fallback is
/// disposed here.</summary>
public sealed class AcmeCertificateSource : ICertificateSource
{
    private readonly AcmeManager _m;
    private readonly ICertificateSource _fallback;

    internal AcmeCertificateSource(AcmeManager m, ICertificateSource fallback)
    {
        _m = m;
        _fallback = fallback;
        _m.Issued += OnIssued;
        _fallback.Changed += OnFallback;
    }

    public string Mode => "acme";
    public CertificateMaterial Current => _m.Good ?? _fallback.Current;
    public event Action<CertificateMaterial>? Changed;

    private void OnIssued(CertificateMaterial m) => Changed?.Invoke(m);

    private void OnFallback(CertificateMaterial m)
    {
        if (_m.Good is null) Changed?.Invoke(m);
    }

    public void Dispose()
    {
        _m.Issued -= OnIssued;
        _fallback.Changed -= OnFallback;
        _fallback.Dispose();
    }
}
