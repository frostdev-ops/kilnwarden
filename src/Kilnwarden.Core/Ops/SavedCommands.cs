// SPDX-License-Identifier: MIT
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Kap;
using Kilnwarden.Modules;
using Kilnwarden.Routing;
using Kilnwarden.Storage;

namespace Kilnwarden.Ops;

/// <summary>A filled-in command form saved under a name (DESIGN §5a "Saved commands").</summary>
public sealed class SavedCommand
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>The line the form built (what command_run gets).</summary>
    public string Line { get; set; } = "";

    /// <summary>The line's first word, lower case, without the slash.</summary>
    public string Command { get; set; } = "";

    /// <summary>The web app's form state, to reopen the form (opaque to the server, at most 8 KiB).</summary>
    public JsonNode? Form { get; set; }

    /// <summary>Ask before running (the run needs confirmed: true).</summary>
    public bool Confirm { get; set; }

    /// <summary>Other users with commands.command_run see it too (they run it as themselves).</summary>
    public bool Shared { get; set; }

    public ActorRef Owner { get; set; } = new("", "");

    /// <summary>Grows with every edit; a schedule pins the revision it was saved with.</summary>
    public long Revision { get; set; } = 1;

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset Updated { get; set; }
}

/// <summary>saved-commands/saved.json and pins.json (per-user pins), written atomically under one lock.</summary>
public sealed class SavedCommandStore
{
    public const string File = "saved-commands/saved.json", PinsFile = "saved-commands/pins.json";
    public const int MaxTotal = 500, MaxPerUser = 50, MaxPins = 50, MaxFormBytes = 8 << 10, MaxLine = 1000;

    private readonly DataStore _data;
    private readonly object _gate = new();
    private List<SavedCommand> _all;
    private Dictionary<string, List<string>> _pins;

    public SavedCommandStore(DataStore data, IWebAdminLog log)
    {
        _data = data;
        _all = Load<SavedFile>(data, File, log)?.Commands ?? [];
        _pins = Load<PinFile>(data, PinsFile, log)?.Pins ?? [];
    }

    /// <summary>Raised after a change (schedules re-check the revisions they pinned at run time, not here).</summary>
    public event Action? Changed;

    public IReadOnlyList<SavedCommand> All
    {
        get
        {
            lock (_gate) return [.. _all];
        }
    }

    public SavedCommand? Find(string? id)
    {
        lock (_gate) return _all.FirstOrDefault(c => c.Id == id);
    }

    public IReadOnlyList<string> PinsOf(string accountId)
    {
        lock (_gate) return _pins.TryGetValue(accountId, out var l) ? [.. l] : [];
    }

    public SavedCommand Add(SavedCommand c)
    {
        lock (_gate)
        {
            if (_all.Count >= MaxTotal) throw new InvalidOperationException($"at most {MaxTotal} saved commands");
            if (_all.Count(x => x.Owner.Id == c.Owner.Id) >= MaxPerUser) throw new InvalidOperationException($"at most {MaxPerUser} saved commands per user");
            c.Id = "sc-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
            _all = [.. _all, c];
            Save();
        }
        Changed?.Invoke();
        return c;
    }

    public SavedCommand Update(string id, Action<SavedCommand> change)
    {
        SavedCommand next;
        lock (_gate)
        {
            var cur = _all.FirstOrDefault(c => c.Id == id) ?? throw new KeyNotFoundException(id);
            next = Clone(cur);
            change(next);
            next.Id = cur.Id;
            next.Owner = cur.Owner;
            next.Created = cur.Created;
            next.Revision = cur.Revision + 1;
            _all = [.. _all.Select(c => c.Id == id ? next : c)];
            Save();
        }
        Changed?.Invoke();
        return next;
    }

    public bool Remove(string id)
    {
        lock (_gate)
        {
            if (!_all.Any(c => c.Id == id)) return false;
            _all = [.. _all.Where(c => c.Id != id)];
            foreach (var l in _pins.Values) l.Remove(id);
            Save();
        }
        Changed?.Invoke();
        return true;
    }

    public bool SetPinned(string accountId, string id, bool pinned)
    {
        lock (_gate)
        {
            if (!_pins.TryGetValue(accountId, out var l)) _pins[accountId] = l = [];
            if (pinned && !l.Contains(id))
            {
                if (l.Count >= MaxPins) throw new InvalidOperationException($"at most {MaxPins} pins");
                l.Add(id);
            }
            else if (!pinned) l.Remove(id);
            if (l.Count == 0) _pins.Remove(accountId);
            _data.WriteJson(PinsFile, new PinFile { Pins = _pins });
            return pinned;
        }
    }

    private void Save() => _data.WriteJson(File, new SavedFile { Commands = _all });

