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
    private static readonly TimeSpan ClaimLinkLife = TimeSpan.FromMinutes(15);
    /// <summary>The name's scheduled move or release, as the relay last told this panel (0.1.1: lane L1b), and when this
    /// server's own key takes a name over (the relay's refusal of a key a move waits for).</summary>
    private RelayControl.Scheduled? _scheduled;
    /// <summary>The relay said the name moved away (or was released): its last notice on the old link.</summary>
    private RelayControl.Done? _done;
    private long _movingHereUnix;

    private DateTimeOffset? MovingHere => Interlocked.Read(ref _movingHereUnix) is > 0 and var t ? DateTimeOffset.FromUnixTimeSeconds(t) : null;
    private readonly Lock _keyLock = new();
    private RelayKey? _key;
    private SettingsStore<RelaySettings> _settings = null!;
    private WebAdminBuilder _builder = null!;
    private CancellationTokenSource? _life;
    private Task? _run;
    private string _state = "waiting";
    private string? _lastError;
    private string? _problem;
    private string? _since;
    private RelayStatus? _status;

    private void SetState(string state, string? error = null, string? problem = null)
    {
        var was = Volatile.Read(ref _state);
        Volatile.Write(ref _state, state);
        if (error is not null) Volatile.Write(ref _lastError, error);
        Volatile.Write(ref _problem, state == "connected" ? null : problem ?? Volatile.Read(ref _problem));
        Volatile.Write(ref _since, _builder.Environment.Time.GetUtcNow().ToString("O"));
        if (state == "connected" && was != "connected") _status?.RaiseConnected();
        else if (state != "connected" && was == "connected") _status?.RaiseDisconnected();
    }

    public string Name => "relay";

    public void Configure(WebAdminBuilder builder)
    {
        _builder = builder;
        _settings = builder.SettingsFile<RelaySettings>("relay.json", RelaySettings.Validate);
        EditableSettings.Of(builder.Services).Add(EditableFile.For("relay", "Hosted access (relay)", _settings));
        builder.Services.Add(_settings);
        builder.Services.Add(_status = new RelayStatus(() =>
        {
            var s = _settings.Current;
            var state = !s.Enabled ? "off" : Volatile.Read(ref _state);
            return new RelayStatus.Info(s.Enabled, s.Host, state, Volatile.Read(ref _lastError), state == "connected" ? null : Volatile.Read(ref _problem));
        }));
        var claims = new RelayClaims(MakeClaimLink);
        builder.Services.Add(claims);
        builder.Services.Add(new RelayMoves(() => Upcoming(), () => MovingHere));
        // POST /api/core/relay/move/undo (lane L1b): undo the move or release scheduled for this panel's name, with this
        // panel's relay key (the owner, recent proof: core.settings.edit is sensitive). The relay takes the undo only
        // signed by the key the name has, so a Discord account alone can never undo, nor block, the server's own undo.
        new Modules.ModuleRegistration(builder.Modules, CoreComponent.ModuleId).MapPost("/relay/move/undo", Routing.RouteAccess.Requires(CoreComponent.SettingsEdit), async ctx =>
        {
            var s = _settings.Current;
            if (!s.Enabled || !AcmeSettings.IsDnsName(s.Host)) throw new Http.HttpError(409, "hosted access is off: there is nothing to undo");
            var (status, why) = await UndoAsync(s, ctx.Aborted).ConfigureAwait(false);
            ctx.Services.TryGet<Security.IAuditSink>()?.Record(new Security.AuditEntry(DateTimeOffset.UtcNow, ctx.User?.Id ?? "", ctx.User?.Name ?? "",
                "core.relay.move_undo", new System.Text.Json.Nodes.JsonObject { ["host"] = s.Host, ["status"] = status }, status == 204, null, 0, ctx.ClientAddress));
            return status switch
            {
                204 => Http.HttpResponse.Json(new System.Text.Json.Nodes.JsonObject { ["undone"] = true, ["host"] = s.Host }),
                404 => throw new Http.HttpError(409, "nothing is scheduled for this name any more"),
                _ => throw new Http.HttpError(502, why),
            };
        });
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
                ["namesPage"] = ClaimOrigin(s.Host) is { } claimPages ? claimPages + "/" : null,
                ["scheduled"] = s.Enabled ? ScheduledJson(Upcoming()) : null,
                ["movingHere"] = s.Enabled && MovingHere is { } here ? here.ToString("O") : null,
            }));
        });
    }

    /// <summary>The claim pages' origin for <paramref name="host"/> (lane L1b): <c>https://claim.&lt;zone&gt;</c>, the zone
    /// being the host without its first label (<c>myserver.kiln.nexus</c>: <c>https://claim.kiln.nexus</c>). <c>claim</c>
    /// is a reserved label there, never a panel. Null for a host that is not a name one label under a zone.</summary>
    internal static string? ClaimOrigin(string host)
    {
        if (!AcmeSettings.IsDnsName(host) || host != host.ToLowerInvariant()) return null;
        var dot = host.IndexOf('.', StringComparison.Ordinal);
        var zone = host[(dot + 1)..];
        return dot > 0 && zone.Contains('.', StringComparison.Ordinal) ? $"https://claim.{zone}" : null;
    }

    /// <summary>The scheduled change while its time is still ahead: one whose time passed while the link was down has
    /// happened, or not, without this panel hearing it, and its Undo can't work any more (the relay link's last error
    /// then says what this server's key gets).</summary>
    private RelayControl.Scheduled? Upcoming() =>
        Volatile.Read(ref _scheduled) is { } c && c.At > _builder.Environment.Time.GetUtcNow() ? c : null;

    internal static System.Text.Json.Nodes.JsonObject? ScheduledJson(RelayControl.Scheduled? c) => c is null ? null : new System.Text.Json.Nodes.JsonObject
    {
        ["to"] = c.To, ["requested"] = c.Requested.ToString("O"), ["at"] = c.At.ToString("O"),
    };

    /// <summary>A notice about this panel's name from the relay (lane L1b): kept for Panel settings and the banner, and
    /// said in kiln.log once per change.</summary>
    internal void TakeNotice(RelayControl.NameNotice n, RelaySettings settings)
    {
        if (!string.Equals(n.Host, settings.Host, StringComparison.Ordinal)) return;
        if (n.Done is { } d)
        {
            Volatile.Write(ref _scheduled, null);
            Volatile.Write(ref _done, d);
            _builder.Log.Warn(d.IsRelease
                ? $"{Product.Name} relay: {settings.Host} was released on {d.At.UtcDateTime:yyyy-MM-dd HH:mm} UTC (asked on the claim pages); it no longer reaches this server"
                : $"{Product.Name} relay: {settings.Host} moved to another server's key on {d.At.UtcDateTime:yyyy-MM-dd HH:mm} UTC (asked on the claim pages); " +
                  "it no longer reaches this server. If you did not ask for that, ask the relay's operator on the Kiln Discord.");
            return;
        }
        Volatile.Write(ref _done, null);
        var before = Interlocked.Exchange(ref _scheduled, n.Change);
        if (before == n.Change) return;
        if (n.Change is { } c)
            _builder.Log.Warn($"{Product.Name} relay: {settings.Host} is scheduled to {(c.IsRelease ? "be released" : "move to another server's key")} on " +
                              $"{c.At.UtcDateTime:yyyy-MM-dd HH:mm} UTC (asked {c.Requested.UtcDateTime:yyyy-MM-dd HH:mm} UTC); until then it stays with this server. " +
                              "If you did not ask for this, undo it: Panel settings > Hosted access > Undo (the owner), then secure the Discord account " +
                              "that holds the name: whoever asked was signed in with it.");
        else if (before is not null)
            _builder.Log.Info($"{Product.Name} relay: nothing is scheduled for {settings.Host} any more; it stays with this server");
    }

    /// <summary>Sends the undo of the name's scheduled move or release (<c>DELETE ORIGIN/kiln/webadmin/relay/move</c>,
    /// signed by this panel's relay key, kind <c>move-undo</c>, an empty id): the relay's status (204 undone, 404
    /// nothing scheduled), or 0 and why it did not answer.</summary>
    private async Task<(int Status, string Why)> UndoAsync(RelaySettings s, CancellationToken ct)
    {
        RelayKey key;
        try { key = SharedKey(); }
        catch (Exception e) when (e is ArgumentException or System.Security.Cryptography.CryptographicException or IOException)
        {
            return (0, "relay-key.pem cannot be read");
        }
        using var http = new HttpMessageInvoker(new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseCookies = false, UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(8),
        });
        using var req = new HttpRequestMessage(HttpMethod.Delete, new UriBuilder(s.Origin) { Path = Api + "move" }.Uri);
        key.Authorize((name, value) => req.Headers.TryAddWithoutValidation(name, value), "move-undo", s.Host, "", _builder.Environment.Time);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var res = await http.SendAsync(req, deadline.Token).ConfigureAwait(false);
            var status = (int)res.StatusCode;
            if (status == 204)
            {
                Volatile.Write(ref _scheduled, null);
                _builder.Log.Warn($"{Product.Name} relay: undid what was scheduled for {s.Host}; it stays with this server and is no longer tied to a Discord account " +
                                  "(make a claim link to manage it on the claim pages again)");
            }
            return (status, status switch
            {
                204 => "undone",
                404 => "nothing scheduled",
                401 => "the relay refused this server's signature (is the server's clock right?)",
                403 => "the relay does not take this server's key for the name any more",
                _ => $"the relay answered HTTP {status}",
            });
        }
        catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException)
        {
            return (0, $"no answer from the relay ({e.GetType().Name})");
        }
    }

    /// <summary>What the relay link waits for (besides the relay itself).</summary>
    private List<string> WaitingFor(RelaySettings s)
    {
        var server = _builder.Settings.Current;
        var waiting = new List<string>();
        if (!RemoteReach(server)) waiting.Add("an owner account (or the console's kilnwarden setup for the first-run setup)");
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
        if (ClaimOrigin(s.Host) is not { } claimPages)
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
        var url = $"{claimPages}/claim?{key.ClaimQuery(s.Host, _builder.Environment.Time)}";
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
                        SetState("error", "relay-key.pem cannot be read: delete relay-key.pem and relay-public.pem, then register the new public key", "key");
                        _builder.Log.Warn($"{Product.Name} relay for {settings.Host}: relay-key.pem cannot be read ({e.GetType().Name}). To make a new key, " +
                                          "delete relay-key.pem and relay-public.pem in the panel's data folder; the new public key then needs registering again.");
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException or HttpRequestException or
                                                 WebSocketException or System.Security.Cryptography.CryptographicException or TimeoutException or JsonException)
                    {
                        // lane L1b: a key the name moves to later is told when; once the name moved away, say so
                        Interlocked.Exchange(ref _movingHereUnix, e is RelayRefusedException { MovingAt: { } at } ? at.ToUnixTimeSeconds() : 0);
                        // what the relay said last (its notice at the switch), or a scheduled change whose time has passed
                        // by this server's clock (the link was down at the switch)
                        var past = Volatile.Read(ref _done) is { } told ? new RelayControl.Scheduled(told.To, told.At, told.At)
                            : Volatile.Read(ref _scheduled) is { } due && due.At <= _builder.Environment.Time.GetUtcNow() ? due : null;
                        // in plain words, once per change (a retry every 4 s would fill kiln.log with the same line)
                        var why = RelayErrors.Explain(e, settings, past);
                        var again = Volatile.Read(ref _lastError) == why && Volatile.Read(ref _state) == "error";
                        SetState("error", why, RelayErrors.Kind(e, past));
                        if (!again) _builder.Log.Warn($"{Product.Name} relay for {settings.Host}: {why}; retrying every 4 s");
                    }
                }
                await Task.Delay(TimeSpan.FromSeconds(4), ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    /// <summary>The panel may be reached through the relay: the owner exists (or settings.json remoteSetup), or the
    /// server console's hosted setup is open (0.1.2: <c>kilnwarden setup hosted</c>, an hour at a time).</summary>
    private bool RemoteReach(ServerSettings server) =>
        Hosting.ISetupState.RemoteReachAllowed(_builder.SetupState, server) || _builder.Services.TryGet<Setup.HostedSetupWindow>()?.Open == true;

    private bool CanRelay(RelaySettings settings)
    {
        var server = _builder.Settings.Current;
        return RemoteReach(server) && server.Tls.Mode != TlsMode.Off &&
            (server.AllowedHosts.Contains(settings.Host, StringComparer.OrdinalIgnoreCase) ||
             server.Tls.Hostnames.Contains(settings.Host, StringComparer.OrdinalIgnoreCase));
    }

    private async Task RunSessionAsync(HttpMessageInvoker http, RelayKey key, RelaySettings settings, HttpServer listener,
        RelayTarget target, CancellationToken ct)
    {
        using var session = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var control = await ConnectAsync(http, key, settings, "control", "", session.Token).ConfigureAwait(false);
        Interlocked.Exchange(ref _movingHereUnix, 0);
        Volatile.Write(ref _done, null);
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
                RelayControl.Announce incoming;
                switch (RelayControl.Parse(message.AsSpan(0, frame.Count)))
                {
                    case RelayControl.NameNotice n:
                        TakeNotice(n, settings);
                        continue;
                    case RelayControl.Ignored:
                        continue;
                    case RelayControl.Announce a:
                        incoming = a;
                        break;
                    default:
                        continue;
                }
                var peer = incoming.Peer;
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
        // 0.1.1: notices about the name on the control link (docs/protocol-relay-broker.md §3.1); a relay sends them only
        // to a link that asks, so 0.1.0 mods never see one
        if (kind == "control") ws.Options.SetRequestHeader("X-Kwa-Notices", "1");
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
            // a key the name is scheduled to move to: when it may connect (lane L1b)
            DateTimeOffset? movingAt = null;
            if (ws.HttpResponseHeaders is { } headers && headers.TryGetValue("x-kwa-moving-at", out var v) && v.FirstOrDefault() is { } t &&
                long.TryParse(t, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var unix) && unix is > 0 and < 253_402_300_800)
                movingAt = DateTimeOffset.FromUnixTimeSeconds(unix);
            ws.Dispose();
            throw new RelayRefusedException((int)status, e) { MovingAt = movingAt };
        }
        catch
        {
            ws.Dispose();
            throw;
        }
    }
}
