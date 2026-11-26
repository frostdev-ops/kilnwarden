// SPDX-License-Identifier: MIT
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Config;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Identity;
using Kilnwarden.Modules;
using Kilnwarden.Relay;
using Kilnwarden.Routing;
using Kilnwarden.Storage;
using Kilnwarden.Tls.Acme;

namespace Kilnwarden.Setup;

/// <summary>setup.json: how far the setup wizard got (0.1.2). Not secret. A panel that had an owner before 0.1.2 starts
/// with <see cref="Done"/> set, so nobody is sent through the wizard again.</summary>
public sealed class SetupProgress
{
    public int Format { get; set; } = 1;
    public bool Done { get; set; }
    public DateTimeOffset? DoneAt { get; set; }

    /// <summary>"hosted", "launcher", "own" or "" (not chosen yet).</summary>
    public string Route { get; set; } = "";

    /// <summary>The wizard page the owner was on (the web app's id), to come back to after a restart.</summary>
    public string Step { get; set; } = "";

    /// <summary>A hosted name this server's relay link was up for once: it was claimed (the link proves it).</summary>
    public string? ClaimedHost { get; set; }

    /// <summary>The own-address route: the name and the port people type.</summary>
    public string? OwnHost { get; set; }

    public int OwnPort { get; set; }

    public static IReadOnlyList<string> Validate(SetupProgress p) =>
        p.Route is "" or "hosted" or "launcher" or "own" ? [] : ["route must be hosted, launcher, own or empty"];
}

/// <summary>Test hooks (internal; the dev host's end-to-end runs use them too).</summary>
internal sealed class SetupOptions
{
    public SelfProbe? Probe { get; init; }
    public TimeSpan? ProbeEvery { get; init; }

    /// <summary>The Discord broker's reachability check (0.1.3): null when it answers, else what went wrong.</summary>
    public Func<string, CancellationToken, Task<string?>>? BrokerCheck { get; init; }
}

/// <summary>One line of the setup checklist: <see cref="State"/> is "done", "working" (it is happening by itself),
/// "waiting" (for an earlier step or for the owner) or "failed" (<see cref="Problem"/> says why and what to do).</summary>
public sealed record SetupStep(string Id, string State, SetupProblem? Problem = null, string? Note = null);

/// <summary>A failed step in plain words: a short title, the fix, and the technical detail (shown behind "Details").</summary>
public sealed record SetupProblem(string Title, string Fix, string? Detail = null);

/// <summary>The setup wizard (0.1.2): the panel's /api/setup (the owner's wizard after the account exists: how to open
/// the panel, hosted access, an own address, the launcher, Discord sign-in, done; 0.1.3 adds the owner's own Discord
/// sign-in, then mods and the dashboard, which use the Mods page's and the overview's own routes) and the server console's
/// <c>kilnwarden setup</c>, <c>kilnwarden setup hosted &lt;name&gt;</c> and <c>kilnwarden status</c>. It writes the
/// settings the old manual setup asked for by hand (relay.json, settings.json tls, acme.json DNS-01) and watches each
/// step until the panel really answers at its address.</summary>
public sealed class SetupComponent : IWebAdminComponent
{
    public const string ModuleId = "setup";
    public const string Launcher = "0.2.0-dev.31.14";
    public static readonly TimeSpan HostedLinkLife = TimeSpan.FromMinutes(30);

    private readonly SetupOptions _o;
    private WebAdminBuilder _b = null!;
    private SettingsStore<SetupProgress> _progress = null!;
    private HostedSetupWindow _window = null!;
    private bool _existed;
    private ITimer? _timer;
    private SelfProbe _prober = null!;
    private (string Key, SelfProbe.Result Result)? _probe;
    private int _probing;
    private CancellationTokenSource? _life;
    private (DateTimeOffset At, SetupDetect.Facts Facts)? _facts;

    public SetupComponent() : this(null)
    {
    }

    internal SetupComponent(SetupOptions? options)
    {
        _o = options ?? new SetupOptions();
    }

    public string Name => "setup";

    private AuthService Auth => _b.Services.Get<AuthService>();
    private SettingsStore<RelaySettings>? RelayStore => _b.Services.TryGet<SettingsStore<RelaySettings>>();
    private SettingsStore<AcmeSettings>? AcmeStore => _b.Services.TryGet<SettingsStore<AcmeSettings>>();
    private AcmeManager? Acme => _b.Services.TryGet<AcmeManager>();
    private RelayStatus? Relay => _b.Services.TryGet<RelayStatus>();
    private TimeProvider Time => _b.Environment.Time;

    public SetupProgress Progress => _progress.Current;

