// SPDX-License-Identifier: MIT
using System.Text.Json;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Http;
using Kilnwarden.Kap;
using Kilnwarden.Modules;
using Kilnwarden.Routing;

namespace Kilnwarden.Ops;

/// <summary>The schedules module's routes (docs/step2-lane-b-ops.md "Schedules").</summary>
internal static class ScheduleRoutes
{
    public const string ModuleId = "schedules";
    public const string View = "schedules.view", Manage = "schedules.manage";

    public static ModuleSpec Spec() => new()
    {
        Id = ModuleId, Title = "Schedules", Icon = "calendar-clock",
        Description = "Restarts with a countdown, timed backups, announcements and saved commands, on a clock",
        Permissions =
        [
            new PermissionSpec(View, "See schedules and their runs") { DefaultRole = BuiltinRole.Moderator },
            new PermissionSpec(Manage, "Add, change, switch and remove schedules (each also needs the permission of what it runs)") { DefaultRole = BuiltinRole.Admin },
        ],
        Pages = [new PageSpec("schedules", "Schedules", PageKind.Builtin, "schedules") { Icon = "calendar-clock", Permission = View, Order = 85 }],
    };

    /// <summary>Kinds whose saving, enabling and running by hand need a recent proof.</summary>
    public static bool IsSensitive(string kind) => kind is "restart" or "command";