    private static SavedCommand Clone(SavedCommand c) =>
        JsonSerializer.Deserialize<SavedCommand>(JsonSerializer.SerializeToUtf8Bytes(c, DataStore.Json), DataStore.Json)!;

    internal static T? Load<T>(DataStore data, string file, IWebAdminLog log) where T : class
    {
        try
        {
            return data.ReadJson<T>(file);
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            // keep the unreadable file for the owner to look at, start empty (the panel still serves)
            var path = data.PathOf(file);
            var aside = path + ".unreadable-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            try
            {
                System.IO.File.Move(path, aside);
            }
            catch (IOException)
            {
            }
            log.Error($"{file} is unreadable ({e.Message}); moved to {Path.GetFileName(aside)}, starting empty");
            return null;
        }
    }

    private sealed class SavedFile
    {
        public int Format { get; set; } = 1;
        public List<SavedCommand>? Commands { get; set; } = [];
    }

    private sealed class PinFile
    {
        public int Format { get; set; } = 1;
        public Dictionary<string, List<string>>? Pins { get; set; } = [];
    }
}

/// <summary>Runs a command line for a principal the same way the KAP gateway does: the gate (op permission, module
/// switch, Console-level proof unless &lt;paramref name="requireRecent"/&gt; is off for a scheduled job), the role's command
/// rules (which give the level cap), then command_run. Never throws for a refusal.</summary>
internal sealed class CommandRunner(KapClient kap, CommandRules rules, ModuleRegistry registry, Func<DateTimeOffset> now)
{
    public sealed record Outcome(bool Allowed, string Code, string Message, KapResult? Result, KapCaller? Caller);

    /// <summary>Whether &lt;paramref name="p"/&gt; may run &lt;paramref name="line"/&gt; now (no proof asked: saving and listing).</summary>
    public async Task<(KapCaller? Caller, string Code, string Message)> CheckAsync(WebPrincipal p, string line, bool requireRecent, CancellationToken ct)
    {
        var args = new JsonObject { ["line"] = line };
        var d = KapGate.CheckOp(p, "command_run", args, registry, now(), requireRecent);
        if (!d.Allowed) return (null, d.Code, d.Message);
        var (caller, refusal) = await rules.ForLineAsync(p, line, ct).ConfigureAwait(false);
        return caller is null ? (null, "forbidden", refusal ?? "not allowed") : (caller, "", "");
    }

    public async Task<Outcome> RunAsync(WebPrincipal p, string line, bool requireRecent, CancellationToken ct)
    {
        var (caller, code, message) = await CheckAsync(p, line, requireRecent, ct).ConfigureAwait(false);
        if (caller is null) return new Outcome(false, code, message, null, null);
        var r = await kap.CallAsync(caller, "command_run", new JsonObject { ["line"] = line }, ct).ConfigureAwait(false);
        return new Outcome(true, "", "", r, caller);
    }

    /// <summary>The audit record of a run: the gateway's command metadata (first word, argument count, level) and outcome.</summary>
    public static JsonObject Audited(string line, Outcome o)
    {
        var a = KapGateway.Redact("command_run", new JsonObject { ["line"] = line }, o.Caller);
        if (o.Result is { } r) KapGateway.AddCommandOutcome(a, r);
        return a;
    }
}

/// <summary>The saved-commands module's routes (docs/step2-lane-b-ops.md).</summary>
internal static class SavedCommandRoutes
{
    public const string ModuleId = "saved-commands";
    public const string View = "saved-commands.view", Manage = "saved-commands.manage", ManageAll = "saved-commands.manage_all";
    private const string RunPerm = "commands.command_run";

    public static ModuleSpec Spec() => new()
    {
        Id = ModuleId, Title = "Saved commands", Icon = "bookmark",
        Description = "Filled-in command forms saved under a name: run them again, pin them, use them in schedules",
        Permissions =
        [
            new PermissionSpec(View, "See and run saved commands (each run is checked as your own command)") { DefaultRole = BuiltinRole.Moderator },
            new PermissionSpec(Manage, "Save, change and share your own commands") { DefaultRole = BuiltinRole.Moderator },
            new PermissionSpec(ManageAll, "Change and remove other users' shared commands") { DefaultRole = BuiltinRole.Admin },
        ],
        Pages = [new PageSpec("saved-commands", "Saved commands", PageKind.Builtin, "saved-commands") { Icon = "bookmark", Permission = View, Order = 71 }],
        Widgets = [new WidgetSpec("pinned", "Pinned commands", WidgetSurface.Dashboard, "/api/saved-commands/pinned") { Permission = View, Kind = "buttons" }],
    };

