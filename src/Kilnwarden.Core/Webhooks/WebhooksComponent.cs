// SPDX-License-Identifier: MIT
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kilnwarden.Branding;
using Kilnwarden.Config;
using Kilnwarden.Events;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Kap;
using Kilnwarden.Modules;
using Kilnwarden.Routing;
using Kilnwarden.Security;

namespace Kilnwarden.Webhooks;

/// <summary>Discord and generic JSON webhooks (DESIGN §5, lane C; contract docs/step2-lane-c.md C3). Opt-in: nothing is
/// sent until the owner adds a hook. Event sources here: server.online/offline (the KAP connection), webhook.test,
/// admin.action (a tap on the panel's one audit log, so sign-ins, user changes, KAP-socket ops and every lane's writes all
/// reach it), and whatever other components and extensions declare on &lt;see cref="PanelEvents"/&gt; (player.join, leave
/// and death come from Player activity, metrics.alert from Metrics, schedule.* from Schedules ...).</summary>
public sealed class WebhooksComponent : IWebAdminComponent
{
    public const string ModuleId = "webhooks", View = "webhooks.view", Edit = "webhooks.edit";
    public const string SettingsFileName = "webhook-settings.json";

    /// <summary>What the panel shows (and logs) while the private-target opt-in is on.</summary>
    public const string PrivateTargetsWarning =
        "Webhooks may reach private LAN addresses (10/8, 172.16/12, 192.168/16, fc00::/7), over plain http:// too. " +
        "Anyone who can add a webhook can make this server send requests into your network. " +
        "A plain http:// webhook sends its URL and payload unencrypted across your LAN. Loopback, link-local and cloud metadata stay blocked.";

    private SettingsStore<WebhookSettings>? _settings;

    private readonly IWebhookTransport? _transport;
    private WebhookStore? _store;
    private WebhookDispatcher? _dispatcher;
    private PanelEvents? _events;
    private WebAdminBuilder? _b;
    private KapClient? _kap;
    private IDisposable? _ownTransport;
    private IDisposable? _auditTap;

    public WebhooksComponent() : this(null)
    {
    }

    /// <summary>Tests: a fake sender.</summary>
    internal WebhooksComponent(IWebhookTransport? transport) => _transport = transport;

    public string Name => "webhooks";
    internal WebhookDispatcher? Dispatcher => _dispatcher;
    internal WebhookStore? Store => _store;

    /// <summary>The owner's private-target opt-in (webhook-settings.json; off by default).</summary>
    internal bool AllowPrivateTargets => _settings?.Current.AllowPrivateTargets == true;