    public static void Map(ModuleRegistration m, ScheduleStore store, SavedCommandStore saved, Scheduler scheduler, AuthService auth, KapClient kap,
        CommandRunner runner, TimeProvider time)
    {
        m.MapGet("", RouteAccess.Requires(View), ctx =>
        {
            var now = time.GetUtcNow();
            return ValueTask.FromResult(HttpResponse.Json(new JsonObject
            {
                ["schedules"] = new JsonArray([.. store.All.Select(s => (JsonNode)Describe(s, saved, time, now))]),
                ["restart"] = kap.Session?.Restart?.DeepClone(),
                ["now"] = now.ToString("O"),
                ["timeZones"] = new JsonArray("UTC", "local"),
                ["localZone"] = time.LocalTimeZone.Id,
            }));
        });
        m.MapGet("/{id}/runs", RouteAccess.Requires(View), ctx =>
        {
            var s = store.Find(ctx.RouteValue("id"));
            return ValueTask.FromResult(s is null ? HttpResponse.NotFound() : HttpResponse.Json(store.Runs(s.Id, 100)));
        });
        m.MapPost("", RouteAccess.Requires(Manage), async ctx =>
        {
            var p = OpsHttp.User(ctx);
            var (s, refusal) = await PrepareAsync(ctx, p, saved, auth, runner, time).ConfigureAwait(false);
            if (refusal is not null) return refusal;
            Schedule added;
            try
            {
                added = store.Add(s!);
            }
            catch (InvalidOperationException e)
            {
                return OpsHttp.Fail(409, "limit", e.Message);
            }
            OpsHttp.Audit(auth.Audit, "schedules.create", p, ctx.ClientAddress, true, null, AuditArgs(added));
            return HttpResponse.Json(Describe(added, saved, time, time.GetUtcNow()), 201);
        });
        m.MapPut("/{id}", RouteAccess.Requires(Manage), async ctx =>
        {
            var p = OpsHttp.User(ctx);
            var cur = store.Find(ctx.RouteValue("id"));
            if (cur is null) return HttpResponse.NotFound();
            if (IsSensitive(cur.Kind) && !auth.RecentlyAuthenticated(p)) return Denied(auth, ctx, p, "schedules.update", cur);
            var (s, refusal) = await PrepareAsync(ctx, p, saved, auth, runner, time).ConfigureAwait(false);
            if (refusal is not null) return refusal;
            var next = store.Update(cur.Id, x =>
            {
                s!.Id = x.Id;
                s.Created = x.Created;
                s.LastRun = x.LastRun;
                CopyInto(s, x);
            });
            if (next is null) return HttpResponse.NotFound();
            OpsHttp.Audit(auth.Audit, "schedules.update", p, ctx.ClientAddress, true, null, AuditArgs(next));
            return HttpResponse.Json(Describe(next, saved, time, time.GetUtcNow()));
        });
        m.MapDelete("/{id}", RouteAccess.Requires(Manage), ctx =>
        {
            var p = OpsHttp.User(ctx);
            var cur = store.Find(ctx.RouteValue("id"));
            if (cur is null || !store.Remove(cur.Id)) return ValueTask.FromResult(HttpResponse.NotFound());
            OpsHttp.Audit(auth.Audit, "schedules.delete", p, ctx.ClientAddress, true, null, AuditArgs(cur));
            return ValueTask.FromResult(HttpResponse.Empty());
        });
        m.MapPost("/{id}/enable", RouteAccess.Requires(Manage), async ctx =>
        {
            var p = OpsHttp.User(ctx);
            var cur = store.Find(ctx.RouteValue("id"));
            if (cur is null) return HttpResponse.NotFound();
            if (OpsHttp.Bool(OpsHttp.Body(ctx), "enabled") is not { } on) throw new HttpError(400, "body: {\"enabled\": true|false}");
            if (on)
            {
                // enabling is a new approval: the enabler becomes the actor and must be allowed to run it now
                if (IsSensitive(cur.Kind) && !auth.RecentlyAuthenticated(p)) return Denied(auth, ctx, p, "schedules.enable", cur);
                if (await CheckActionAsync(p, cur, saved, auth, runner).ConfigureAwait(false) is { } no) return no;
            }
            var now = time.GetUtcNow();
            var next = store.Update(cur.Id, x =>
            {
                if (on && !x.Enabled)
                {
                    x.Actor = new ActorRef(p.Id, p.Name);
                    x.Updated = now;
                    x.LastSlot = null;
                    x.UpdatedBy = p.Name;
                }
                x.Enabled = on;
            });
            if (next is null) return HttpResponse.NotFound();
            OpsHttp.Audit(auth.Audit, on ? "schedules.enable" : "schedules.disable", p, ctx.ClientAddress, true, null, AuditArgs(next));
            return HttpResponse.Json(Describe(next, saved, time, now));
        });
        m.MapPost("/{id}/skip-next", RouteAccess.Requires(Manage), ctx =>
        {
            var p = OpsHttp.User(ctx);
            var cur = store.Find(ctx.RouteValue("id"));
            if (cur is null) return ValueTask.FromResult(HttpResponse.NotFound());
            if (OpsHttp.Bool(OpsHttp.Body(ctx), "skip") is not { } skip) throw new HttpError(400, "body: {\"skip\": true|false}");
            var now = time.GetUtcNow();
            var nextSlot = NextSlot(cur, time, now);
            if (skip && nextSlot is null) return ValueTask.FromResult(OpsHttp.Fail(409, "no_next", "this schedule has no next run"));
            var next = store.Update(cur.Id, x => x.SkipSlot = skip ? nextSlot : null);
            if (next is null) return ValueTask.FromResult(HttpResponse.NotFound());
            var a = AuditArgs(next);
            a["skipSlot"] = next.SkipSlot?.ToString("O");
            OpsHttp.Audit(auth.Audit, "schedules.skip_next", p, ctx.ClientAddress, true, null, a);
            return ValueTask.FromResult(HttpResponse.Json(Describe(next, saved, time, now)));
        });
        m.MapPost("/{id}/run", RouteAccess.Requires(Manage), async ctx =>
        {
            var p = OpsHttp.User(ctx);
            var cur = store.Find(ctx.RouteValue("id"));
            if (cur is null) return HttpResponse.NotFound();
            if (IsSensitive(cur.Kind) && !auth.RecentlyAuthenticated(p)) return Denied(auth, ctx, p, "schedules.run", cur);
            var run = await scheduler.ExecuteAsync(cur, null, p, false, ctx.Aborted).ConfigureAwait(false);
            store.Record(run);
            store.Update(cur.Id, x => x.LastRun = run);
            if (run.Code == "reauth_required") return OpsHttp.Reauth();
            return HttpResponse.Json(new JsonObject { ["run"] = JsonSerializer.SerializeToNode(run, HttpResponse.JsonOptions) });
        });
        m.MapPost("/restart/cancel", RouteAccess.Requires(Manage), async ctx =>
        {
            var p = OpsHttp.User(ctx);
            var d = KapGate.CheckOp(p, "server_restart_cancel", null, auth.Registry, auth.Now);
            if (!d.Allowed)
            {
                OpsHttp.Audit(auth.Audit, "schedules.restart_cancel", p, ctx.ClientAddress, false, d.Code, null);
                return OpsHttp.Refused(d);
            }
            var r = await kap.CallAsync(p.Caller(), "server_restart_cancel", null, ctx.Aborted).ConfigureAwait(false);
            OpsHttp.Audit(auth.Audit, "schedules.restart_cancel", p, ctx.ClientAddress, r.Ok, r.Ok ? null : r.Code, null);
            return r.Ok ? HttpResponse.Json(new JsonObject { ["ok"] = true, ["result"] = r.Data?.DeepClone() }) : OpsHttp.KapFailed(r);
        });
    }

    private static HttpResponse Denied(AuthService auth, RequestContext ctx, WebPrincipal p, string op, Schedule s)
    {
        OpsHttp.Audit(auth.Audit, op, p, ctx.ClientAddress, false, "reauth_required", AuditArgs(s));
        return OpsHttp.Reauth();
    }