    public static void Map(ModuleRegistration m, SavedCommandStore store, AuthService auth, CommandRunner runner, TimeProvider time)
    {
        m.MapGet("", RouteAccess.Requires(View), async ctx =>
        {
            var p = OpsHttp.User(ctx);
            var pins = store.PinsOf(p.Id).ToHashSet(StringComparer.Ordinal);
            var list = new JsonArray();
            foreach (var c in Visible(store, p)) list.Add(await DescribeAsync(c, p, runner, pins, ctx.Aborted).ConfigureAwait(false));
            return HttpResponse.Json(list);
        });
        m.MapGet("/pinned", RouteAccess.Requires(View), async ctx =>
        {
            var p = OpsHttp.User(ctx);
            var pins = store.PinsOf(p.Id);
            var visible = Visible(store, p).ToDictionary(c => c.Id, StringComparer.Ordinal);
            var list = new JsonArray();
            foreach (var id in pins)
                if (visible.TryGetValue(id, out var c)) list.Add(await DescribeAsync(c, p, runner, pins.ToHashSet(), ctx.Aborted).ConfigureAwait(false));
            return HttpResponse.Json(list);
        });
        m.MapPost("", RouteAccess.Requires(Manage), async ctx =>
        {
            var p = OpsHttp.User(ctx);
            var input = Parse(OpsHttp.Body(ctx), out var problem);
            if (input is null) return OpsHttp.Fail(422, "invalid", problem!);
            var (caller, code, message) = await runner.CheckAsync(p, input.Line, false, ctx.Aborted).ConfigureAwait(false);
            if (caller is null) return OpsHttp.Fail(403, code, message);
            var now = time.GetUtcNow();
            input.Owner = new ActorRef(p.Id, p.Name);
            input.Created = input.Updated = now;
            SavedCommand saved;
            try
            {
                saved = store.Add(input);
            }
            catch (InvalidOperationException e)
            {
                return OpsHttp.Fail(409, "limit", e.Message);
            }
            OpsHttp.Audit(auth.Audit, "saved-commands.create", p, ctx.ClientAddress, true, null, AuditArgs(saved));
            return HttpResponse.Json(await DescribeAsync(saved, p, runner, new HashSet<string>(), ctx.Aborted).ConfigureAwait(false), 201);
        });
        m.MapPut("/{id}", RouteAccess.Requires(Manage), async ctx =>
        {
            var p = OpsHttp.User(ctx);
            var cur = store.Find(ctx.RouteValue("id"));
            if (cur is null || !Sees(cur, p)) return HttpResponse.NotFound();
            if (!MayEdit(cur, p)) return OpsHttp.Fail(403, "forbidden", $"only its owner or a user with {ManageAll} may change it");
            var input = Parse(OpsHttp.Body(ctx), out var problem);
            if (input is null) return OpsHttp.Fail(422, "invalid", problem!);
            var (caller, code, message) = await runner.CheckAsync(p, input.Line, false, ctx.Aborted).ConfigureAwait(false);
            if (caller is null) return OpsHttp.Fail(403, code, message);
            var saved = store.Update(cur.Id, c =>
            {
                c.Name = input.Name;
                c.Description = input.Description;
                c.Line = input.Line;
                c.Command = input.Command;
                c.Form = input.Form;
                c.Confirm = input.Confirm;
                c.Shared = input.Shared;
                c.Updated = time.GetUtcNow();
            });
            OpsHttp.Audit(auth.Audit, "saved-commands.update", p, ctx.ClientAddress, true, null, AuditArgs(saved));
            return HttpResponse.Json(await DescribeAsync(saved, p, runner, store.PinsOf(p.Id).ToHashSet(), ctx.Aborted).ConfigureAwait(false));
        });
        m.MapDelete("/{id}", RouteAccess.Requires(Manage), ctx =>
        {
            var p = OpsHttp.User(ctx);
            var cur = store.Find(ctx.RouteValue("id"));
            if (cur is null || !Sees(cur, p)) return ValueTask.FromResult(HttpResponse.NotFound());
            if (!MayEdit(cur, p)) return ValueTask.FromResult(OpsHttp.Fail(403, "forbidden", $"only its owner or a user with {ManageAll} may remove it"));
            store.Remove(cur.Id);
            OpsHttp.Audit(auth.Audit, "saved-commands.delete", p, ctx.ClientAddress, true, null, AuditArgs(cur));
            return ValueTask.FromResult(HttpResponse.Empty());
        });
        m.MapPut("/{id}/pin", RouteAccess.Requires(View), ctx =>
        {
            var p = OpsHttp.User(ctx);
            var cur = store.Find(ctx.RouteValue("id"));
            if (cur is null || !Sees(cur, p)) return ValueTask.FromResult(HttpResponse.NotFound());
            if (OpsHttp.Bool(OpsHttp.Body(ctx), "pinned") is not { } on) throw new HttpError(400, "body: {\"pinned\": true|false}");
            try
            {
                return ValueTask.FromResult(HttpResponse.Json(new { pinned = store.SetPinned(p.Id, cur.Id, on) }));
            }
            catch (InvalidOperationException e)
            {
                return ValueTask.FromResult(OpsHttp.Fail(409, "limit", e.Message));
            }
        });
        m.MapPost("/{id}/run", RouteAccess.Requires(View), async ctx =>
        {
            var p = OpsHttp.User(ctx);
            var cur = store.Find(ctx.RouteValue("id"));
            if (cur is null || !Sees(cur, p)) return HttpResponse.NotFound();
            if (cur.Confirm && OpsHttp.Bool(OpsHttp.Body(ctx), "confirmed") != true)
                return OpsHttp.Fail(409, "confirm_required", "this saved command asks for a confirmation: send {\"confirmed\": true}");
            var sw = Stopwatch.StartNew();
            var o = await runner.RunAsync(p, cur.Line, true, ctx.Aborted).ConfigureAwait(false);
            var audited = CommandRunner.Audited(cur.Line, o);
            audited["saved"] = cur.Id;
            audited["revision"] = cur.Revision;
            var ok = o.Allowed && o.Result!.Ok;
            OpsHttp.Audit(auth.Audit, "saved-commands.run", p, ctx.ClientAddress, ok, ok ? null : o.Allowed ? o.Result!.Code : o.Code, audited,
                sw.Elapsed.TotalMilliseconds);
            if (!o.Allowed) return OpsHttp.Fail(403, o.Code, o.Message);
            if (!o.Result!.Ok) return OpsHttp.KapFailed(o.Result);
            return HttpResponse.Json(new JsonObject { ["ok"] = true, ["result"] = o.Result.Data?.DeepClone() });
        });
    }

