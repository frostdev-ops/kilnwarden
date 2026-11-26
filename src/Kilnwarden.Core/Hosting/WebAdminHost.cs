// SPDX-License-Identifier: MIT
using System.Diagnostics;
using System.Net;
using Kilnwarden.Config;
using Kilnwarden.Modules;
using Kilnwarden.Security;
using Kilnwarden.Server;
using Kilnwarden.Static;
using Kilnwarden.Storage;
using Kilnwarden.Tls;

namespace Kilnwarden.Hosting;

/// <summary>The composition root: settings, storage, the module registry, the components, the pipeline and the
/// listener. <see cref="Start"/> returns at once (everything slow, TLS keys included, runs on a pool thread, never on
/// the tick thread); <see cref="Stop"/> is the OnUnload order: stop the listener (WebSockets get 1001, wait at most
/// ~1 s, cancel, abort), then stop the components in reverse order.</summary>
public sealed class WebAdminHost : IServerInfo
{
    private readonly WebAdminEnvironment _env;
    private readonly IReadOnlyList<IWebAdminComponent> _components;
    private readonly CancellationTokenSource _life = new();
    private readonly List<IWebAdminComponent> _started = [];
    private readonly object _restartGate = new();
    private readonly object _endpointGate = new();
    private WebAdminBuilder? _builder;
    private ListenerManager? _listeners;
    private ISetupState? _setup;
    private IPublicAddress? _public;
    private ListenerManager.Running? _running;
    private Task? _start;
    private Task _restart = Task.CompletedTask;
    private int _stopped;

    /// <summary>Cheap: nothing is read, written or JIT-heavy here (OnLoad runs it on the tick thread); the start does
    /// the rest on a pool thread.</summary>
    public WebAdminHost(WebAdminEnvironment env, IEnumerable<IWebAdminComponent> components)
    {
        _env = env;
        _components = [new CoreComponent(), .. components];
    }

    private DataStore? _data;
    private SettingsStore<ServerSettings>? _settings;
    private ModuleRegistry? _modules;

    public DataStore Data => _data ?? throw new InvalidOperationException("not started");
    public SettingsStore<ServerSettings> Settings => _settings ?? throw new InvalidOperationException("not started");
    public ModuleRegistry Modules => _modules ?? throw new InvalidOperationException("not started");
    public WebAdminServices Services { get; } = new();
    public RequestPipeline? Pipeline { get; private set; }

    /// <summary>The counters of the running server (null before the start).</summary>
    public ServerCounters? Counters => _listeners?.Shared.Counters;

    internal HttpServer? Server => _listeners?.Server;
    internal ListenerManager.Plan? Plan => _listeners?.Current;

    /// <summary>Completes when the start finished (faulted when it failed; the error is logged too).</summary>
    public Task Started => _start ?? Task.CompletedTask;

    /// <summary>Completes when the last listener restart (settings change, owner created) finished.</summary>
    internal Task Restarted
    {
        get
        {
            lock (_restartGate) return _restart;
        }
    }

    /// <summary>Starts on a pool thread and returns at once.</summary>
    public void Start() => _start = Task.Run(() => StartAsync(_life.Token));