    private static void CopyInto(Schedule from, Schedule to)
    {
        to.Name = from.Name;
        to.Enabled = from.Enabled;
        to.Kind = from.Kind;
        to.When = from.When;
        to.TimeZone = from.TimeZone;
        to.SkipWhenEmpty = from.SkipWhenEmpty;
        to.Missed = from.Missed;
        to.CatchUpMinutes = from.CatchUpMinutes;
        to.Params = from.Params;
        to.Actor = from.Actor;
        to.Updated = from.Updated;
        to.UpdatedBy = from.UpdatedBy;
        to.LastSlot = null;
        to.SkipSlot = null;
    }

    /// <summary>The body as a schedule the caller may save: checked fields, the proof for sensitive kinds, the caller's
    /// permission for what it runs, the saved command pinned. The caller becomes the actor.</summary>
    private static async Task<(Schedule? Schedule, HttpResponse? Refusal)> PrepareAsync(RequestContext ctx, WebPrincipal p, SavedCommandStore saved,
        AuthService auth, CommandRunner runner, TimeProvider time)
    {
        var s = ScheduleInput.Parse(OpsHttp.Body(ctx), time, out var problems);
        if (s is null) return (null, OpsHttp.Fail(422, "invalid", string.Join("; ", problems)));
        if (IsSensitive(s.Kind) && !auth.RecentlyAuthenticated(p))
        {
            OpsHttp.Audit(auth.Audit, "schedules.save", p, ctx.ClientAddress, false, "reauth_required", new JsonObject { ["kind"] = s.Kind });
            return (null, OpsHttp.Reauth());
        }
        if (s.Kind == "command")
        {
            var sc = saved.Find(OpsHttp.Str(s.Params, "savedCommandId"));
            if (sc is null || !SavedCommandRoutes.Sees(sc, p)) return (null, OpsHttp.Fail(422, "invalid", "params.savedCommandId: no such saved command"));
            s.Params["revision"] = sc.Revision;
            s.Params["line"] = sc.Line;
        }
        if (await CheckActionAsync(p, s, saved, auth, runner).ConfigureAwait(false) is { } no) return (null, no);
        var now = time.GetUtcNow();
        s.Actor = new ActorRef(p.Id, p.Name);
        s.Created = now;
        s.Updated = now;
        s.UpdatedBy = p.Name;
        return (s, null);
    }

    /// <summary>Whether <paramref name="p"/> may run what <paramref name="s"/> runs, right now (no proof: that is asked
    /// separately for sensitive kinds).</summary>
    private static async Task<HttpResponse?> CheckActionAsync(WebPrincipal p, Schedule s, SavedCommandStore saved, AuthService auth, CommandRunner runner)
    {
        if (s.Kind == "command")
        {
            var line = OpsHttp.Str(s.Params, "line") ?? saved.Find(OpsHttp.Str(s.Params, "savedCommandId"))?.Line ?? "";
            var (caller, code, message) = await runner.CheckAsync(p, line, false, CancellationToken.None).ConfigureAwait(false);
            return caller is null ? OpsHttp.Fail(403, code, message) : null;
        }
        var op = s.Kind switch { "restart" => "server_restart", "backup" => "backup_create", _ => "broadcast" };
        var d = KapGate.CheckOp(p, op, null, auth.Registry, auth.Now, requireRecent: false);
        return d.Allowed ? null : OpsHttp.Refused(d);
    }

    internal static DateTimeOffset? NextSlot(Schedule s, TimeProvider time, DateTimeOffset now)
    {
        var tz = ScheduleClock.Zone(s.TimeZone, time) ?? TimeZoneInfo.Utc;
        var baseline = s.LastSlot ?? s.Updated;
        // a moment whose countdown already started is still the next one
        var from = now - TimeSpan.FromSeconds(s.LeadSeconds);
        return ScheduleClock.Next(s.When, tz, baseline > from ? baseline : from);
    }

    private static JsonObject Describe(Schedule s, SavedCommandStore saved, TimeProvider time, DateTimeOffset now)
    {
        var o = JsonSerializer.SerializeToNode(s, HttpResponse.JsonOptions)!.AsObject();
        var next = s.Enabled ? NextSlot(s, time, now) : null;
        o["nextRun"] = next?.ToString("O");
        o["nextFire"] = next is { } n ? (n - TimeSpan.FromSeconds(s.LeadSeconds)).ToString("O") : null;
        if (s.Kind == "command")
        {
            // the line is shown only as its command; the saved command's own page has the rest
            var p = (JsonObject)o["params"]!;
            p.Remove("line");
            var sc = saved.Find(OpsHttp.Str(s.Params, "savedCommandId"));
            o["savedCommand"] = sc is null ? null : new JsonObject
            {
                ["id"] = sc.Id, ["name"] = sc.Name, ["command"] = sc.Command,
                ["changed"] = sc.Revision != (OpsHttp.Long(s.Params, "revision") ?? -1),
            };
        }
        return o;
    }

    private static JsonObject AuditArgs(Schedule s) => new()
    {
        ["schedule"] = s.Id, ["name"] = s.Name, ["kind"] = s.Kind, ["enabled"] = s.Enabled, ["when"] = s.When.Type,
    };
}
