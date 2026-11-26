// SPDX-License-Identifier: MIT
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Modules;
using Kilnwarden.Routing;

namespace Kilnwarden.Tls.Acme;

/// <summary>Local ACME certificates (step 2 lane A): acme.json, the manager the listener wraps in tls.mode acme, and
/// /api/acme (status; renew now, owner-only and step-up).</summary>
public sealed class AcmeComponent : IWebAdminComponent
{
    public const string ModuleId = "acme", View = "acme.view", Renew = "acme.renew";

    private readonly AcmeOptions _o;
    private AcmeManager? _m;

    public AcmeComponent() : this(null)
    {
    }

    internal AcmeComponent(AcmeOptions? options)
    {
        _o = options ?? new AcmeOptions();
    }

    public string Name => "acme";

    public void Configure(WebAdminBuilder b)
    {
        var settings = b.SettingsFile<AcmeSettings>("acme.json", AcmeSettings.Validate);
        Config.EditableSettings.Of(b.Services).Add(Config.EditableFile.For("acme", "Let's Encrypt certificate", settings));
        b.Services.Add(settings); // the setup wizard writes acme.json for hosted access and an own address (0.1.2)
        var opts = _o.Time is null ? new AcmeOptions
        {
            Time = b.Environment.Time, Http = _o.Http, Polling = _o.Polling, FirstCheck = _o.FirstCheck, Period = _o.Period, DefaultBind = _o.DefaultBind,
        } : _o;
        // an order needs the owner, or settings.json remoteSetup before it (the first-run setup on a trusted certificate)
        // the hosted setup from the server console (0.1.2) may order before the owner exists too
        _m = new AcmeManager(b.Data, settings, () => b.Settings.Current,
            () => Hosting.ISetupState.RemoteReachAllowed(b.SetupState, b.Settings.Current) || b.Services.TryGet<Setup.HostedSetupWindow>()?.Open == true, b.Log, opts,
            () => b.Services.TryGet<Config.SettingsStore<Relay.RelaySettings>>()?.Current,
            () => b.Services.TryGet<Relay.RelayStatus>()?.Describe().State == "connected");
        _services = b.Services;
        var reg = b.Modules.Register(new ModuleSpec
        {
            Id = ModuleId, Title = "Certificates (ACME)", Icon = "shield-check", CanDisable = false,
            Description = "Let's Encrypt (or another ACME CA) certificates for the panel, ordered and renewed in process (settings.json tls.mode acme)",
            Permissions =
            [
                new PermissionSpec(View, "See the certificate and its renewals") { DefaultRole = BuiltinRole.Admin },
                new PermissionSpec(Renew, "Renew the certificate now") { DefaultRole = BuiltinRole.Owner, Sensitive = true },
            ],
        }, Product.ModId);
        reg.MapGet("/status", RouteAccess.Requires(View), _ => ValueTask.FromResult(HttpResponse.Json(_m.Status())));
        reg.MapPost("/renew", RouteAccess.Requires(Renew), ctx =>
        {
            var why = _m.RenewNow();
            ctx.Services.TryGet<Security.IAuditSink>()?.Record(Auth.AuditLog.Entry("acme.renew", ctx.User as Auth.WebPrincipal, ctx.ClientAddress, why is null, why));
            return ValueTask.FromResult(why is null
                ? HttpResponse.Json(new System.Text.Json.Nodes.JsonObject { ["ok"] = true, ["started"] = true }, 202)
                : HttpResponse.Error(why.StartsWith("waiting", StringComparison.Ordinal) || why.Contains("budget") ? 429 : 409, "acme_blocked", why));
        });
        b.Services.Add<IAcmeCertificates>(_m);
        b.Services.Add(_m);
        b.Settings.Changed += (_, _) => _m.Kick();
    }

    private WebAdminServices? _services;
    private Relay.RelayStatus? _relay;

    public Task StartAsync(WebAdminServices services, CancellationToken ct)
    {
        _m!.Start();
        // a dns-01 order waits for the relay link (0.1.2): it starts as soon as the link is up, not at the next hourly check
        _relay = _services?.TryGet<Relay.RelayStatus>();
        if (_relay is not null) _relay.Connected += OnRelayConnected;
        return Task.CompletedTask;
    }

    private void OnRelayConnected() => _m?.Kick();

    public Task StopAsync(CancellationToken ct)
    {
        if (_relay is not null) _relay.Connected -= OnRelayConnected;
        _m?.Dispose();
        return Task.CompletedTask;
    }
}
