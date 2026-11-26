// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;
using Kilnwarden.Config;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Kap;
using Kilnwarden.Modules;
using Kilnwarden.Routing;

namespace Kilnwarden.Auth;

/// <summary>Test hooks (internal): weak hashing for speed, a clock, KAP client timings.</summary>
internal sealed class AuthOptions
{
    public int? Iterations { get; init; }
    public TimeProvider? Time { get; init; }
    public Func<KapClientOptions, KapClientOptions>? Kap { get; init; }
    public TimeSpan? SocketRecheck { get; init; }
}

/// <summary>Accounts, roles, sessions, the gate, the audit log and the KAP client (the auth lane), as one host component.
/// It installs the session and CSRF middleware, the permission gate, the setup state (the owner exists) and the audit
/// sink; registers the auth, users and audit modules and the KAP-backed modules (players, world, ... with the permission
/// of every Kiln.Admin op); and serves the admin pages' KAP WebSocket at /api/server/kap. Shared services for other
/// components: <see cref="AuthService"/>, <see cref="KapClient"/>, <see cref="AuditLog"/>, the stores.</summary>
public sealed class AuthComponent : IWebAdminComponent
{
    public const string AuthModule = "auth", UsersModule = "users", AuditModule = "audit", ServerModule = "server";

    private readonly AuthOptions _o;
    private AuthService? _auth;
    private KapClient? _kap;
    private KapGateway? _gateway;
    private IServerInfo? _server;
    private IWebAdminLog? _log;
    private SettingsStore<ServerSettings>? _serverSettings;
    private Func<Relay.RelaySettings?> _relay = () => null;
    private string? _setupToken;
    private int _linkLogged;

    public AuthComponent() : this(null)
    {
    }

    internal AuthComponent(AuthOptions? options)
    {
        _o = options ?? new AuthOptions();
    }

    public string Name => "auth";

    public void Configure(WebAdminBuilder b)
    {
        var env = b.Environment;
        var log = b.Log;
        var time = _o.Time ?? env.Time;
        var settings = b.SettingsFile<AuthSettings>("auth.json", AuthSettings.Validate);
        EditableSettings.Of(b.Services).Add(EditableFile.For("auth", "Sign-in and sessions", settings));
        var s = settings.Current;
        // an unreadable accounts.json or roles.json throws here: the panel does not start without them
        var accounts = new AccountStore(b.Data, time);
        var roles = new RoleStore(b.Data);
        var sessions = new SessionStore(b.Data, () => settings.Current, log, time);
        var hasher = _o.Iterations is { } weak
            ? new PasswordHasher(weak, s.MaxConcurrentHashes, env.GameThread, null, allowWeak: true)
            : new PasswordHasher(s.Pbkdf2Iterations, s.MaxConcurrentHashes, env.GameThread);
        var limiter = new SignInLimiter(() => settings.Current.SignIn, time);
        var audit = new AuditLog(b.Data, log);
        var kapOptions = new KapClientOptions { AdminDirectory = env.KilnAdminDataDirectory, Log = log };
        _kap = new KapClient(_o.Kap?.Invoke(kapOptions) ?? kapOptions);
        var providers = new SignInProviders();
        _auth = new AuthService(accounts, roles, sessions, hasher, limiter, () => settings.Current, audit, b.Modules, log, _kap, time, providers);
        _gateway = new KapGateway(_auth, _kap, b.Modules, log) { RecheckEvery = _o.SocketRecheck ?? TimeSpan.FromSeconds(5) };
        _server = b.Server;
        _log = log;
        _serverSettings = b.Settings;
        _relay = () => b.Services.TryGet<SettingsStore<Relay.RelaySettings>>()?.Current;
        _auth.RemoteSetupOn = () => b.Settings.Current.RemoteSetup;
        b.Services.Add(new FirstRunSetup(this));

        var authMod = b.Modules.Register(new ModuleSpec
        {
            Id = AuthModule, Title = "Sign-in", Icon = "log-in", CanDisable = false,
            Description = "Sign-in, two-factor, the first-run setup and your own account",
            Pages = [new PageSpec("account", "My account", PageKind.Builtin, "auth/account") { Icon = "user", Order = 990 }],
        }, Product.ModId);
        AuthRoutes.Map(authMod, _auth);

        var usersMod = b.Modules.Register(new ModuleSpec
        {
            Id = UsersModule, Title = "Users and roles", Icon = "users-round", CanDisable = false,
            Permissions =
            [
                new PermissionSpec(UserRoutes.View, "See accounts, roles and their sessions") { DefaultRole = BuiltinRole.Admin },
                new PermissionSpec(UserRoutes.Manage, "Create, change, disable and remove accounts (never above your own permissions)")
                    { DefaultRole = BuiltinRole.Owner, Sensitive = true },
                new PermissionSpec(UserRoutes.RolesPerm, "Create and change roles (never above your own permissions)") { DefaultRole = BuiltinRole.Owner, Sensitive = true },
            ],
            Pages = [new PageSpec("users", "Users and roles", PageKind.Builtin, "users") { Icon = "users-round", Permission = UserRoutes.View, Order = 910 }],
        }, Product.ModId);
        UserRoutes.Map(usersMod, _auth);

        var auditMod = b.Modules.Register(new ModuleSpec
        {
            Id = AuditModule, Title = "Audit log", Icon = "scroll-text",
            Description = "Who did what: sign-ins, account changes and every action (it is recorded even while this page is off)",
            Permissions = [new PermissionSpec(AuditRoutes.View, "Read the audit log") { DefaultRole = BuiltinRole.Admin }],
            Pages = [new PageSpec("audit", "Audit log", PageKind.Builtin, "audit") { Icon = "scroll-text", Permission = AuditRoutes.View, Order = 920 }],
        }, Product.ModId);
        AuditRoutes.Map(auditMod, audit);

        foreach (var km in KapPolicy.Modules)
        {
            var reg = b.Modules.Register(new ModuleSpec
            {
                Id = km.Id, Title = km.Title, Icon = km.Icon, CanDisable = km.Id != ServerModule,
                Permissions = KapPolicy.PermissionsOf(km.Id),
                Pages = [.. km.Pages.Select(p => new PageSpec(p.Id, p.Title, PageKind.Builtin, p.Id) { Permission = p.Permission, Order = p.Order })],
            }, Product.ModId);
            if (km.Id == ServerModule) MapServer(reg);
        }

        // first of all: an older Kiln (Kiln.Admin without the 30.9 ops) gets a static refusal and loopback only
        var tooOld = new KilnTooOld(_kap, accounts);
        _kap.Incompatible += tooOld.OnIncompatible;
        b.Use(PipelineStage.Early, tooOld);
        b.Use(PipelineStage.Session, new SessionMiddleware(_auth));
        b.Use(PipelineStage.Csrf, new CsrfMiddleware());
        b.AccessGate = new PermissionGate(audit, _auth);
        b.SetupState = tooOld;
        b.Audit = audit;
        b.Services.Add(_auth);
        b.Services.Add(_kap);
        b.Services.Add(_gateway);
        b.Services.Add(audit);
        b.Services.Add(accounts);
        b.Services.Add(roles);
        b.Services.Add(sessions);
        b.Services.Add(providers);
    }

