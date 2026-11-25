// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Sockets;
using Kilnwarden.Config;
using Kilnwarden.Server;
using Kilnwarden.Storage;
using Kilnwarden.Tls;

namespace Kilnwarden.Hosting;

/// <summary>Where and how the panel listens, from settings.json and whether the owner exists (DESIGN §2):
/// <list type="bullet">
/// <item>127.0.0.1:&lt;query port + 100&gt; by default. A public bind only once the owner account exists, with a loud
/// warning; until then loopback.</item>
/// <item>TLS: self-signed (default), file, off (loopback, or reverseProxy.enabled), acme (the acme component's certificate; self-signed until
/// then). Engine: kwtls when its DLL loads (ECDSA), else SslStream (RSA, TLS 1.2 floor).</item>
/// <item>A change of bind, port or TLS settings, or the owner appearing, restarts the listener (graceful, ~1 s); other
/// settings (limits, allowlist, hosts) apply to the next request.</item>
/// </list></summary>
internal sealed class ListenerManager
{
    public const int DefaultQueryPort = 15637;
    public const int PortOffset = 100;

    private readonly WebAdminEnvironment _env;
    private readonly DataStore _data;
    private readonly ServerShared _shared;
    private readonly Func<bool> _ownerExists;
    private readonly Func<bool>? _setupOpen;
    private readonly Func<Tls.Acme.IAcmeCertificates?> _acme;
    private readonly IWebAdminLog _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Running? _running;
    private ServerPolicy _policy;
    private bool _stopped;

    public ListenerManager(WebAdminEnvironment env, DataStore data, ServerSettings initial, Func<bool> ownerExists,
        Func<Func<ServerPolicy>, ServerShared> shared, Func<Tls.Acme.IAcmeCertificates?>? acme = null, Func<bool>? setupOpen = null)
    {
        _acme = acme ?? (() => null);
        _setupOpen = setupOpen;
        _env = env;
        _data = data;
        _log = env.Log;
        _ownerExists = ownerExists;
        _policy = ServerPolicy.From(initial, publicBind: false);
        _shared = shared(() => Volatile.Read(ref _policy));
    }

    public ServerShared Shared => _shared;
    public HttpServer? Server => _running?.Server;
    public Plan? Current => _running?.Plan;
    public event Action<Running?>? Changed;

    /// <summary>The listener's settings as they take effect.</summary>
    public sealed record Plan(IPAddress Bind, int Port, TlsMode Tls, bool Public, bool WaitingForOwner, IReadOnlyList<string> Names, string? Problem)
    {
        /// <summary>Public before the owner exists: settings.json remoteSetup.</summary>
        public bool RemoteSetup { get; init; }

        public bool SameListener(Plan o) =>
            Bind.Equals(o.Bind) && Port == o.Port && Tls == o.Tls && Names.SequenceEqual(o.Names);
    }

    public sealed record Running(Plan Plan, HttpServer Server, ITlsServer? Tls, ICertificateSource? Source, IReadOnlyList<Uri> Endpoints);

    public Plan MakePlan(ServerSettings s)
    {
        var bind = IPAddress.Parse(s.Bind);
        var wantPublic = !IPAddress.IsLoopback(bind);
        var owner = _ownerExists();
        // settings.json remoteSetup: public already for the first-run setup (it needs the one-time link)
        var early = !owner && s.RemoteSetup && (_setupOpen?.Invoke() ?? false);
        string? problem = null;
        var acme = s.Tls.Mode == TlsMode.Acme && _acme() is not null;
        var tls = s.Tls.Mode == TlsMode.Acme && !acme ? TlsMode.SelfSigned : s.Tls.Mode;
        if (s.Tls.Mode == TlsMode.Acme && !acme) problem = "tls.mode acme needs the acme component; serving a self-signed certificate";
        var effectivePublic = wantPublic && (owner || early);
        if (effectivePublic && tls == TlsMode.Off && !s.ReverseProxy.Enabled)
        {
            effectivePublic = false;
            problem = "tls.mode off on a public address needs reverseProxy.enabled (plain HTTP must not cross a network); listening on 127.0.0.1 only";
        }
        var effective = effectivePublic ? bind : IPAddress.Loopback;
        var port = s.Port != 0 ? s.Port : (_env.QueryPort() ?? DefaultQueryPort) + PortOffset;
        if (port > 65535) port = DefaultQueryPort + PortOffset;
        var names = new List<string> { "localhost", "127.0.0.1", "::1" };
        foreach (var h in s.Tls.Hostnames.Select(h => h.Trim().ToLowerInvariant()).Where(h => h.Length > 0))
            if (!names.Contains(h)) names.Add(h);
        if (effectivePublic && !bind.Equals(IPAddress.Any) && !bind.Equals(IPAddress.IPv6Any) && !names.Contains(bind.ToString())) names.Add(bind.ToString());
        return new Plan(effective, port, tls, effectivePublic, wantPublic && !owner && !early, names, problem) { RemoteSetup = effectivePublic && !owner };
    }