    public void Configure(WebAdminBuilder b)
    {
        _b = b;
        _store = new WebhookStore(b.Data);
        _events = PanelEvents.Of(b.Services, b.Log);
        _events.Declare(new EventKind("server.online", "The panel reached the server (Kiln.Admin)", [], "webhooks"));
        _events.Declare(new EventKind("server.offline", "The panel lost the server (Kiln.Admin)", ["reason"], "webhooks"));
        _events.Declare(new EventKind("webhook.test", "A test from the panel", ["by"], "webhooks"));
        _events.Declare(new EventKind(WebhookAuditTap.Topic, "An admin action", ["user", "op"], "webhooks"));
        // admin actions: a tap on the shared audit log (auth's), which every component records to, directly or as the
        // IAuditSink service; without the auth component (engine tests), in front of whatever sink is installed
        var tap = new WebhookAuditTap(_events);
        if (b.Services.TryGet<Auth.AuditLog>() is { } auditLog) _auditTap = auditLog.Tap(tap.Observe);
        else b.Audit = new WebhookAuditTap.Forwarder(b.Audit, tap);
        _settings = b.SettingsFile<WebhookSettings>(SettingsFileName);
        if (AllowPrivateTargets) b.Log.Warn($"webhooks: private targets are ON (the owner's choice in {SettingsFileName}). {PrivateTargetsWarning}");
        _settings.Changed += (before, after) =>
        {
            if (before.AllowPrivateTargets == after.AllowPrivateTargets) return;
            if (after.AllowPrivateTargets) b.Log.Warn($"webhooks: private targets turned ON. {PrivateTargetsWarning}");
            else b.Log.Info("webhooks: private targets turned off (public internet addresses only)");
        };
        var transport = _transport;
        if (transport is null)
        {
            var http = new HttpWebhookTransport(allowPrivate: () => AllowPrivateTargets);
            transport = http;
            _ownTransport = http;
        }
        _dispatcher = new WebhookDispatcher(transport, b.Environment.Time, b.Log, ServerName, () => AllowPrivateTargets);
        _events.Published += OnEvent;

        var m = b.Modules.Register(new ModuleSpec
        {
            Id = ModuleId, Title = "Webhooks", Icon = "webhook",
            Description = "Send chosen events to Discord or any HTTPS endpoint",
            Permissions =
            [
                new PermissionSpec(View, "See webhooks (never their full URLs) and their deliveries") { DefaultRole = BuiltinRole.Admin },
                new PermissionSpec(Edit, "Add, change, test and remove webhooks") { DefaultRole = BuiltinRole.Owner, Sensitive = true },
            ],
            Pages = [new PageSpec("webhooks", "Webhooks", PageKind.Builtin, "webhooks") { Icon = "webhook", Permission = View, Order = 930 }],
        }, Product.ModId);
        m.MapGet("/hooks", RouteAccess.Requires(View), _ => Ok(new JsonObject { ["hooks"] = new JsonArray([.. _store.All.Select(h => (JsonNode)Public(h))]) }));
        m.MapGet("/events", RouteAccess.Requires(View), _ => Ok(new JsonObject
        {
            ["events"] = new JsonArray([.. _events.Kinds.Select(k => (JsonNode)new JsonObject
            {
                ["topic"] = k.Topic, ["title"] = k.Title, ["fields"] = new JsonArray([.. k.Fields.Select(f => (JsonNode)f)]),
            })]),
        }));
        m.MapGet("/deliveries", RouteAccess.Requires(View), _ => Ok(new JsonObject { ["deliveries"] = _dispatcher.Deliveries() }));
        // the private-target opt-in: anyone who sees webhooks sees whether it is on (and the warning); only an owner with
        // a recent sign-in changes it, audited either way
        m.MapGet("/settings", RouteAccess.Requires(View), ctx => Ok(new JsonObject
        {
            ["allowPrivateTargets"] = AllowPrivateTargets,
            ["canChange"] = ctx.User is Auth.WebPrincipal { IsOwner: true },
            ["warning"] = AllowPrivateTargets ? PrivateTargetsWarning : null,
        }));
        m.MapPut("/settings", RouteAccess.Requires(View), ctx =>
        {
            var p = ctx.User as Auth.WebPrincipal ?? throw new HttpError(401, "sign in first");
            var audit = ctx.Services.TryGet<IAuditSink>();
            void Record(bool ok, string? error, bool? value) =>
                audit?.Record(new AuditEntry(DateTimeOffset.UtcNow, p.Id, p.Name, "webhooks.private_targets",
                    value is null ? null : new JsonObject { ["allowPrivateTargets"] = value }, ok, error, 0, ctx.ClientAddress));
            var body = ctx.Request.Body.IsEmpty ? new JsonObject() : ctx.ReadJson<JsonObject>();
            bool? want = body["allowPrivateTargets"] is JsonValue v && v.GetValueKind() is JsonValueKind.True or JsonValueKind.False ? v.GetValue<bool>() : null;
            if (!p.IsOwner)
            {
                Record(false, "owner only", want);
                return ValueTask.FromResult(HttpResponse.Error(403, "forbidden", "only an owner can change where webhooks may go"));
            }
            if (ctx.Services.TryGet<Auth.AuthService>() is not { } auth || !auth.RecentlyAuthenticated(p))
            {
                Record(false, "reauth_required", want);
                return ValueTask.FromResult(HttpResponse.Error(403, "reauth_required", "re-authenticate with your password or enrolled two-factor code, then retry"));
            }
            if (want is not { } on) throw new HttpError(422, "allowPrivateTargets: true or false");
            _settings.Save(new WebhookSettings { AllowPrivateTargets = on });
            Record(true, null, on);
            return Ok(new JsonObject { ["ok"] = true, ["allowPrivateTargets"] = on, ["warning"] = on ? PrivateTargetsWarning : null });
        });
        m.MapPost("/hooks", RouteAccess.Requires(Edit), ctx =>
        {
            var body = ctx.ReadJson<HookBody>();
            var h = new WebhookHook
            {
                Id = WebhookStore.NewId(), Name = body.Name ?? "", Kind = body.Kind ?? "discord", Url = body.Url ?? "", Events = body.Events ?? [],
                Enabled = body.Enabled ?? true, Template = body.Template,
            };
            Validate(h);
            Change(ctx, l => [.. l, h]);
            Audit(ctx, "webhooks.add", h);
            return Ok(new JsonObject { ["ok"] = true, ["hook"] = Public(h) }, 201);
        });
        m.MapPut("/hooks/{id}", RouteAccess.Requires(Edit), ctx =>
        {
            var cur = _store.Find(ctx.RouteValue("id")) ?? throw new HttpError(404, "no such webhook");
            var body = ctx.ReadJson<HookBody>();
            var h = new WebhookHook
            {
                Id = cur.Id, Name = body.Name ?? cur.Name, Kind = body.Kind ?? cur.Kind, Url = body.Url ?? cur.Url, Events = body.Events ?? cur.Events,
                Enabled = body.Enabled ?? cur.Enabled, Template = body.Template ?? cur.Template,
            };
            // a new kind needs the matching kind of URL: keeping a Discord URL for a json hook (or back) must be said
            if (body.Kind is not null && body.Kind != cur.Kind && body.Url is null) throw new HttpError(422, "url: give the URL again when the kind changes");
            Validate(h);
            Change(ctx, l => [.. l.Select(x => x.Id == h.Id ? h : x)]);
            Audit(ctx, "webhooks.edit", h);
            return Ok(new JsonObject { ["ok"] = true, ["hook"] = Public(h) });
        });
        m.MapDelete("/hooks/{id}", RouteAccess.Requires(Edit), ctx =>
        {
            var cur = _store.Find(ctx.RouteValue("id")) ?? throw new HttpError(404, "no such webhook");
            Change(ctx, l => [.. l.Where(x => x.Id != cur.Id)]);
            Audit(ctx, "webhooks.remove", cur);
            return Ok(new JsonObject { ["ok"] = true });
        });
        m.MapPost("/hooks/{id}/test", RouteAccess.Requires(Edit), ctx =>
        {
            var cur = _store.Find(ctx.RouteValue("id")) ?? throw new HttpError(404, "no such webhook");
            var kind = _events.Find("webhook.test")!;
            _dispatcher.Enqueue(cur, new PanelEvent("webhook.test", b.Environment.Time.GetUtcNow(), new JsonObject { ["by"] = ctx.User!.Name }), kind);
            Audit(ctx, "webhooks.test", cur);
            return Ok(new JsonObject { ["ok"] = true, ["queued"] = true }, 202);
        });
    }