    private async Task StartAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            _data = new DataStore(_env.DataDirectory);
            _data.Resecure();
            WriteLauncherEndpoint(null);
            _settings = new SettingsStore<ServerSettings>(_data, "settings.json", _env.Log, SettingsRules.Validate);
            _modules = new ModuleRegistry(_data);
            Services.Add(_env);
            Services.Add(_modules);
            Services.Add(_settings);
            Services.Add<IServerInfo>(this);
            Services.Add(new Relay.RelayTarget(() => _listeners?.Server));
            if (_env.GameThread is not null) Services.Add(_env.GameThread);
            var b = _builder = new WebAdminBuilder(_env, Data, Modules, Settings, Services, this);
            foreach (var c in _components)
            {
                ct.ThrowIfCancellationRequested();
                c.Configure(b);
            }
            Services.Add(b.AccessGate);
            Services.Add(b.SetupState);
            Services.Add(b.Audit);
            _setup = b.SetupState;
            var web = StaticFiles.Load(Path.Combine(_env.ModDirectory, "web"), _env.Log);
            Pipeline = new RequestPipeline(Modules, b.Middleware(), b.AccessGate, web.HandleAsync, _env.Log);
            foreach (var c in _components)
            {
                ct.ThrowIfCancellationRequested();
                await c.StartAsync(Services, ct).ConfigureAwait(false);
                lock (_started) _started.Add(c);
            }
            // the panel's public address, when a component knows it (the setup's): the launcher's file follows it (while
            // a listener runs; without one there is no file to write, and the listener's own change writes it)
            if (Services.TryGet<IPublicAddress>() is { } pub)
            {
                _public = pub;
                pub.Changed += () =>
                {
                    if (Volatile.Read(ref _running) is { } running) WriteLauncherEndpoint(running);
                };
            }
            _listeners = new ListenerManager(_env, Data, Settings.Current, () => _setup.OwnerExists,
                policy => new ServerShared(Pipeline, Services, _env.Log, new ClientLimiter(_env.Time), policy),
                () => Services.TryGet<Tls.Acme.IAcmeCertificates>(), () => _setup.SetupOpen);
            Services.Add(_listeners.Shared.Counters);
            _listeners.Changed += r =>
            {
                Volatile.Write(ref _running, r);
                Endpoints = r?.Endpoints ?? [];
                Certificate = r?.Tls?.Certificate;
                TlsEngine = r?.Tls?.Engine ?? "off";
                WriteLauncherEndpoint(r);
                if (r?.Source is { } source)
                    source.Changed += _ =>
                    {
                        if (ReferenceEquals(_listeners.Server, r.Server)) WriteLauncherEndpoint(r);
                    };
                RaiseServerChanged();
            };
            // before the first listener: a change of the setup state during its start (Kiln.Admin found too old while the
            // listener binds) re-applies it after, instead of being missed (the public bind of remoteSetup stayed up)
            _setup.Changed += () => Restart(Settings.Current);
            await _listeners.ApplyAsync(Settings.Current, force: true, ct).ConfigureAwait(false);
            Settings.Changed += (_, next) => Restart(next);
            Settings.Watch(_env.Time);
            _env.Log.Info($"{Product.Name} {Product.Version} started in {sw.Elapsed.TotalMilliseconds:F0} ms " +
                          $"({_components.Count} components, {Modules.Modules.Count} modules, {Modules.Routes.Count} routes" +
                          (web.IsPlaceholder ? ", no web app built: placeholder page" : $", web app {web.Count} files") + ")");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // stopped while starting
        }
        catch (Exception e)
        {
            _env.Log.Error($"{Product.Name} failed to start; the panel is off until the mod reloads", e);
            throw;
        }
    }

    /// <summary>Re-applies the listener settings off the caller's thread (a settings poll, the auth component).</summary>
    private void Restart(ServerSettings settings)
    {
        if (Volatile.Read(ref _stopped) == 1 || _listeners is not { } l) return;
        CancellationToken token;
        try
        {
            token = _life.Token;
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        lock (_restartGate)
        {
            _restart = _restart.ContinueWith(async _ =>
            {
                try
                {
                    await l.ApplyAsync(settings, force: false, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    _env.Log.Error("listener restart failed; the panel is off until settings.json is fixed or the mod reloads", e);
                }
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
        }
    }

    /// <summary>Stops everything (OnUnload). Synchronous and bounded (about 1.5 s at worst). Returns a summary line.</summary>
    public string Stop(int graceMs = 1000)
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 1) return "already stopped";
        var sw = Stopwatch.StartNew();
        _life.Cancel();   // a start still running stops at its next step
        try
        {
            _start?.Wait(graceMs);
        }
        catch (AggregateException)
        {
            // logged by StartAsync
        }
        _settings?.Dispose();
        var listener = _listeners?.Stop(graceMs) ?? "never listened";
        WriteLauncherEndpoint(null);
        List<IWebAdminComponent> started;
        lock (_started) started = [.. _started];
        started.Reverse();
        using var stopCts = new CancellationTokenSource(graceMs);
        var stops = started.Select(c => SafeStop(c, stopCts.Token)).ToArray();
        var allStopped = Task.WaitAll(stops, graceMs);
        foreach (var s in _builder?.OwnedSettings ?? []) s.Dispose();
        _life.Dispose();
        return $"stopped in {sw.Elapsed.TotalMilliseconds:F0} ms: {listener}" + (allStopped ? "" : "; a component did not stop in time");
    }

    private async Task SafeStop(IWebAdminComponent c, CancellationToken ct)
    {
        try
        {
            await c.StopAsync(ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _env.Log.Error($"component {c.Name}: stop failed", e);
        }
    }

    // The launcher reads this file through the server folder (via SSH for its remote instances). It contains only
    // addresses and certificate fingerprints. Owner tickets are minted separately over authenticated KAP.
    // format 1: host, port, sha256 (this machine's address and the certificate it gets); public and publicSha256 (the
    // panel's address on the internet and the certificate it serves there) only while that address really works
    // (IPublicAddress), and only when that certificate is the one this listener serves. Older launchers ignore them.
    private void WriteLauncherEndpoint(ListenerManager.Running? running)
    {
        lock (_endpointGate)
        {
            const string file = "launcher-endpoint.json";
            if (_data is not { } data) return;
            try
            {
                if (running is null)
                {
                    Retrying(() => data.Delete(file));
                    return;
                }
                if (Volatile.Read(ref _stopped) != 0 || !ReferenceEquals(_listeners?.Server, running.Server)) return;
                var bind = running.Plan.Bind;
                var address = bind.Equals(IPAddress.Any) ? IPAddress.Loopback
                    : bind.Equals(IPAddress.IPv6Any) ? IPAddress.IPv6Loopback : bind;
                var o = new System.Text.Json.Nodes.JsonObject
                {
                    ["format"] = 1, ["host"] = address.ToString(), ["port"] = running.Server.LocalEndPoint.Port,
                    ["sha256"] = running.Tls?.LoopbackCertificate.Sha256,
                };
                if (_public?.Public is { } pub && running.Tls?.Certificate.Sha256 == pub.Sha256)
                {
                    o["public"] = pub.Url;
                    o["publicSha256"] = pub.Sha256;
                }
                Retrying(() => data.WriteJson(file, o));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _env.Log.Warn($"Could not update {file}: {e.Message}");
            }
        }
    }

    // On Windows a launcher reading the file at that moment makes the replace (or the delete) fail: a few more tries, so
    // the file does not stay behind the panel until its next change.
    private static void Retrying(Action write)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                write();
                return;
            }
            catch (Exception e) when (attempt < 5 && e is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(40 * attempt);
            }
        }
    }

    // IServerInfo
    public IReadOnlyList<Uri> Endpoints { get; private set; } = [];
    public CertificateInfo? Certificate { get; private set; }
    public string TlsEngine { get; private set; } = "off";
    public event Action? Changed;

    private void RaiseServerChanged()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception e)
        {
            _env.Log.Error("a server-info listener failed", e);
        }
    }
}
