// SPDX-License-Identifier: MIT
using Kilnwarden.Auth;
using Kilnwarden.Hosting;
using Kilnwarden.Kap;

namespace Kilnwarden.Ops;

/// <summary>Test hooks (internal): a clock, no background timer, the zone lookup, the downloader, the incoming folder.</summary>
internal sealed class OpsOptions
{
    public TimeProvider? Time { get; init; }

    /// <summary>False: the scheduler only runs when a test calls <see cref="Scheduler.RunDueAsync"/>.</summary>
    public bool Timer { get; init; } = true;

    public IPackageDownloader? Downloader { get; init; }
    public Func<string>? Incoming { get; init; }
}

/// <summary>Step 2 lane B as one host component: schedules, saved commands and mod updates (docs/step2-lane-b-ops.md).
/// Goes after the auth component (it uses its <see cref="AuthService"/>, <see cref="KapClient"/> and audit log).
/// Shared services for other components: <see cref="ScheduleStore"/>, <see cref="SavedCommandStore"/>.</summary>
public sealed class OpsComponent : IWebAdminComponent
{
    public static readonly TimeSpan TickPeriod = TimeSpan.FromSeconds(5);

    private readonly OpsOptions _o;
    private Scheduler? _scheduler;
    private ScheduleStore? _schedules;
    private PlanStore? _plans;
    private HttpPackageDownloader? _ownDownloader;
    private ITimer? _sweep;
    private string? _incoming;
    private TimeProvider _time = TimeProvider.System;

    public OpsComponent() : this(null)
    {
    }

    internal OpsComponent(OpsOptions? options)
    {
        _o = options ?? new OpsOptions();
    }

    public string Name => "ops";

    internal Scheduler Scheduler => _scheduler ?? throw new InvalidOperationException("not configured");

    public void Configure(WebAdminBuilder b)
    {
        var auth = b.Services.Get<AuthService>();
        var kap = b.Services.Get<KapClient>();
        _time = _o.Time ?? b.Environment.Time;
        var time = _time;
        var rules = new CommandRules(kap, time);
        var runner = new CommandRunner(kap, rules, b.Modules, () => auth.Now);

        var saved = new SavedCommandStore(b.Data, b.Log);
        var savedMod = b.Modules.Register(SavedCommandRoutes.Spec(), Product.ModId);
        SavedCommandRoutes.Map(savedMod, saved, auth, runner, time);

        _schedules = new ScheduleStore(b.Data, b.Log);
        // runs as panel events (webhooks): one that worked, and one that failed or was refused (skips are not news)
        var events = Events.PanelEvents.Of(b.Services, b.Log);
        events.Declare(new Events.EventKind("schedule.ran", "A schedule ran", ["schedule", "kind", "by"], "schedules"));
        events.Declare(new Events.EventKind("schedule.failed", "A schedule failed, was refused or missed its time", ["schedule", "kind", "outcome", "code", "message"], "schedules"));
        var store = _schedules;
        _schedules.Recorded += run =>
        {
            var name = store.Find(run.Schedule)?.Name ?? run.Schedule;
            if (run.Outcome == "ok")
                events.Publish("schedule.ran", new System.Text.Json.Nodes.JsonObject { ["schedule"] = name, ["kind"] = run.Kind, ["by"] = run.By }, run.At);
            else if (run.Outcome is "failed" or "refused" or "missed")
                events.Publish("schedule.failed", new System.Text.Json.Nodes.JsonObject
                {
                    ["schedule"] = name, ["kind"] = run.Kind, ["outcome"] = run.Outcome, ["code"] = run.Code,
                    ["message"] = run.Message is { Length: > 200 } m ? m[..200] : run.Message,
                }, run.At);
        };
        _scheduler = new Scheduler(_schedules, saved, auth, kap, runner, b.Modules, time, b.Log);
        var schedMod = b.Modules.Register(ScheduleRoutes.Spec(), Product.ModId);
        ScheduleRoutes.Map(schedMod, _schedules, saved, _scheduler, auth, kap, runner, time);

        var settings = b.SettingsFile<UpdateSettings>("updates/updates.json", UpdateSettings.Validate);
        _plans = new PlanStore(b.Data, time);
        var downloader = _o.Downloader ?? (_ownDownloader = new HttpPackageDownloader());
        _incoming = _o.Incoming?.Invoke() ?? Path.Combine(Path.GetFullPath(b.Environment.ServerDirectory), "kiln", "staged", "incoming");
        var updMod = b.Modules.Register(UpdateRoutes.Spec(), Product.ModId);
        UpdateRoutes.Map(updMod, new UpdateRoutes.Deps(_plans, settings, b.Settings, auth, kap, downloader, b.Environment, time, b.Log, _o.Incoming));

        b.Services.Add(_schedules);
        b.Services.Add(saved);
    }

    public Task StartAsync(WebAdminServices services, CancellationToken ct)
    {
        _plans!.Reset();
        UpdateRoutes.SweepIncoming(_incoming!, _time);
        _sweep = _time.CreateTimer(_ =>
        {
            _plans.Expire();
            UpdateRoutes.SweepIncoming(_incoming!, _time);
        }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        if (_o.Timer) _scheduler!.Start(TickPeriod);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _sweep?.Dispose();
        _scheduler?.Dispose();
        _schedules?.Dispose();
        _ownDownloader?.Dispose();
        return Task.CompletedTask;
    }
}