    private void MapServer(ModuleRegistration m)
    {
        // Public on purpose: the handler accepts and then closes 4401/4403, which the page can read (it cannot read an
        // HTTP status from a refused upgrade, admin-protocol.md §12); the gate runs inside, at every message
        m.MapWebSocket("/kap", RouteAccess.Public, ws => _gateway!.HandleAsync(ws));
        m.MapGet("/status", RouteAccess.SignedIn, ctx =>
        {
            var s = _kap!.Session;
            var p = (WebPrincipal)ctx.User!;
            var visible = s is null ? default : KapGate.Visible(p, s, _auth!.Registry);
            return ValueTask.FromResult(HttpResponse.Json(new JsonObject
            {
                ["connected"] = s is not null,
                ["kiln"] = s?.Kiln, ["admin"] = s?.Admin, ["server"] = s?.Server?.DeepClone(), ["restart"] = s?.Restart?.DeepClone(),
                ["ops"] = s is null ? null : new JsonArray([.. visible.Ops.Select(o => (JsonNode)o)]),
                ["topics"] = s is null ? null : new JsonArray([.. visible.Topics.Select(t => (JsonNode)t)]),
            }));
        });
    }

    public Task StartAsync(WebAdminServices services, CancellationToken ct)
    {
        // timers, tasks and sockets start here, not in Configure: a start that fails before this leaves nothing running
        _auth!.Audit.Start();
        _auth.Sessions.Start();
        _kap!.Start();
        _log!.Info($"accounts: {_auth.Accounts.All.Count}, roles: {_auth.Roles.All.Count}, passwords: PBKDF2-SHA256 x{_auth.Hasher.Iterations} ({Pbkdf2.Engine} engine)");
        if (!_auth.OwnerExists)
        {
            _setupToken = _auth.NewSetupToken();
            _server!.Changed += LogSetupLink;
            _auth.Accounts.Changed += OwnerChanged;
            _auth.Audit.Record(AuditLog.Entry("auth.setup_link", null, null, true));
            LogSetupLink();
        }
        return Task.CompletedTask;
    }

