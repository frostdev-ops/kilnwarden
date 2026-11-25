// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using Kilnwarden.Config;
using Kilnwarden.Hosting;
using Kilnwarden.Identity;
using Kilnwarden.Server;
using Kilnwarden.Tls.Acme;

namespace Kilnwarden.Relay;

internal sealed record RelayTarget(Func<HttpServer?> Listener);

internal sealed class RelayComponent : IWebAdminComponent
{
    private const int MaxStreams = 24;
    private const string Api = "/kiln/webadmin/relay/";
    /// <summary>The relay operator's claim pages for panel names, under the relay's origin (frostdev.io).</summary>
    public const string NamesPath = "/kiln/webadmin/names";
    private static readonly TimeSpan ClaimLinkLife = TimeSpan.FromMinutes(15);
    private readonly Lock _keyLock = new();
    private RelayKey? _key;
    private SettingsStore<RelaySettings> _settings = null!;
    private WebAdminBuilder _builder = null!;
    private CancellationTokenSource? _life;
    private Task? _run;
    private string _state = "waiting";
    private string? _lastError;
    private string? _since;

    private void SetState(string state, string? error = null)
    {
        Volatile.Write(ref _state, state);
        if (error is not null) Volatile.Write(ref _lastError, error);
        Volatile.Write(ref _since, _builder.Environment.Time.GetUtcNow().ToString("O"));
    }

    public string Name => "relay";

    public void Configure(WebAdminBuilder builder)
    {
        _builder = builder;
        _settings = builder.SettingsFile<RelaySettings>("relay.json", RelaySettings.Validate);
        EditableSettings.Of(builder.Services).Add(EditableFile.For("relay", "Hosted access (relay)", _settings));
        builder.Services.Add(_settings);
        builder.Services.Add(new RelayStatus(() =>
        {
            var s = _settings.Current;
            return new RelayStatus.Info(s.Enabled, s.Host, !s.Enabled ? "off" : Volatile.Read(ref _state), Volatile.Read(ref _lastError));
        }));
        var claims = new RelayClaims(MakeClaimLink);
        builder.Services.Add(claims);
        // POST /api/core/relay/claim: a claim link for relay.json's host (owner, recent proof: core.settings.edit is sensitive)
        new Modules.ModuleRegistration(builder.Modules, CoreComponent.ModuleId).MapPost("/relay/claim", Routing.RouteAccess.Requires(CoreComponent.SettingsEdit), ctx =>
        {
            var link = claims.Make("settings");
            if (link.Error is { } error) throw new Http.HttpError(409, error);
            ctx.Services.TryGet<Security.IAuditSink>()?.Record(new Security.AuditEntry(DateTimeOffset.UtcNow, ctx.User?.Id ?? "", ctx.User?.Name ?? "",
                "core.relay.claim_link", new System.Text.Json.Nodes.JsonObject { ["host"] = link.Host, ["fingerprint"] = link.Fingerprint }, true, null, 0, ctx.ClientAddress));
            return ValueTask.FromResult(Http.HttpResponse.Json(new System.Text.Json.Nodes.JsonObject
            {
                ["url"] = link.Url, ["host"] = link.Host, ["fingerprint"] = link.Fingerprint, ["expires"] = link.Expires?.ToString("O"),
                ["warnings"] = new System.Text.Json.Nodes.JsonArray([.. link.Warnings.Select(w => (System.Text.Json.Nodes.JsonNode)w)]),
            }));
        });
        // GET /api/core/relay: what the relay client is doing, and the public key to register (Panel settings)
        new Modules.ModuleRegistration(builder.Modules, CoreComponent.ModuleId).MapGet("/relay", Routing.RouteAccess.Requires(CoreComponent.SettingsView), _ =>
        {
            var s = _settings.Current;
            var publicKey = builder.Data.ReadBytes("relay-public.pem") is { } pem ? System.Text.Encoding.ASCII.GetString(pem) : null;
            var waiting = new System.Text.Json.Nodes.JsonArray([.. WaitingFor(s).Select(w => (System.Text.Json.Nodes.JsonNode)w)]);
            return ValueTask.FromResult(Http.HttpResponse.Json(new System.Text.Json.Nodes.JsonObject
            {
                ["enabled"] = s.Enabled, ["origin"] = s.Origin, ["host"] = s.Host, ["state"] = !s.Enabled ? "off" : Volatile.Read(ref _state),
                ["lastError"] = Volatile.Read(ref _lastError), ["since"] = Volatile.Read(ref _since), ["waitingFor"] = waiting,
                ["publicKey"] = publicKey, ["fingerprint"] = publicKey is null ? null : PemFingerprint(publicKey),
                ["namesPage"] = IdentitySettings.IsBrokerOrigin(s.Origin) ? s.Origin.TrimEnd('/') + NamesPath : null,
            }));
        });
    }

