// SPDX-License-Identifier: MIT
using System.Diagnostics;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Hosting;
using Kilnwarden.Kap;
using Kilnwarden.Modules;

namespace Kilnwarden.Ops;

/// <summary>Runs due schedules (docs/step2-lane-b-ops.md "Schedules"): every few seconds, for each enabled schedule, the
/// newest moment that is due is handled once (run, skipped, missed or refused) and its <c>lastSlot</c> is saved before
/// anything is sent, so reloads, restarts and clock steps never run a moment twice. A run is checked as the schedule's
/// actor as that account is at run time (<see cref="Actors"/>); nothing of a sign-in is stored.</summary>
internal sealed class Scheduler(ScheduleStore store, SavedCommandStore saved, AuthService auth, KapClient kap, CommandRunner runner,
    ModuleRegistry registry, TimeProvider time, IWebAdminLog log) : IDisposable
{
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(ScheduleInput.GraceSeconds);

    private readonly CancellationTokenSource _life = new();
    private ITimer? _timer;
    private int _busy;
    private Task _tick = Task.CompletedTask;

    public void Start(TimeSpan period) => _timer ??= time.CreateTimer(_ => Tick(), null, period, period);

    private void Tick()
    {
        if (Interlocked.Exchange(ref _busy, 1) != 0) return;
        _tick = Task.Run(async () =>
        {
            try
            {
                await RunDueAsync(_life.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                log.Error("schedules: a tick failed", e);
            }
            finally
            {
                Volatile.Write(ref _busy, 0);
            }
        });
    }

    /// <summary>Handles every moment that is due now. Returns the records it made (tests).</summary>
    public async Task<IReadOnlyList<ScheduleRun>> RunDueAsync(CancellationToken ct)
    {
        var made = new List<ScheduleRun>();
        if (!registry.IsEnabled(ScheduleRoutes.ModuleId)) return made;
        foreach (var s in store.All)
        {
            ct.ThrowIfCancellationRequested();
            if (!s.Enabled) continue;
            var now = time.GetUtcNow();
            var tz = ScheduleClock.Zone(s.TimeZone, time) ?? TimeZoneInfo.Utc;
            var baseline = s.LastSlot ?? s.Updated;
            if (ScheduleClock.Next(s.When, tz, baseline) is not { } next) continue;
            var lead = TimeSpan.FromSeconds(s.LeadSeconds);
            if (next - lead > now) continue;
            var slot = ScheduleClock.Latest(s.When, tz, baseline, now + lead) ?? next;
            var late = now - (slot - lead);
            var skip = s.SkipSlot is { } k && k == slot;
            string decision = skip ? "skipped"
                : late <= Grace ? "run"
                : s.Missed == "catchUp" && s.Kind != "restart" && late <= TimeSpan.FromMinutes(s.CatchUpMinutes) ? "run"
                : "missed";
            // dedupe first: the moment is handled from here on, whatever happens to the run
            var claimed = false;
            var after = store.Update(s.Id, x =>
            {
                if (x.Updated != s.Updated || x.LastSlot != s.LastSlot || !x.Enabled) return;
                claimed = true;
                x.LastSlot = slot;
                if (x.SkipSlot is { } ks && ks <= slot) x.SkipSlot = null;
                if (x.When.Type == "once") x.Enabled = false;
            });
            if (after is null || !claimed) continue;
            if (slot != next)
                made.Add(Record(s, new ScheduleRun(now, s.Id, next, s.Kind, "missed", "missed",
                    $"moments from {next:O} to before {slot:O} were missed (the panel was not running)", null)));
            ScheduleRun run = decision switch
            {
                "skipped" => new ScheduleRun(now, s.Id, slot, s.Kind, "skipped", "skip_next", "skipped on request", null),
                "missed" => new ScheduleRun(now, s.Id, slot, s.Kind, "missed", "missed", $"{(int)late.TotalMinutes} min late: {(s.Missed == "catchUp" ? "past the catch-up window" : "missed runs are skipped")}", null),
                _ => await ExecuteAsync(s, slot, null, late > Grace, ct).ConfigureAwait(false),
            };
            made.Add(Record(s, run));
        }
        return made;
    }

    private ScheduleRun Record(Schedule s, ScheduleRun run)
    {
        store.Record(run);
        store.Update(s.Id, x => x.LastRun = run);
        return run;
    }

    /// <summary>Runs <paramref name="s"/> once: as its actor at run time (&lt;paramref name="live"/&gt; null: the scheduler;
    /// no proof is asked) or as a signed-in user's live principal ("run now": that user's own gate, proof included).</summary>
    public async Task<ScheduleRun> ExecuteAsync(Schedule s, DateTimeOffset? slot, WebPrincipal? live, bool catchUp, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var job = live is null;
        var p = live ?? Actors.Resolve(auth, s.Actor.Id);
        var by = p?.Name ?? s.Actor.Name;
        var audit = new JsonObject
        {
            ["schedule"] = s.Id, ["kind"] = s.Kind, ["slot"] = slot?.ToString("O"), ["trigger"] = job ? (catchUp ? "catch-up" : "schedule") : "manual",
        };
        ScheduleRun Done(string outcome, string? code, string? message)
        {
            var ok = outcome == "ok";
            if (outcome != "skipped")
                auth.Audit.Record(new Security.AuditEntry(time.GetUtcNow(), p?.Id ?? s.Actor.Id, by, "schedules.run", audit, ok, ok ? null : code, sw.Elapsed.TotalMilliseconds, null));
            return new ScheduleRun(time.GetUtcNow(), s.Id, slot, s.Kind, outcome, code, message, by);
        }

        if (p is null) return Done("refused", "actor_gone", $"the account '{s.Actor.Name}' that saved this schedule is gone or disabled: save it again");
        if (p.Restriction != Restriction.None) return Done("refused", "actor_restricted", $"{p.Name} must finish their account setup (password or two-factor) first");
        if (!p.Has(ScheduleRoutes.Manage)) return Done("refused", "actor_unauthorized", $"{p.Name} no longer has {ScheduleRoutes.Manage}");

        string op;
        JsonObject args;
        string? line = null;
        switch (s.Kind)
        {
            case "restart":
                op = "server_restart";
                args = new JsonObject { ["seconds"] = OpsHttp.Long(s.Params, "countdownSeconds") ?? 300 };
                if (OpsHttp.Str(s.Params, "message") is { } msg) args["message"] = msg;
                var support = kap.Session?.Restart is JsonObject r ? KapFrames.Str(r, "supported") : null;
                if (support != "supported" && OpsHttp.Bool(s.Params, "allowStopOnly") != true)
                    return Done("refused", "restart_unsupported",
                        $"restart support is '{support ?? "unknown"}': nothing known brings the server back; allow a stop in the schedule to run it anyway");
                break;
            case "backup":
                op = "backup_create";
                args = new JsonObject();
                if (OpsHttp.Str(s.Params, "name") is { } prefix) args["name"] = $"{prefix}-{time.GetUtcNow():yyyyMMdd-HHmm}";
                break;
            case "announce":
                op = "broadcast";
                args = new JsonObject { ["text"] = OpsHttp.Str(s.Params, "text") ?? "" };
                break;
            case "command":
            {
                op = "command_run";
                var sc = saved.Find(OpsHttp.Str(s.Params, "savedCommandId"));
                if (sc is null) return Done("refused", "saved_command_gone", "its saved command was removed: pick another one");
                if (sc.Revision != (OpsHttp.Long(s.Params, "revision") ?? -1) || sc.Line != OpsHttp.Str(s.Params, "line"))
                    return Done("refused", "saved_command_changed", $"the saved command '{sc.Name}' changed since this schedule was saved: save the schedule again");
                if (!SavedCommandRoutes.Sees(sc, p)) return Done("refused", "forbidden", $"{p.Name} can no longer see the saved command '{sc.Name}'");
                line = sc.Line;
                args = new JsonObject { ["line"] = line };
                foreach (var (k, v) in KapGateway.Redact("command_run", args)) audit[k] = v?.DeepClone();
                break;
            }
            default:
                return Done("refused", "bad_kind", $"unknown kind {s.Kind}");
        }

        KapCaller? caller;
        if (line is not null)
        {
            var (c, code, message) = await runner.CheckAsync(p, line, !job, ct).ConfigureAwait(false);
            if (c is null) return Done("refused", code, message);
            caller = c;
            audit["level"] = CommandLevels.Name(c.Level);
        }
        else
        {
            var d = KapGate.CheckOp(p, op, args, registry, auth.Now, requireRecent: !job);
            if (!d.Allowed) return Done("refused", d.Code, d.Message);
            caller = p.Caller();
        }

        if (job && s.SkipWhenEmpty)
        {
            var pl = await kap.CallAsync(KapCaller.Panel, "players", null, ct).ConfigureAwait(false);
            int? online = pl.Ok ? pl.Data switch
            {
                JsonArray a => a.Count,
                JsonObject o when o["players"] is JsonArray a2 => a2.Count,
                _ => null,
            } : null;
            if (online == 0) return Done("skipped", "empty", "nobody is online");
            if (online is null) audit["playersUnknown"] = true;
        }

        var result = await kap.CallAsync(caller, op, args, ct).ConfigureAwait(false);
        if (line is not null) KapGateway.AddCommandOutcome(audit, result);
        return result.Ok
            ? Done("ok", null, s.Kind == "restart" ? "the countdown started" : null)
            : Done("failed", result.Code, result.Error);
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _life.Cancel();
        try
        {
            _tick.Wait(TimeSpan.FromSeconds(1));
        }
        catch (Exception)
        {
            // cancelled or failed: logged by the tick
        }
        _life.Dispose();
    }
}
