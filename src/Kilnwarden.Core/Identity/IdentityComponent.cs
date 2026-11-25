// SPDX-License-Identifier: MIT
using Kilnwarden.Auth;
using Kilnwarden.Config;
using Kilnwarden.Hosting;
using Kilnwarden.Modules;

namespace Kilnwarden.Identity;

/// <summary>Test hooks (internal): the clock, a fake Steam, the mock broker's key.</summary>
internal sealed class IdentityOptions
{
    public TimeProvider? Time { get; init; }
    public HttpMessageInvoker? SteamHttp { get; init; }
    public IReadOnlyList<BrokerKey>? BrokerKeys { get; init; }
}

/// <summary>What the identity routes share: settings, the providers, the pending intents and this panel's id.</summary>
public sealed class IdentityService
{
    private readonly SettingsStore<IdentitySettings> _settings;

    internal IdentityService(AuthService auth, SettingsStore<IdentitySettings> settings, SteamOpenId steam, DiscordBroker discord, string panelId, TimeProvider time)
    {
        Auth = auth;
        _settings = settings;
        Steam = steam;
        Discord = discord;
        PanelId = panelId;
        Intents = new LoginIntents(time);
    }

    public AuthService Auth { get; }
    public SteamOpenId Steam { get; }
    public DiscordBroker Discord { get; }
    public LoginIntents Intents { get; }

    /// <summary>This panel's id at the broker (random, kept in identity-state.json): assertions name it.</summary>
    public string PanelId { get; }

    public IdentitySettings Settings => _settings.Current;

    /// <summary>Steam when switched on; Discord when switched on and its broker's key is pinned in this build.</summary>
    public bool IsEnabled(string provider) => provider switch
    {
        "steam" => Settings.Steam.Enabled,
        "discord" => Settings.Discord.Enabled && Discord.HasKeyFor(Settings.Discord.Broker),
        _ => false,
    };

    public IEnumerable<ISignInProvider> Enabled() => new ISignInProvider[] { Discord, Steam }.Where(p => IsEnabled(p.Id));

    /// <summary>Kiln.Admin, for the Steam role map (the SteamID's permission groups); null without one.</summary>
    internal Kap.KapClient? Kap { get; set; }

    private IReadOnlyList<RoleMapping> MapOf(string provider) => provider switch
    {
        "discord" => Settings.Discord.RoleMap ?? [],
        "steam" => Settings.Steam.RoleMap ?? [],
        _ => [],
    };

    /// <summary>The roles of an account's synced lists that its provider's mapping still gives (AuthService.MappedRoles):
    /// a mapping removed, a provider switched off or unlinked takes the role away at once.</summary>
    internal IEnumerable<string> StillMapped(Account a) =>
        a.Synced.Where(kv => IsEnabled(kv.Key) && a.Links.ContainsKey(kv.Key))
            .SelectMany(kv => kv.Value.Where(r => MapOf(kv.Key).Any(m => m.Role == r)));

    /// <summary>Records what the provider's mapping gives the account linked to <paramref name="who"/> now, before a
    /// sign-in or right after a link. Discord: the assertion's guild roles. Steam: the SteamID's groups in Kiln's
    /// permissions.json (permissions_get as the panel; when Kiln.Admin cannot say, nothing changes).</summary>
    internal async Task SyncMappedRolesAsync(ExternalIdentity who, System.Net.IPAddress? ip, CancellationToken ct)
    {
        var map = MapOf(who.Provider);
        var account = Auth.Accounts.All.Where(x => x.Links.TryGetValue(who.Provider, out var s) && s == who.Subject).Take(2).ToList();
        if (account.Count != 1) return;
        if (map.Count == 0)
        {
            if (account[0].Synced.ContainsKey(who.Provider)) Auth.SyncRoles(account[0].Id, who.Provider, [], ip);
            return;
        }
        IReadOnlyList<string>? groups = who.Provider == "discord" ? who.Groups ?? [] : await SteamGroupsAsync(who.Subject, ct).ConfigureAwait(false);
        if (groups is null) return;
        var roles = map.Where(m => groups.Contains(m.From, StringComparer.Ordinal)).Select(m => m.Role).ToList();
        Auth.SyncRoles(account[0].Id, who.Provider, roles, ip);
    }