    /// <summary>What the relay link waits for (besides the relay itself).</summary>
    private List<string> WaitingFor(RelaySettings s)
    {
        var server = _builder.Settings.Current;
        var waiting = new List<string>();
        if (!Hosting.ISetupState.RemoteReachAllowed(_builder.SetupState, server)) waiting.Add("an owner account (or settings.json remoteSetup for the first-run setup)");
        if (server.Tls.Mode == TlsMode.Off) waiting.Add("TLS (settings.json tls.mode is off)");
        if (s.Host.Length > 0 && !server.AllowedHosts.Contains(s.Host, StringComparer.OrdinalIgnoreCase) && !server.Tls.Hostnames.Contains(s.Host, StringComparer.OrdinalIgnoreCase))
            waiting.Add($"{s.Host} in settings.json tls.hostnames");
        return waiting;
    }

    /// <summary>SHA-256 hex of a PEM public key's DER (relay-public.pem): the fingerprint the relay and the claim pages show.</summary>
    internal static string? PemFingerprint(string pem)
    {
        var b64 = string.Concat(pem.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("-----", StringComparison.Ordinal)));
        try { return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Convert.FromBase64String(b64))); }
        catch (FormatException) { return null; }
    }

    /// <summary>The one relay key of this panel, made on first use (hosted access switched on, or a claim link).</summary>
    private RelayKey SharedKey()
    {
        lock (_keyLock) return _key ??= new RelayKey(_builder.Data);
    }

    private RelayClaims.Link MakeClaimLink(string source)
    {
        var s = _settings.Current;
        if (!AcmeSettings.IsDnsName(s.Host) || s.Host != s.Host.ToLowerInvariant())
            return new RelayClaims.Link(null, s.Host, null, null, [], "set the name first: Panel settings > Hosted access > Registered name (such as myserver.kiln.nexus), then save");
        if (!IdentitySettings.IsBrokerOrigin(s.Origin))
            return new RelayClaims.Link(null, s.Host, null, null, [], "relay.json origin must be the relay's https origin (https://frostdev.io)");
        RelayKey key;
        try { key = SharedKey(); }
        catch (Exception e) when (e is ArgumentException or System.Security.Cryptography.CryptographicException or IOException)
        {
            return new RelayClaims.Link(null, s.Host, null, null, [], "relay-key.pem cannot be read: delete relay-key.pem and relay-public.pem, restart, then make the link again");
        }
        var now = _builder.Environment.Time.GetUtcNow();
        var url = $"{s.Origin.TrimEnd('/')}{NamesPath}/claim?{key.ClaimQuery(s.Host, _builder.Environment.Time)}";
        var warnings = new List<string>();
        if (!s.Enabled) warnings.Add("hosted access is off: switch it on and save, or the claimed name lapses an hour after the claim");
        warnings.AddRange(WaitingFor(s).Select(w => $"the relay link also waits for {w}"));
        return new RelayClaims.Link(url, s.Host, key.Fingerprint, now + ClaimLinkLife, warnings, null);
    }

    public Task StartAsync(WebAdminServices services, CancellationToken ct)
    {
        _life = new CancellationTokenSource();
        var token = _life.Token;
        _run = Task.Run(() => RunAsync(services.Get<RelayTarget>(), token), token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_life is null) return;
        await _life.CancelAsync().ConfigureAwait(false);
        try { if (_run is not null) await _run.WaitAsync(ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        finally
        {
            _life.Dispose();
            lock (_keyLock)
            {
                _key?.Dispose();
                _key = null;
            }
        }
    }

    private async Task RunAsync(RelayTarget target, CancellationToken ct)
    {
        using var http = new HttpMessageInvoker(new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseCookies = false, UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(8),
        });
        RelayKey? key = null;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var settings = _settings.Current;
                if (settings.Enabled)
                {
                    try
                    {
                        key ??= SharedKey();
                        if (target.Listener() is { IsTls: true } listener && CanRelay(settings))
                            await RunSessionAsync(http, key, settings, listener, target, ct).ConfigureAwait(false);
                    }
                    catch (Exception e) when (key is null && e is ArgumentException or System.Security.Cryptography.CryptographicException)
                    {
                        // relay-key.pem is there but unusable (review S8): say what to do, and keep trying in case it is fixed
                        SetState("error", "relay-key.pem cannot be read: delete relay-key.pem and relay-public.pem, then register the new public key");
                        _builder.Log.Warn($"{Product.Name} relay for {settings.Host}: relay-key.pem cannot be read ({e.GetType().Name}). To make a new key, " +
                                          "delete relay-key.pem and relay-public.pem in the panel's data folder; the new public key then needs registering again.");
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException or HttpRequestException or
                                                 WebSocketException or System.Security.Cryptography.CryptographicException or TimeoutException or JsonException)
                    {
                        // in plain words, once per change (a retry every 4 s would fill kiln.log with the same line)
                        var why = RelayErrors.Explain(e, settings);
                        var again = Volatile.Read(ref _lastError) == why && Volatile.Read(ref _state) == "error";
                        SetState("error", why);
                        if (!again) _builder.Log.Warn($"{Product.Name} relay for {settings.Host}: {why}; retrying every 4 s");
                    }
                }
                await Task.Delay(TimeSpan.FromSeconds(4), ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private bool CanRelay(RelaySettings settings)
    {
        var server = _builder.Settings.Current;
        return Hosting.ISetupState.RemoteReachAllowed(_builder.SetupState, server) && server.Tls.Mode != TlsMode.Off &&
            (server.AllowedHosts.Contains(settings.Host, StringComparer.OrdinalIgnoreCase) ||
             server.Tls.Hostnames.Contains(settings.Host, StringComparer.OrdinalIgnoreCase));
    }

    private async Task RunSessionAsync(HttpMessageInvoker http, RelayKey key, RelaySettings settings, HttpServer listener,
        RelayTarget target, CancellationToken ct)
    {
        using var session = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var control = await ConnectAsync(http, key, settings, "control", "", session.Token).ConfigureAwait(false);
        SetState("connected");
        _builder.Log.Info($"{Product.Name} relay connected for {settings.Host}");
        var watch = WatchAsync(settings, listener, target, session);
        var pipes = new Dictionary<string, Task>(StringComparer.Ordinal);
        var message = new byte[1024];
        var warnedPeer = false;
        try
        {
            while (!session.IsCancellationRequested)
            {
                var frame = await control.ReceiveAsync(message.AsMemory(), session.Token).ConfigureAwait(false);
                if (frame.MessageType == WebSocketMessageType.Close) break;
                if (frame.MessageType != WebSocketMessageType.Text || !frame.EndOfMessage)
                    throw new IOException("Invalid relay control message.");
                var incoming = JsonSerializer.Deserialize<Incoming>(message.AsSpan(0, frame.Count), Storage.DataStore.Json);
                if (incoming is not { Id.Length: 32, Ip: not null, Port: > 0 and <= 65535 } ||
                    !incoming.Id.All(char.IsAsciiHexDigitLower) || !IPAddress.TryParse(incoming.Ip, out var peer))
                    throw new IOException("Invalid relay peer.");
                // an honest relay only ever sees browsers on public addresses (review S3): anything else is not served
                if (!AcceptableAnnouncement(peer))
                {
                    if (!warnedPeer) _builder.Log.Warn($"{Product.Name} relay for {settings.Host}: the relay announced a non-public peer address; not served");
                    warnedPeer = true;
                    continue;
                }
                foreach (var done in pipes.Where(x => x.Value.IsCompleted).Select(x => x.Key).ToArray()) pipes.Remove(done);
                if (pipes.Count < MaxStreams && !pipes.ContainsKey(incoming.Id))
                    pipes.Add(incoming.Id, AttachAsync(http, key, settings, listener, incoming.Id, new IPEndPoint(peer, incoming.Port), session.Token));
            }
        }
        catch (OperationCanceledException) when (session.IsCancellationRequested) { }
        finally
        {
            if (Volatile.Read(ref _state) == "connected") SetState("waiting");
            await session.CancelAsync().ConfigureAwait(false);
            await watch.ConfigureAwait(false);
            await Task.WhenAll(pipes.Values).ConfigureAwait(false);
        }
    }

    private sealed record Incoming(string Id, string Ip, int Port);

    /// <summary>A peer address a relay may announce: a public unicast address (never loopback, private, link-local,
    /// CGNAT, multicast, unspecified or documentation ranges).</summary>
    internal static bool AcceptableAnnouncement(IPAddress peer) =>
        Webhooks.WebhookGuard.IsPublic(peer.IsIPv4MappedToIPv6 ? peer.MapToIPv4() : peer);

    private async Task WatchAsync(RelaySettings settings, HttpServer listener, RelayTarget target, CancellationTokenSource session)
    {
        try
        {
            while (ReferenceEquals(settings, _settings.Current) && ReferenceEquals(listener, target.Listener()) && CanRelay(settings))
                await Task.Delay(TimeSpan.FromSeconds(1), session.Token).ConfigureAwait(false);
            await session.CancelAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (session.IsCancellationRequested) { }
    }

    private async Task AttachAsync(HttpMessageInvoker http, RelayKey key, RelaySettings settings, HttpServer target,
        string id, IPEndPoint peer, CancellationToken ct)
    {
        try
        {
            using var socket = await ConnectAsync(http, key, settings, "data", id, ct).ConfigureAwait(false);
            using var kill = ct.Register(socket.Abort);
            await target.AdmitRelayAsync(new RelayStream(socket), peer).ConfigureAwait(false);
        }
        catch (Exception e) when (e is HttpRequestException or WebSocketException or IOException or TimeoutException or OperationCanceledException) { }
    }

    private async Task<ClientWebSocket> ConnectAsync(HttpMessageInvoker http, RelayKey key, RelaySettings settings, string kind,
        string id, CancellationToken ct)
    {
        var uri = new UriBuilder(settings.Origin) { Scheme = "wss", Path = Api + kind }.Uri;
        var ws = new ClientWebSocket();
        ws.Options.CollectHttpResponseDetails = true; // the relay's refusal status, for RelayErrors
        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        ws.Options.KeepAliveTimeout = TimeSpan.FromSeconds(15);
        key.Authorize(ws.Options.SetRequestHeader, kind, settings.Host, id, _builder.Environment.Time);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            await ws.ConnectAsync(uri, http, deadline.Token).ConfigureAwait(false);
            return ws;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            ws.Dispose();
            throw new TimeoutException($"{Product.Name} relay connection timed out.");
        }
        catch (WebSocketException e) when (ws.HttpStatusCode is var status && (int)status >= 400)
        {
            ws.Dispose();
            throw new RelayRefusedException((int)status, e);
        }
        catch
        {
            ws.Dispose();
            throw;
        }
    }
}