    public void Configure(WebAdminBuilder b)
    {
        _b = b;
        _existed = b.Data.Exists("setup.json");
        _progress = b.SettingsFile<SetupProgress>("setup.json", SetupProgress.Validate);
        _window = new HostedSetupWindow(b.Environment.Time, () => b.Services.TryGet<AuthService>()?.OwnerExists == true);
        _prober = _o.Probe ?? new SelfProbe();
        b.Services.Add(_window);
        b.Services.Add(this);
        var m = b.Modules.Register(new ModuleSpec
        {
            Id = ModuleId, Title = "Setup", Icon = "wand-sparkles", CanDisable = false,
            Description = "The setup wizard: how you open the panel, hosted access, your own address, Discord sign-in",
        }, Product.ModId);
        Map(m);
    }

    public Task StartAsync(WebAdminServices services, CancellationToken ct)
    {
        _life = new CancellationTokenSource();
        // a panel that had its owner before 0.1.2: its setup is done
        if (!_existed && Auth.OwnerExists) Save(p => { p.Done = true; p.DoneAt = Time.GetUtcNow(); });
        var every = _o.ProbeEvery ?? TimeSpan.FromSeconds(15);
        _timer = Time.CreateTimer(_ => MaybeProbe(), null, every, every);
        if (Relay is { } r) r.Connected += OnRelayConnected;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _timer?.Dispose();
        if (Relay is { } r) r.Connected -= OnRelayConnected;
        _life?.Cancel();
        _life?.Dispose();
        return Task.CompletedTask;
    }

    private void OnRelayConnected()
    {
        if (RelayStore?.Current is { Enabled: true, Host.Length: > 0 } s && Progress.ClaimedHost != s.Host) Save(p => p.ClaimedHost = s.Host);
    }

    private readonly Lock _saveLock = new();

    /// <summary>One change of setup.json at a time (the wizard's place and its "done" can arrive together).</summary>
    private void Save(Action<SetupProgress> change)
    {
        lock (_saveLock)
        {
            var p = Copy(_progress.Current);
            change(p);
            _progress.Save(p);
        }
    }