    /// <summary>Starts (or restarts) the listener for <paramref name="settings"/>. Serialized; a no-op after Stop.</summary>
    public async Task ApplyAsync(ServerSettings settings, bool force, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_stopped) return;
            var plan = MakePlan(settings);
            Volatile.Write(ref _policy, ServerPolicy.From(settings, plan.Public));
            if (!force && _running is { } r && r.Plan.SameListener(plan)) return;
            if (_running is { } old)
            {
                var report = old.Server.Stop(1000);
                old.Tls?.Dispose();
                old.Source?.Dispose();
                _running = null;
                _log.Info($"listener restarting for new settings ({report.Connections} connection(s) closed)");
            }
            if (plan.Problem is not null) _log.Warn(plan.Problem);
            if (plan.WaitingForOwner)
                _log.Info($"bind {settings.Bind} waits until the owner account exists; listening on 127.0.0.1 until then");
            var (tls, source) = plan.Tls == TlsMode.Off ? (null, null) : BuildTls(settings, plan);
            var server = new HttpServer("panel", new IPEndPoint(plan.Bind, plan.Port), tls, _shared);
            await BindAsync(server, plan, ct).ConfigureAwait(false);
            var endpoints = Endpoints(plan, tls is not null, server.LocalEndPoint.Port);
            _running = new Running(plan, server, tls, source, endpoints);
            if (plan.Public) Loud(settings, plan, server.LocalEndPoint.Port);
            if (plan.RemoteSetup)
                _log.Warn($"REMOTE SETUP: no owner account yet and settings.json remoteSetup is on, so {Product.Name} already listens on {plan.Bind}:{server.LocalEndPoint.Port}. " +
                          "The first-run setup needs the one-time link from kiln.log (or the console command kilnwarden setup); create the owner now.");
            _log.Info($"panel at {string.Join(" ", endpoints)} ({(tls is null ? "plain HTTP" : $"{tls.Engine}, {tls.Certificate.Source} {tls.Certificate.KeyType}, SHA-256 {tls.Certificate.Sha256}")})");
            Changed?.Invoke(_running);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task BindAsync(HttpServer server, Plan plan, CancellationToken ct)
    {
        // during a hot reload the old instance may still hold the port for a moment
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                server.Start();
                return;
            }
            catch (SocketException e) when (attempt < 10 && e.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                await Task.Delay(300, ct).ConfigureAwait(false);
            }
            catch (SocketException e)
            {
                throw new InvalidOperationException($"cannot listen on {plan.Bind}:{plan.Port}: {e.SocketErrorCode} (settings.json: port)", e);
            }
        }
    }

    private (ITlsServer, ICertificateSource) BuildTls(ServerSettings s, Plan plan)
    {
        KwtlsLibrary? lib = null;
        if (s.Tls.Engine != TlsEngine.SslStream)
        {
            var path = Path.Combine(_env.ModDirectory, KwtlsLibrary.FileName(Product.KwtlsVersion));
            try
            {
                lib = KwtlsLibrary.Load(path);
            }
            catch (Exception e) when (e is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException or InvalidOperationException or IOException)
            {
                var text = $"TLS: kwtls not available ({e.Message}); falling back to SslStream (RSA, TLS 1.2)";
                if (s.Tls.Engine == TlsEngine.Kwtls) _log.Error(text);
                else _log.Warn(text);
            }
        }
        string[] alpn = ["http/1.1"];
        ICertificateSource? source = null;
        if (plan.Tls == TlsMode.File)
        {
            try
            {
                source = new FileCertificateSource(DataPath(s.Tls.CertFile), DataPath(s.Tls.KeyFile), _log, _env.Time);
                return (lib is not null ? new KwtlsServer(lib, source, alpn, _log) : new SslStreamServer(source, alpn, _log), source);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or InvalidOperationException or NotSupportedException
                                          or System.Security.Cryptography.CryptographicException)
            {
                source?.Dispose();
                _log.Error($"TLS: the certificate files cannot be used ({e.Message}); serving a self-signed certificate so the panel stays reachable");
            }
        }
        source = lib is not null
            ? new SelfSignedCertificateSource(_data, "ecdsa", plan.Names, _log, _env.Time, lib.SelfSigned)
            : new SelfSignedCertificateSource(_data, "rsa", plan.Names, _log, _env.Time, (n, cn, f, t) => Pfx.SelfSignedRsa(cn, n, f, t));
        // tls.mode acme: the ACME certificate once there is one, this self-signed one until then (and while it fails);
        // the listener also answers TLS-ALPN-01 validations with the challenge certificates
        if (plan.Tls == TlsMode.Acme && _acme() is { } acme)
        {
            source = acme.Wrap(source);
            ITlsServer main = lib is not null ? new KwtlsServer(lib, source, alpn, _log) : new SslStreamServer(source, alpn, _log);
            string[] challenge = [AlpnChallenges.Protocol];
            // the challenge certificate is served by SslStream whatever the main engine: RFC 8737 makes its acmeIdentifier
            // extension critical, and kwtls (webpki) refuses to load a certificate with a critical extension it does not
            // know. SslStream serves it (RSA, TLS 1.2 and up: what validators speak; Wine's SChannel too, step 0)
            return (new AlpnChallengeServer(main, acme.Alpn, m => new SslStreamServer(new FixedCertificateSource(m, "acme-challenge"), challenge, _log)), source);
        }
        return (lib is not null ? new KwtlsServer(lib, source, alpn, _log) : new SslStreamServer(source, alpn, _log), source);
    }

    private string DataPath(string p) => Path.IsPathRooted(p) ? p : Path.GetFullPath(Path.Combine(_data.Root, p));

    private static IReadOnlyList<Uri> Endpoints(Plan plan, bool tls, int port)
    {
        var scheme = tls ? "https" : "http";
        var hosts = new List<string>();
        if (!plan.Public) hosts.AddRange(["127.0.0.1", "localhost"]);
        else
        {
            if (!plan.Bind.Equals(IPAddress.Any) && !plan.Bind.Equals(IPAddress.IPv6Any))
                hosts.Add(plan.Bind.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{plan.Bind}]" : plan.Bind.ToString());
            hosts.AddRange(plan.Names.Where(n => n is not ("localhost" or "127.0.0.1" or "::1") && !IPAddress.TryParse(n, out _)));
            hosts.Add("127.0.0.1");
        }
        return [.. hosts.Distinct().Select(h => new Uri($"{scheme}://{h}:{port}/"))];
    }

    private void Loud(ServerSettings s, Plan plan, int port)
    {
        var bar = new string('!', 100);
        _log.Warn(bar);
        _log.Warn($"PUBLIC: {Product.Name} listens on {plan.Bind}:{port}. Anyone who can reach this port sees its sign-in page.");
        _log.Warn("Use strong passwords and two-factor sign-in, and limit who can connect (settings.json ipAllowlist, a firewall).");
        if (plan.Tls == TlsMode.Off) _log.Warn("TLS is off: only safe behind your reverse proxy on a trusted network.");
        if (s.IpAllowlist.Count == 0) _log.Warn("ipAllowlist is empty: every address may connect.");
        _log.Warn(bar);
    }

    /// <summary>Stops the listener for good (OnUnload). Waits for a restart in progress (bounded).</summary>
    public string Stop(int graceMs)
    {
        var got = _gate.Wait(graceMs);
        try
        {
            _stopped = true;
            if (_running is not { } r) return "not listening";
            var report = r.Server.Stop(graceMs);
            r.Tls?.Dispose();
            r.Source?.Dispose();
            _running = null;
            return $"{report.Connections} connection(s) closed ({report.Graceful} graceful, {report.Aborted} aborted)" +
                   (report.AcceptEnded ? "" : ", accept thread still running");
        }
        finally
        {
            if (got) _gate.Release();
        }
    }
}