    private static ValueTask<HttpResponse> Ok(JsonObject o, int status = 200) => ValueTask.FromResult(HttpResponse.Json(o, status));

    private void Validate(WebhookHook h)
    {
        if (WebhookStore.Check(h, _events!.Kinds, AllowPrivateTargets) is { Count: > 0 } p) throw new HttpError(422, string.Join("; ", p));
    }

    private void Change(RequestContext ctx, Func<List<WebhookHook>, List<WebhookHook>> f)
    {
        try
        {
            _store!.Save(f);
        }
        catch (ArgumentException e)
        {
            throw new HttpError(422, e.Message);
        }
        _ = ctx;
    }

    private static void Audit(RequestContext ctx, string op, WebhookHook h) =>
        ctx.Services.TryGet<IAuditSink>()?.Record(new AuditEntry(DateTimeOffset.UtcNow, ctx.User!.Id, ctx.User.Name, op,
            new JsonObject { ["id"] = h.Id, ["name"] = h.Name, ["kind"] = h.Kind, ["url"] = WebhookGuard.Mask(h.Url) }, true, null, 0, ctx.ClientAddress));

    /// <summary>A hook as answers show it: the URL masked.</summary>
    public static JsonObject Public(WebhookHook h) => new()
    {
        ["id"] = h.Id, ["name"] = h.Name, ["kind"] = h.Kind, ["url"] = WebhookGuard.Mask(h.Url), ["enabled"] = h.Enabled,
        ["events"] = new JsonArray([.. h.Events.Select(e => (JsonNode)e)]), ["template"] = h.Template,
    };