    private static T Copy<T>(T v) where T : class => JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(v, DataStore.Json), DataStore.Json)!;

    // ------------------------------------------------------------------------------------------------ the routes

    private void Map(ModuleRegistration m)
    {
        // what the wizard shows; owner only (core.settings.edit), a GET so no recent proof
        m.MapGet("/state", RouteAccess.Requires(CoreComponent.SettingsEdit), ctx =>
        {
            MaybeProbe();
            return ValueTask.FromResult(HttpResponse.Json(State(ctx)));
        });
        // the wizard's place (no setting changes): the owner, without a recent proof
        m.MapPut("/progress", RouteAccess.Requires(CoreComponent.SettingsView), ctx =>
        {
            Owner(ctx);
            var body = ctx.ReadJson<JsonObject>();
            var route = body["route"]?.GetValue<string>();
            var step = body["step"]?.GetValue<string>();
            if (route is not null and not ("" or "hosted" or "launcher" or "own")) throw new HttpError(422, "route must be hosted, launcher or own");
            if (step is { Length: > 40 } || step?.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-')) == true) throw new HttpError(422, "step is a short id");
            Save(p =>
            {
                if (route is not null) p.Route = route;
                if (step is not null) p.Step = step;
            });
            return ValueTask.FromResult(HttpResponse.Json(new JsonObject { ["ok"] = true }));
        }, 1024);
        m.MapPost("/done", RouteAccess.Requires(CoreComponent.SettingsView), ctx =>
        {
            var p0 = Owner(ctx);
            Save(p => { p.Done = true; p.DoneAt = Time.GetUtcNow(); p.Step = "done"; });
            Audit(ctx, p0, "setup.done", null);
            return ValueTask.FromResult(HttpResponse.Json(new JsonObject { ["ok"] = true }));
        });
        // Panel settings > Run the setup again
        m.MapPost("/again", RouteAccess.Requires(CoreComponent.SettingsView), ctx =>
        {
            var p0 = Owner(ctx);
            Save(p => { p.Done = false; p.Step = "route"; });
            Audit(ctx, p0, "setup.again", null);
            return ValueTask.FromResult(HttpResponse.Json(new JsonObject { ["ok"] = true }));
        });
        // hosted access: the name, every setting it needs, and a claim link (owner, recent proof)
        m.MapPost("/hosted", RouteAccess.Requires(CoreComponent.SettingsEdit), ctx =>
        {
            var label = PanelNames.Normalize(ctx.ReadJson<JsonObject>()["name"]?.GetValue<string>());
            if (PanelNames.Problem(label) is { } problem) throw new HttpError(422, problem);
            var host = ConfigureHosted(label);
            Audit(ctx, (WebPrincipal)ctx.User!, "setup.hosted", new JsonObject { ["host"] = host });
            return ValueTask.FromResult(HttpResponse.Json(ClaimJson(ctx, "wizard")));
        }, 1024);
        m.MapPost("/hosted/claim", RouteAccess.Requires(CoreComponent.SettingsEdit), ctx => ValueTask.FromResult(HttpResponse.Json(ClaimJson(ctx, "wizard"))));
        m.MapPost("/check", RouteAccess.Requires(CoreComponent.SettingsView), ctx =>
        {
            Owner(ctx);
            _probe = null;
            MaybeProbe();
            return ValueTask.FromResult(HttpResponse.Json(new JsonObject { ["ok"] = true }));
        });
        // an own address: the name, the port people type, how Let's Encrypt checks it (owner, recent proof)
        m.MapPost("/own", RouteAccess.Requires(CoreComponent.SettingsEdit), ctx =>
        {
            var body = ctx.ReadJson<JsonObject>();
            var host = (body["host"]?.GetValue<string>() ?? "").Trim().TrimEnd('.').ToLowerInvariant();
            var port = body["port"]?.GetValue<int>() ?? 0;
            var challenge = body["challenge"]?.GetValue<string>() ?? "http-01";
            if (!AcmeSettings.IsDnsName(host)) throw new HttpError(422, "Type a web address such as panel.example.com (no https://, no port).");
            if (host.EndsWith("." + (RelayStore?.Current.Zone ?? PanelNames.Zone), StringComparison.Ordinal))
                throw new HttpError(422, "Names under kiln.nexus come with hosted access. Go back and pick Anywhere.");
            if (port is < 0 or > 65535) throw new HttpError(422, "The port is a number from 1 to 65535.");
            if (challenge is not ("http-01" or "tls-alpn-01")) throw new HttpError(422, "Pick how Let's Encrypt checks your address.");
            ConfigureOwn(host, port, challenge);
            Audit(ctx, (WebPrincipal)ctx.User!, "setup.own", new JsonObject { ["host"] = host, ["port"] = port, ["challenge"] = challenge });
            return ValueTask.FromResult(HttpResponse.Json(new JsonObject { ["ok"] = true }));
        }, 1024);
        // 0.1.3: Discord sign-in for the owner. Switches Discord sign-in on for the owner alone (discord.ownerOnly) when it
        // is off, checking the server the owner named or else the Kiln Discord; the link itself is the usual one
        // (/api/identity/start with intent link, back "setup": recent proof, the broker's pins, state and nonce, one
        // account per Discord user). First checks that the broker answers, unless the owner goes on anyway.
        m.MapPost("/discord/me", RouteAccess.Requires(CoreComponent.SettingsEdit), async ctx =>
        {
            var p0 = Owner(ctx);
            var anyway = ctx.ReadJson<JsonObject>()["anyway"]?.GetValue<bool>() == true;
            var store = _b.Services.TryGet<SettingsStore<IdentitySettings>>() ?? throw new HttpError(409, "Discord sign-in is not available in this build.");
            var id = _b.Services.TryGet<IdentityService>() ?? throw new HttpError(409, "Discord sign-in is not available in this build.");
            var broker = store.Current.Discord.Broker;
            if (!id.Discord.HasKeyFor(broker)) throw new HttpError(409, "Discord sign-in is not available in this build.");
            if (!anyway && await BrokerCheck(broker, ctx.Aborted).ConfigureAwait(false) is { } problem)
                return HttpResponse.Error(503, "broker_unreachable", problem);
            var s = Copy(store.Current);
            if (!s.Discord.Enabled)
            {
                s.Discord.Enabled = true;
                s.Discord.OwnerOnly = true;
                if (!IdentitySettings.SnowflakeRe().IsMatch(s.Discord.GuildId ?? "")) s.Discord.GuildId = DiscordSettings.KilnGuild;
                try { store.Save(s); }
                catch (ArgumentException e) { throw new HttpError(422, e.Message); }
            }
            Audit(ctx, p0, "setup.discord.me", new JsonObject { ["guildId"] = s.Discord.GuildId, ["ownerOnly"] = s.Discord.OwnerOnly });
            return HttpResponse.Json(new JsonObject { ["ok"] = true, ["guildId"] = s.Discord.GuildId, ["ownerOnly"] = s.Discord.OwnerOnly });
        }, 1024);
        // Discord sign-in for the other admins (owner, recent proof): their Discord server; everyone who links may sign in
        m.MapPost("/discord", RouteAccess.Requires(CoreComponent.SettingsEdit), ctx =>
        {
            var guild = (ctx.ReadJson<JsonObject>()["guildId"]?.GetValue<string>() ?? "").Trim();
            if (guild.Length is < 5 or > 20 || !guild.All(char.IsAsciiDigit)) throw new HttpError(422, "A Discord server ID is a long number, such as 1568454645683195976.");
            var store = _b.Services.TryGet<SettingsStore<IdentitySettings>>() ?? throw new HttpError(409, "Discord sign-in is not available in this build.");
            var s = Copy(store.Current);
            s.Discord.Enabled = true;
            s.Discord.OwnerOnly = false;
            s.Discord.GuildId = guild;
            try { store.Save(s); }
            catch (ArgumentException e) { throw new HttpError(422, e.Message); }
            Audit(ctx, (WebPrincipal)ctx.User!, "setup.discord", new JsonObject { ["guildId"] = guild });
            return ValueTask.FromResult(HttpResponse.Json(new JsonObject { ["ok"] = true }));
        }, 1024);
    }

    private Task<string?> BrokerCheck(string broker, CancellationToken ct) => (_o.BrokerCheck ?? CheckBrokerAsync)(broker, ct);

    /// <summary>GET {broker}/kiln/auth/discord/health (the broker's own health page): null when it answers 200 within a
    /// few seconds, else what happened, for "Details".</summary>
    internal static async Task<string?> CheckBrokerAsync(string broker, CancellationToken ct)
    {
        using var http = new HttpMessageInvoker(new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseCookies = false, UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(6), MaxResponseHeadersLength = 16,
        });
        using var t = CancellationTokenSource.CreateLinkedTokenSource(ct);
        t.CancelAfter(TimeSpan.FromSeconds(8));
        var url = $"{broker.TrimEnd('/')}/kiln/auth/discord/health";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var res = await http.SendAsync(req, t.Token).ConfigureAwait(false);
            return res.StatusCode == HttpStatusCode.OK ? null : $"{url} answered {(int)res.StatusCode}";
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return $"{url} did not answer within 8 seconds";
        }
        catch (Exception e) when (e is HttpRequestException or IOException or System.Security.Authentication.AuthenticationException)
        {
            return $"{url}: {e.Message}";
        }
    }

    private static WebPrincipal Owner(RequestContext ctx) =>
        ctx.User is WebPrincipal { IsOwner: true } p ? p : throw new HttpError(403, "only the owner runs the setup");

    private static void Audit(RequestContext ctx, WebPrincipal p, string op, JsonObject? args) =>
        ctx.Services.TryGet<Security.IAuditSink>()?.Record(AuditLog.Entry(op, p, ctx.ClientAddress, true, args: args));

    private JsonObject ClaimJson(RequestContext ctx, string source)
    {
        var claims = _b.Services.TryGet<RelayClaims>() ?? throw new HttpError(409, "hosted access is not part of this build");
        var link = claims.Make(source);
        if (link.Error is { } error) throw new HttpError(409, error);
        ctx.Services.TryGet<Security.IAuditSink>()?.Record(new Security.AuditEntry(DateTimeOffset.UtcNow, ctx.User?.Id ?? "", ctx.User?.Name ?? "",
            "core.relay.claim_link", new JsonObject { ["host"] = link.Host, ["fingerprint"] = link.Fingerprint, ["source"] = source }, true, null, 0, ctx.ClientAddress));
        return new JsonObject { ["claimUrl"] = link.Url, ["host"] = link.Host, ["expires"] = link.Expires?.ToString("O") };
    }

    // ------------------------------------------------------------------------------------------------ writing the settings

    /// <summary>Hosted access for <paramref name="label"/>: relay.json (on, the name), acme.json (the terms, DNS-01 through
    /// the relay, the name) and settings.json (tls.mode acme, the name allowed). The relay's origin and zone, the ACME
    /// directory and every other setting stay as they are. The certificate's order waits for the relay link.</summary>
    internal string ConfigureHosted(string label)
    {
        var relayStore = RelayStore ?? throw new HttpError(409, "hosted access is not part of this build");
        var acmeStore = AcmeStore ?? throw new HttpError(409, "Let's Encrypt is not part of this build");
        var r = Copy(relayStore.Current);
        var old = r.Host;
        var host = PanelNames.Host(label, r.Zone);
        r.Enabled = true;
        r.Host = host;
        relayStore.Save(r);
        var a = Copy(acmeStore.Current);
        a.AcceptTerms = true;
        a.Challenge = "dns-01";
        a.Names = [host];
        acmeStore.Save(a);
        var s = Copy(_b.Settings.Current);
        s.Tls.Mode = TlsMode.Acme;
        if (old.Length > 0 && old != host) s.AllowedHosts.RemoveAll(h => string.Equals(h, old, StringComparison.OrdinalIgnoreCase));
        if (!s.AllowedHosts.Contains(host, StringComparer.OrdinalIgnoreCase)) s.AllowedHosts.Add(host);
        if (!SameServerSettings(s, _b.Settings.Current)) _b.Settings.Save(s);
        Save(p => p.Route = "hosted");
        return host;
    }

    private void ConfigureOwn(string host, int port, string challenge)
    {
        var acmeStore = AcmeStore ?? throw new HttpError(409, "Let's Encrypt is not part of this build");
        var a = Copy(acmeStore.Current);
        a.AcceptTerms = true;
        a.Challenge = challenge;
        a.Names = [host];
        acmeStore.Save(a);
        if (RelayStore is { } relayStore && relayStore.Current.Enabled)
        {
            var r = Copy(relayStore.Current);
            r.Enabled = false;
            relayStore.Save(r);
        }
        var s = Copy(_b.Settings.Current);
        if (IPAddress.TryParse(s.Bind, out var bind) && IPAddress.IsLoopback(bind)) s.Bind = "0.0.0.0";
        s.Tls.Mode = TlsMode.Acme;
        if (!s.AllowedHosts.Contains(host, StringComparer.OrdinalIgnoreCase)) s.AllowedHosts.Add(host);
        _b.Settings.Save(s);
        Save(p => { p.Route = "own"; p.OwnHost = host; p.OwnPort = port; });
    }

    private static bool SameServerSettings(ServerSettings a, ServerSettings b) =>
        JsonSerializer.Serialize(a, DataStore.Json) == JsonSerializer.Serialize(b, DataStore.Json);

    // ------------------------------------------------------------------------------------------------ the checklist

    /// <summary>The port the panel listens on (settings.json port, or the query port + 100).</summary>
    private int PanelPort() => _b.Server.Endpoints.FirstOrDefault()?.Port ?? _b.Settings.Current.Port;

    /// <summary>The hosted access checklist: connected (to the relay), claimed, secure (the certificate), ready (the
    /// panel answers at its address). Null while hosted access is off.</summary>
    public IReadOnlyList<SetupStep>? HostedSteps(out string host)
    {
        host = "";
        if (RelayStore?.Current is not { Enabled: true, Host.Length: > 0 } settings) return null;
        host = settings.Host;
        var r = Relay?.Describe() ?? new RelayStatus.Info(true, settings.Host, "waiting", null);
        var linked = r.State == "connected";
        var kind = linked ? null : r.Problem;
        var reached = linked || kind is "unclaimed" or "moving" or "moved" or "clock" or "limited" or "busy";
        var detail = r.LastError;
        SetupStep connected = reached ? new("connected", "done")
            : kind is null ? new("connected", "working")
            : new("connected", "failed", RelayProblem(kind, settings, detail));
        var claimedBefore = Progress.ClaimedHost == settings.Host && kind is not ("unclaimed" or "moved");
        SetupStep claimed = linked || claimedBefore ? new("claimed", "done")
            : kind == "unclaimed" ? new("claimed", "waiting", Note: "claim")
            : kind == "moving" ? new("claimed", "waiting", Note: "moving")
            : kind is "moved" or "clock" ? new("claimed", "failed", RelayProblem(kind, settings, detail))
            : new("claimed", connected.State == "done" ? "working" : "waiting");
        var secure = SecureStep(host, linked, dns01: true);
        var ready = ReadyStep($"hosted:{host}", linked && secure.State == "done");
        return [connected, claimed, secure, ready];
    }

    /// <summary>The own-address checklist: found (the name resolves), reachable (this panel answers there), secure,
    /// ready (a trusted certificate there). Null unless that route is chosen.</summary>
    public IReadOnlyList<SetupStep>? OwnSteps(out string host, out int port)
    {
        host = Progress.OwnHost ?? "";
        port = Progress.OwnPort > 0 ? Progress.OwnPort : PanelPort();
        if (Progress.Route != "own" || host.Length == 0) return null;
        var p = _probe is { } x && x.Key.StartsWith($"own:{host}:{port}:", StringComparison.Ordinal) ? x.Result : null;
        SetupStep found = p is null ? new("found", "working")
            : p.Kind == "dns" ? new("found", "failed", new SetupProblem($"{host} doesn't point to a server yet.",
                "At your DNS provider, point the name at this server's public IP address. It can take a few minutes to work.", p.Detail))
            : new("found", "done");
        SetupStep reachable = p is null || found.State != "done" ? new("reachable", "waiting")
            : p.Ok || p.Kind is "untrusted" or "http" ? new("reachable", "done")
            : new("reachable", "failed", new SetupProblem($"This server can't reach {host}:{port}.",
                $"Open port {port} in the server's firewall and your router. At home, routers often can't loop back to themselves: try opening the address on your phone with Wi-Fi off.", p.Detail));
        var secure = SecureStep(host, reachable.State == "done", dns01: false);
        var ready = ReadyStep($"own:{host}:{port}", secure.State == "done");
        if (ready.State == "waiting" && p is not null && secure.State == "done") ready = new("ready", "working");
        return [found, reachable, secure, ready];
    }

    private SetupStep SecureStep(string host, bool canStart, bool dns01)
    {
        if (Acme is not { } acme) return new("secure", "failed", new SetupProblem("Let's Encrypt is not part of this build.", "Use a release build of Kilnwarden."));
        // A cached order is not enough: the live named TLS engine must actually serve that certificate.
        if (acme.Good is { } g && g.Info.Names.Contains(host, StringComparer.OrdinalIgnoreCase) && g.Info.NotAfter > Time.GetUtcNow() &&
            _b.Services.Get<RelayTarget>().Listener()?.Tls?.Certificate.Sha256 == g.Info.Sha256) return new("secure", "done");
        if (!canStart) return new("secure", "waiting");
        var st = acme.Status();
        var phase = st["phase"]?.GetValue<string>() ?? "";
        var error = st["lastError"]?.GetValue<string>();
        var blocked = st["blocked"]?.GetValue<string>();
        var next = st["nextAttempt"]?.GetValue<string>() is { } n && DateTimeOffset.TryParse(n, out var t) ? t : (DateTimeOffset?)null;
        if (phase == "ordering") return new("secure", "working");
        if (blocked is { } b && b.Contains("budget", StringComparison.Ordinal))
            return new("secure", "failed", new SetupProblem("Too many certificate tries this week.", "It tries again by itself in a few days.", b));
        if (error is not null && next is { } at && at > Time.GetUtcNow())
            return new("secure", "failed", new SetupProblem("The certificate didn't come through.",
                dns01 ? $"It tries again by itself at {at.ToLocalTime():HH:mm}. Nothing to do unless this stays for hours: then ask on the Kiln Discord."
                    : $"Let's Encrypt must reach this server: {(_progressChallenge() == "tls-alpn-01" ? "port 443 must reach the panel" : "port 80 must be open to it")}. It tries again at {at.ToLocalTime():HH:mm}.",
                error));
        return new("secure", "working");
    }

    private string _progressChallenge() => AcmeStore?.Current.Challenge ?? "http-01";

    private SetupStep ReadyStep(string key, bool canStart)
    {
        if (!canStart) return new("ready", "waiting");
        if (_probe is not { } p || !p.Key.StartsWith(key + ":", StringComparison.Ordinal) || !p.Key.EndsWith(":trusted", StringComparison.Ordinal))
            return new("ready", "working");
        if (p.Result.Ok) return new("ready", "done");
        return new("ready", "failed", new SetupProblem("Your panel's address doesn't answer from here yet.",
            "It checks again every 15 seconds. You can also open the address on your phone to try it.", p.Result.Detail));
    }

    private static SetupProblem RelayProblem(string kind, RelaySettings s, string? detail)
    {
        var relay = Uri.TryCreate(s.Origin, UriKind.Absolute, out var u) ? u.Host : s.Origin;
        return kind switch
        {
            "dns" => new($"This server can't look up {relay}.", "Its internet connection or DNS isn't working. Ask your host to let the server reach the internet.", detail),
            "blocked" or "timeout" => new($"This server can't reach {relay}.", $"Your host may block outgoing connections. Ask them to allow outgoing HTTPS (port 443) to {relay}.", detail),
            "tls" => new($"The connection to {relay} isn't secure.", "Check that the server's clock is right. A firewall that inspects HTTPS can cause this too: ask your host.", detail),
            "no-relay" => new("Nothing answers at the hosted access address.", "In Panel settings > Hosted access > Details, set the relay back to https://frostdev.io.", detail),
            "key" => new("This server's hosted access key can't be read.", "Delete relay-key.pem and relay-public.pem in kiln/data/kilnwarden, restart the server and claim the name again.", detail),
            "moved" => new("This name now belongs to another server.", "Pick a new name. If you didn't move it, ask on the Kiln Discord.", detail),
            "clock" => new("This server's clock is wrong.", "Set the server's clock right (ask your host). It retries by itself.", detail),
            _ => new("Hosted access isn't connecting.", "It keeps trying by itself. If this stays, ask on the Kiln Discord.", detail),
        };
    }

    /// <summary>Starts the address check in the background when a route waits for it (no-op while one runs).</summary>
    internal void MaybeProbe()
    {
        try
        {
            if (_life is not { IsCancellationRequested: false } life) return;
            var acme = Acme;
            var named = acme?.Good;
            var loopback = _b.Server.Certificate;
            string host;
            int port;
            bool trusted;
            string key;
            if (HostedSteps(out var h) is { } hs && hs[0].State == "done" && hs[1].State == "done" && hs[2].State == "done")
            {
                (host, port, trusted) = (h, 443, true);
                key = $"hosted:{host}:{named?.Info.Sha256}:trusted";
            }
            else if (Progress.Route == "own" && Progress.OwnHost is { Length: > 0 } oh)
            {
                host = oh;
                port = Progress.OwnPort > 0 ? Progress.OwnPort : PanelPort();
                trusted = named is { } g && g.Info.Names.Contains(oh, StringComparer.OrdinalIgnoreCase);
                key = $"own:{host}:{port}:{named?.Info.Sha256}:{(trusted ? "trusted" : "any")}";
            }
            else return;
            if (_probe is { } last && last.Key == key && (last.Result.Ok || Time.GetUtcNow() - last.Result.At < TimeSpan.FromSeconds(10))) return;
            if (Interlocked.CompareExchange(ref _probing, 1, 0) != 0) return;
            var ours = new List<string>();
            if (named is not null) ours.Add(named.Info.Sha256);
            if (loopback is not null) ours.Add(loopback.Sha256);
            _ = Task.Run(async () =>
            {
                try
                {
                    var r = await _prober.RunAsync(host, port, ours, trusted, Time, life.Token).ConfigureAwait(false);
                    _probe = (key, r);
                    if (!r.Ok) _b.Log.Debug($"setup: the check of {host}:{port} failed ({r.Kind}: {r.Detail})");
                    else _b.Log.Info($"{Product.Name}: your panel answers at https://{host}{(port == 443 ? "" : $":{port}")}/");
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    _probe = (key, new SelfProbe.Result(false, "other", e.Message, Time.GetUtcNow()));
                }
                finally
                {
                    Volatile.Write(ref _probing, 0);
                }
            });
        }
        catch (InvalidOperationException)
        {
            // still starting
        }
    }

    // ------------------------------------------------------------------------------------------------ the wizard's view

    private SetupDetect.Facts Facts()
    {
        if (_facts is { } f && Time.GetUtcNow() - f.At < TimeSpan.FromMinutes(5)) return f.Facts;
        var facts = SetupDetect.Probe();
        _facts = (Time.GetUtcNow(), facts);
        return facts;
    }

    private JsonObject State(RequestContext ctx)
    {
        var p = Progress;
        var facts = Facts();
        var server = _b.Settings.Current;
        var relay = RelayStore?.Current;
        var acmeS = AcmeStore?.Current;
        var ownSet = IPAddress.TryParse(server.Bind, out var bind) && !IPAddress.IsLoopback(bind) && acmeS is { Challenge: not "dns-01", Names.Count: > 0 };
        var viaHosted = ctx.Connection.Relayed;
        var (suggested, reason) = viaHosted ? ("hosted", "hosted")
            : facts.Container ? ("hosted", "container")
            : facts.GameHost ? ("hosted", "gameHost")
            : ownSet ? ("own", "ownAddress")
            : facts.PublicAddress ? ("hosted", "publicAddress")
            : ("hosted", "default");
        var o = new JsonObject
        {
            ["done"] = p.Done, ["route"] = p.Route, ["step"] = p.Step,
            ["suggested"] = suggested, ["reason"] = reason,
            ["client"] = new JsonObject { ["thisMachine"] = !viaHosted && IPAddress.IsLoopback(ctx.ClientAddress), ["hosted"] = viaHosted },
            ["zone"] = relay?.Zone ?? PanelNames.Zone,
            ["address"] = _b.Server.Endpoints.FirstOrDefault()?.ToString(),
            ["launcher"] = Launcher,
        };
        if (HostedSteps(out var host) is { } hs)
            o["hosted"] = new JsonObject
            {
                ["host"] = host, ["name"] = PanelNames.LabelOf(host), ["url"] = $"https://{host}/", ["steps"] = Steps(hs), ["ready"] = hs.All(s => s.State == "done"),
            };
        else if (relay is { Host.Length: > 0 }) o["hosted"] = new JsonObject { ["host"] = relay.Host, ["name"] = PanelNames.LabelOf(relay.Host), ["off"] = true };
        if (OwnSteps(out var ownHost, out var ownPort) is { } os)
            o["own"] = new JsonObject
            {
                ["host"] = ownHost, ["port"] = ownPort, ["challenge"] = acmeS?.Challenge, ["url"] = $"https://{ownHost}{(ownPort == 443 ? "" : $":{ownPort}")}/",
                ["steps"] = Steps(os), ["ready"] = os.All(s => s.State == "done"),
            };
        else o["own"] = new JsonObject { ["port"] = PanelPort() };
        if (_b.Services.TryGet<IdentityService>() is { } id)
            o["discord"] = new JsonObject
            {
                ["enabled"] = id.Settings.Discord.Enabled, ["guildId"] = id.Settings.Discord.GuildId, ["available"] = id.Discord.HasKeyFor(id.Settings.Discord.Broker),
                ["ownerOnly"] = id.Settings.Discord.OwnerOnly, ["kilnGuild"] = id.Settings.Discord.GuildId == DiscordSettings.KilnGuild,
                ["linked"] = ctx.User is WebPrincipal me && me.Account.Links.ContainsKey("discord"),
            };
        return o;
    }

    private static JsonArray Steps(IEnumerable<SetupStep> steps) => new([.. steps.Select(s => (JsonNode)new JsonObject
    {
        ["id"] = s.Id, ["state"] = s.State, ["note"] = s.Note,
        ["problem"] = s.Problem is { } x ? new JsonObject { ["title"] = x.Title, ["fix"] = x.Fix, ["detail"] = x.Detail } : null,
    })]);

    // ------------------------------------------------------------------------------------------------ the server console

    /// <summary><c>kilnwarden setup hosted &lt;name&gt;</c>: hosted access for the name, the claim link (console only), and
    /// while there is no owner, the hour in which the relay may connect so the owner can then sign up at the name.</summary>
    internal string ConsoleHosted(string typed)
    {
        var label = PanelNames.Normalize(typed);
        if (label.Length == 0) return SetupTexts.HostedUsage;
        if (PanelNames.Problem(label) is { } problem) return $"{problem} {SetupTexts.HostedUsage}";
        var host = ConfigureHosted(label);
        var owner = Auth.OwnerExists;
        if (!owner) _window.Extend();
        var claimed = Progress.ClaimedHost == host;
        string? url = null;
        if (!claimed && _b.Services.TryGet<RelayClaims>() is { } claims)
        {
            var link = claims.Make("console");
            if (link.Error is { } e) return $"Hosted access is set to {host}, but no claim link: {e}.";
            url = link.Url;
            _b.Services.TryGet<Security.IAuditSink>()?.Record(new Security.AuditEntry(DateTimeOffset.UtcNow, "", "console", "core.relay.claim_link",
                new JsonObject { ["host"] = link.Host, ["fingerprint"] = link.Fingerprint, ["source"] = "console-setup" }, true, null, 0, null));
        }
        _b.Services.TryGet<Security.IAuditSink>()?.Record(new Security.AuditEntry(DateTimeOffset.UtcNow, "", "console", "setup.hosted",
            new JsonObject { ["host"] = host }, true, null, 0, null));
        return SetupTexts.HostedStarted(host, url, owner);
    }

    /// <summary><c>kilnwarden setup</c> while hosted access is on and no owner exists: the checklist, and once the name has
    /// its trusted certificate, the one-time link to create the owner there (30 minutes; never in kiln.log).</summary>
    internal string? ConsoleHostedProgress(Func<string, string> hostedLink)
    {
        // settings.json remoteSetup (before 0.1.2): kiln.log's kind of link works from anywhere, so that block stays
        if (Auth.OwnerExists || _b.Settings.Current.RemoteSetup || HostedSteps(out var host) is not { } steps) return null;
        _window.Extend();
        MaybeProbe();
        // The last step validates both the served certificate and trust, just as the owner's browser will.
        var ready = steps.All(s => s.State == "done");
        return SetupTexts.HostedProgress(host, steps, ready ? hostedLink(host) : null);
    }

    /// <summary>The checklist lines for <c>kilnwarden status</c>.</summary>
    internal IReadOnlyList<SetupStep>? StatusSteps(out string route, out string address)
    {
        route = Progress.Route;
        address = "";
        if (HostedSteps(out var host) is { } hs)
        {
            route = "hosted";
            address = $"https://{host}/";
            return hs;
        }
        if (OwnSteps(out var oh, out var op) is { } os)
        {
            address = $"https://{oh}{(op == 443 ? "" : $":{op}")}/";
            return os;
        }
        return null;
    }
}