    /// <summary>Own commands, plus shared ones for users who may run commands at all.</summary>
    internal static IEnumerable<SavedCommand> Visible(SavedCommandStore store, WebPrincipal p) => store.All.Where(c => Sees(c, p));

    internal static bool Sees(SavedCommand c, WebPrincipal p) => c.Owner.Id == p.Id || c.Shared && p.Has(RunPerm);

    private static bool MayEdit(SavedCommand c, WebPrincipal p) => c.Owner.Id == p.Id || c.Shared && p.Has(ManageAll);

    private static async Task<JsonObject> DescribeAsync(SavedCommand c, WebPrincipal p, CommandRunner runner, IReadOnlySet<string> pins, CancellationToken ct)
    {
        var o = JsonSerializer.SerializeToNode(c, HttpResponse.JsonOptions)!.AsObject();
        o["mine"] = c.Owner.Id == p.Id;
        o["pinned"] = pins.Contains(c.Id);
        o["runnable"] = (await runner.CheckAsync(p, c.Line, false, ct).ConfigureAwait(false)).Caller is not null;
        return o;
    }

    private static JsonObject AuditArgs(SavedCommand c)
    {
        var a = KapGateway.Redact("command_run", new JsonObject { ["line"] = c.Line });
        a["saved"] = c.Id;
        a["revision"] = c.Revision;
        a["shared"] = c.Shared;
        a["confirm"] = c.Confirm;
        return a;
    }

    /// <summary>The editable fields, checked; null with a reason when they are not acceptable.</summary>
    internal static SavedCommand? Parse(JsonObject body, out string? problem)
    {
        problem = null;
        var name = OpsHttp.Str(body, "name")?.Trim();
        var line = OpsHttp.Str(body, "line")?.Trim();
        var description = OpsHttp.Str(body, "description")?.Trim();
        if (string.IsNullOrEmpty(name) || !OpsHttp.IsOneLine(name, 64)) problem = "name: 1 to 64 characters, one line";
        else if (description is not null && !OpsHttp.IsOneLine(description, 300)) problem = "description: at most 300 characters, one line";
        else if (string.IsNullOrEmpty(line) || !OpsHttp.IsOneLine(line, SavedCommandStore.MaxLine)) problem = $"line: 1 to {SavedCommandStore.MaxLine} characters, one line";
        else if (CommandRules.FirstWord(line) is not { Length: > 0 } cmd || !cmd.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-' or '.' or ':'))
            problem = "line: must start with a command name";
        else if (body["form"] is { } form && Encoding.UTF8.GetByteCount(form.ToJsonString()) > SavedCommandStore.MaxFormBytes)
            problem = $"form: at most {SavedCommandStore.MaxFormBytes} bytes";
        else if (body["confirm"] is not null and not JsonValue || body["shared"] is not null and not JsonValue) problem = "confirm, shared: true or false";
        if (problem is not null) return null;
        return new SavedCommand
        {
            Name = name!, Description = string.IsNullOrEmpty(description) ? null : description, Line = line!, Command = CommandRules.FirstWord(line),
            Form = body["form"]?.DeepClone(), Confirm = OpsHttp.Bool(body, "confirm") ?? false, Shared = OpsHttp.Bool(body, "shared") ?? false,
        };
    }
}