    private async Task<IReadOnlyList<string>?> SteamGroupsAsync(string steamId, CancellationToken ct)
    {
        if (Kap is not { IsReady: true } kap || kap.Session?.Ops.Contains("permissions_get") != true) return null;
        try
        {
            var r = await kap.CallAsync(Kilnwarden.Kap.KapCaller.Panel, "permissions_get", null, ct).ConfigureAwait(false);
            if (!r.Ok) return null;
            if (r.Data?["players"]?[steamId]?["groups"] is not System.Text.Json.Nodes.JsonArray arr) return [];
            return [.. arr.Select(g => g is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<string>(out var s) ? s : null).OfType<string>()];
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>Makes an intent; the provider URL the browser goes to and the binder for its cookie.</summary>
    public (string Url, string Binder) Start(string provider, IntentKind kind, string? session, string origin)
    {
        if (provider == "steam")
        {
            var (_, binder, intent) = Intents.Create(provider, kind, session, st => SteamOpenId.ReturnUrl(origin, st));
            return (SteamOpenId.RedirectUrl(origin, intent), binder);
        }
        else
        {
            var (_, binder, intent) = Intents.Create(provider, kind, session, st => DiscordBroker.ReturnUrl(origin, st));
            return (DiscordBroker.RedirectUrl(Settings.Discord, PanelId, intent), binder);
        }
    }

    /// <summary>Keeps the shared provider list (GET /api/auth/status, AuthService.SignInExternal) in step with the settings.</summary>
    internal void Sync()
    {
        foreach (var p in new ISignInProvider[] { Steam, Discord })
        {
            if (IsEnabled(p.Id))
            {
                if (!Auth.Providers.Has(p.Id)) Auth.Providers.Add(p);
            }
            else Auth.Providers.Remove(p.Id);
        }
    }
}

/// <summary>Sign-in with Steam and Discord (DESIGN §3, step 2 lane A). Comes after the auth component (it uses its
/// AuthService). Both providers are off by default; identity.json switches them on.</summary>
public sealed class IdentityComponent : IWebAdminComponent
{
    public const string ModuleId = "identity";
    public const string StateFile = "identity-state.json";

    private readonly IdentityOptions _o;
    private IdentityService? _svc;
    private SettingsStore<IdentitySettings>? _settings;
    private HttpMessageInvoker? _ownHttp;
    private IWebAdminLog? _log;

    public IdentityComponent() : this(null)
    {
    }

    internal IdentityComponent(IdentityOptions? options)
    {
        _o = options ?? new IdentityOptions();
    }

    public string Name => "identity";

    internal sealed class State
    {
        public string PanelId { get; set; } = "";
    }

    public void Configure(WebAdminBuilder b)
    {
        var auth = b.Services.Get<AuthService>();
        var time = _o.Time ?? b.Environment.Time;
        _log = b.Log;
        _settings = b.SettingsFile<IdentitySettings>("identity.json", IdentitySettings.Validate);
        Config.EditableSettings.Of(b.Services).Add(Config.EditableFile.For("identity", "Steam and Discord sign-in", _settings));
        var state = b.Data.ReadJson<State>(StateFile);
        if (state is not { PanelId.Length: 32 } || !state.PanelId.All(char.IsAsciiHexDigitLower))
        {
            state = new State { PanelId = Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)) };
            b.Data.WriteJson(StateFile, state);
        }
        var http = _o.SteamHttp ?? (_ownHttp = new HttpMessageInvoker(new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseCookies = false, UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(10),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5), MaxResponseHeadersLength = 16,
        }));
        _svc = new IdentityService(auth, _settings, new SteamOpenId(http, time), new DiscordBroker(time, _o.BrokerKeys), state.PanelId, time);
        var reg = b.Modules.Register(new ModuleSpec
        {
            Id = ModuleId, Title = "Steam and Discord sign-in", Icon = "key-round", CanDisable = false,
            Description = "Sign in with a linked Steam or Discord account (identity.json switches each on; off by default)",
        }, Product.ModId);
        IdentityRoutes.Map(reg, _svc);
        b.Services.Add(_svc);
    }

    public Task StartAsync(WebAdminServices services, CancellationToken ct)
    {
        var svc = _svc!;
        svc.Sync();
        svc.Kap = services.TryGet<Kap.KapClient>();
        svc.Auth.MappedRoles = svc.StillMapped;
        _settings!.Changed += OnChanged;
        if (svc.Settings.Discord.Enabled && !svc.Discord.HasKeyFor(svc.Settings.Discord.Broker))
            _log!.Warn($"identity.json: discord is on, but no key of the broker {svc.Settings.Discord.Broker} is pinned in this build; Discord sign-in stays off");
        return Task.CompletedTask;
    }

    private void OnChanged(IdentitySettings old, IdentitySettings next) => _svc?.Sync();

    public Task StopAsync(CancellationToken ct)
    {
        if (_settings is not null) _settings.Changed -= OnChanged;
        if (_svc is not null && _svc.Auth.MappedRoles == _svc.StillMapped) _svc.Auth.MappedRoles = null;
        _ownHttp?.Dispose();
        return Task.CompletedTask;
    }
}