    private sealed class HookBody
    {
        public string? Name { get; set; }
        public string? Kind { get; set; }
        public string? Url { get; set; }
        public List<string>? Events { get; set; }
        public bool? Enabled { get; set; }
        public string? Template { get; set; }
    }

    private string ServerName() =>
        _b?.Services.TryGet<ServerDisplayName>()?.Current ?? Product.Name;

    private void OnEvent(PanelEvent e, EventKind kind)
    {
        foreach (var h in _store!.All)
            if (h.Enabled && h.Wants(e.Topic)) _dispatcher!.Enqueue(h, e, kind);
    }

    public Task StartAsync(WebAdminServices services, CancellationToken ct)
    {
        _dispatcher!.Start();
        _kap = services.TryGet<KapClient>();
        if (_kap is not null)
        {
            _kap.Connected += OnConnected;
            _kap.Disconnected += OnDisconnected;
        }
        return Task.CompletedTask;
    }

    private void OnConnected(KapSession _) => _events!.Publish("server.online", null);

    private void OnDisconnected(string reason) => _events!.Publish("server.offline", new JsonObject { ["reason"] = reason.Length > 64 ? reason[..64] : reason });

    public async Task StopAsync(CancellationToken ct)
    {
        _auditTap?.Dispose();
        _auditTap = null;
        if (_kap is not null)
        {
            _kap.Connected -= OnConnected;
            _kap.Disconnected -= OnDisconnected;
        }
        if (_events is not null) _events.Published -= OnEvent;
        if (_dispatcher is not null) await _dispatcher.DisposeAsync().ConfigureAwait(false);
        _ownTransport?.Dispose();
    }
}

/// <summary>The admin.action feed: every successful audited action (sign-ins and sign-outs, account and role changes,
/// KAP-socket writes, every lane's and extension's writes) becomes the panel event "admin.action" {user, op}, for webhooks.
/// Only the account name and the op name leave: never arguments, addresses or errors (they may hold player data or
/// secrets). Refused and failed calls are not sent (a password guesser would otherwise fill the hooks); webhook edits
/// themselves are not either (no feedback loop through a hook's own test).</summary>
public sealed class WebhookAuditTap(PanelEvents events)
{
    public const string Topic = "admin.action";

    public void Observe(AuditEntry entry)
    {
        if (!entry.Ok || entry.Op.StartsWith("webhooks.", StringComparison.Ordinal)) return;
        events.Publish(Topic, new JsonObject { ["user"] = entry.UserName, ["op"] = entry.Op }, entry.At);
    }

    /// <summary>Without the shared audit log (engine tests without the auth component): in front of the installed sink.</summary>
    public sealed class Forwarder(IAuditSink inner, WebhookAuditTap tap) : IAuditSink
    {
        public IAuditSink Inner { get; } = inner;

        public void Record(AuditEntry entry)
        {
            Inner.Record(entry);
            tap.Observe(entry);
        }
    }
}

/// <summary>webhook-settings.json: the owner's choices for webhooks. &lt;see cref="AllowPrivateTargets"/&gt; is a security
/// trade-off (off by default): it lets webhooks reach private LAN addresses (RFC 1918, IPv6 ULA), plain http:// to those
/// included; loopback, link-local, multicast, unspecified and broadcast stay refused. Changed through the panel by an
/// owner with a recent sign-in (audited), or by editing the file.</summary>
public sealed class WebhookSettings
{
    public int Format { get; set; } = 1;
    public bool AllowPrivateTargets { get; set; }
}