    /// <summary>kiln.log gets the first-run block once the panel listens (DESIGN §3; 0.1.2: three lines, what to do, the
    /// link for this computer, and the console command for a server without a browser). The token works until the owner
    /// exists or the mod reloads, on this machine only (AuthService.SetupScope), unless settings.json remoteSetup is on.</summary>
    private void LogSetupLink()
    {
        if (_setupToken is not { } token || _auth!.OwnerExists) return;
        if (_server!.Endpoints.Count == 0) return;
        if (Interlocked.Exchange(ref _linkLogged, 2) == 2) return;
        foreach (var line in FirstStartBlock(token)) _log!.Warn(line);
    }

    /// <summary>The three lines of the first-run block for <paramref name="token"/>.</summary>
    internal IReadOnlyList<string> FirstStartBlock(string token)
    {
        var (links, _) = SetupLinks(token);
        var remote = _serverSettings?.Current.RemoteSetup == true;
        var hosted = _relay() is { Enabled: true, Host.Length: > 0 };
        return Setup.SetupTexts.FirstStart(links[0], remote, hosted);
    }

    /// <summary>The setup links for <paramref name="token"/>: the relay's name first when the setup may come from
    /// elsewhere (settings.json remoteSetup), then where the panel listens; and where they work.</summary>
    internal (IReadOnlyList<string> Links, string Where) SetupLinks(string token)
    {
        var links = new List<string>();
        var remote = _serverSettings?.Current.RemoteSetup == true;
        if (remote && _relay() is { Enabled: true, Host.Length: > 0 } relay) links.Add($"https://{relay.Host}/setup#token={token}");
        links.AddRange(_server!.Endpoints.Select(e => new Uri(e, "/setup") + "#token=" + token));
        return (links, remote ? "from any machine (settings.json remoteSetup is on)" : "on this machine");
    }

    /// <summary>The console command's setup link (FirstRunSetup): a new one-use token while no owner exists, good for
    /// an hour (the one in kiln.log keeps working too).</summary>
    internal string? NewConsoleSetupLink(out string where)
    {
        where = "";
        if (_auth is null || _auth.OwnerExists || _server is null) return null;
        var token = _auth.NewSetupToken(TimeSpan.FromHours(1));
        _auth.Audit.Record(AuditLog.Entry("auth.setup_link", null, null, true, args: new JsonObject { ["via"] = "console" }));
        var (links, w) = SetupLinks(token);
        where = w;
        return string.Join("  or  ", links);
    }

    /// <summary>The console's link for the hosted setup (0.1.2): a one-use token that works over hosted access too, for
    /// 30 minutes, at https://<paramref name="host"/>/setup. Only made once the name has its trusted certificate (the
    /// setup component asks), and only shown in the console's answer, never in kiln.log.</summary>
    internal string? NewHostedSetupLink(string host)
    {
        if (_auth is null || _auth.OwnerExists) return null;
        var token = _auth.NewSetupToken(Setup.SetupComponent.HostedLinkLife, AuthService.SetupScope.Hosted);
        _auth.Audit.Record(AuditLog.Entry("auth.setup_link", null, null, true, args: new JsonObject { ["via"] = "console-hosted", ["host"] = host }));
        return $"https://{host}/setup#token={token}";
    }

    /// <summary>The console's first-run block with a new one-hour token (<c>kilnwarden setup</c>).</summary>
    internal IReadOnlyList<string>? NewConsoleBlock()
    {
        if (_auth is null || _auth.OwnerExists || _server is null) return null;
        var token = _auth.NewSetupToken(TimeSpan.FromHours(1));
        _auth.Audit.Record(AuditLog.Entry("auth.setup_link", null, null, true, args: new JsonObject { ["via"] = "console" }));
        return FirstStartBlock(token);
    }

    internal bool OwnerExists => _auth?.OwnerExists == true;

    private void OwnerChanged()
    {
        if (!_auth!.OwnerExists) return;
        _setupToken = null;
        _server!.Changed -= LogSetupLink;
        _auth.Accounts.Changed -= OwnerChanged;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_server is not null) _server.Changed -= LogSetupLink;
        if (_auth is not null) _auth.Accounts.Changed -= OwnerChanged;
        _gateway?.Dispose();
        if (_kap is not null) await _kap.StopAsync(ct).ConfigureAwait(false);
        if (_auth is not null)
        {
            _auth.Sessions.Dispose();
            await _auth.Audit.StopAsync(ct).ConfigureAwait(false);
            _auth.Hasher.Dispose();
        }
    }
}
