// SPDX-License-Identifier: MIT
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Kilnwarden.Auth;
using Kilnwarden.Config;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Kap;
using Kilnwarden.Modules;
using Kilnwarden.Routing;
using Kilnwarden.Storage;

namespace Kilnwarden.Ops;

/// <summary>A checked package waiting to be staged (or an upload still receiving bytes).</summary>
public sealed class PackagePlan
{
    public string Id { get; init; } = "";
    public string Kind { get; set; } = "";
    public string? ModId { get; set; }
    public string? Name { get; set; }
    public string Version { get; set; } = "";
    public string? Current { get; set; }
    public string? Folder { get; set; }
    public int Files { get; set; }
    public long Bytes { get; set; }
    public string Sha256 { get; set; } = "";
    public string SourceType { get; init; } = "";
    public string SourceName { get; init; } = "";
    public List<string> Warnings { get; } = [];
    public SignatureCheck Signature { get; set; } = SignatureCheck.Unsigned;
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset Expires { get; set; }

    internal string Dir { get; init; } = "";
    internal string Zip => Path.Combine(Dir, "package.zip");

    // an upload in progress (not a plan yet)
    internal bool Receiving { get; set; }
    internal long Size { get; init; }
    internal long Received { get; set; }
    internal string? Expected { get; init; }

    /// <summary>The .minisig text given with the upload (or at its finish).</summary>
    internal string? SignatureText { get; set; }
    internal string Owner { get; init; } = "";
    internal readonly SemaphoreSlim Lock = new(1, 1);

    public JsonObject Describe() => new()
    {
        ["id"] = Id, ["kind"] = Kind, ["modId"] = ModId, ["name"] = Name, ["version"] = Version, ["current"] = Current, ["folder"] = Folder,
        ["files"] = Files, ["bytes"] = Bytes, ["sha256"] = Sha256,
        ["source"] = new JsonObject { ["type"] = SourceType, [SourceType == "url" ? "url" : "name"] = SourceName },
        ["warnings"] = new JsonArray([.. Warnings.Select(w => (JsonNode)w)]),
        ["signature"] = new JsonObject
        {
            ["state"] = Signature.State, ["key"] = Signature.Key, ["keyId"] = Signature.KeyId, ["trustedComment"] = Signature.TrustedComment,
        },
        ["created"] = Created.ToString("O"), ["expiresAt"] = Expires.ToString("O"),
    };
}

/// <summary>Uploads and plans in memory, their bytes in updates/scratch/&lt;id&gt;; at most <see cref="MaxOpen"/> at a
/// time, gone after <see cref="Lifetime"/>. The scratch folder is emptied at start.</summary>
internal sealed class PlanStore(DataStore data, TimeProvider time)
{
    public const int MaxOpen = 4;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    private readonly object _gate = new();
    private readonly Dictionary<string, PackagePlan> _plans = new(StringComparer.Ordinal);

    public string Scratch => data.PathOf("updates/scratch");

    public void Reset()
    {
        try
        {
            if (Directory.Exists(Scratch)) Directory.Delete(Scratch, true);
        }
        catch (IOException)
        {
        }
        Directory.CreateDirectory(Scratch);
    }

    public PackagePlan Open(string sourceType, string sourceName, string owner, long size = 0, string? expected = null, bool receiving = false)
    {
        lock (_gate)
        {
            Expire();
            if (_plans.Count >= MaxOpen) throw new InvalidOperationException($"at most {MaxOpen} uploads and plans at a time: stage or remove one");
            var id = "p-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
            var now = time.GetUtcNow();
            var plan = new PackagePlan
            {
                Id = id, SourceType = sourceType, SourceName = sourceName, Owner = owner, Size = size, Expected = expected, Receiving = receiving,
                Created = now, Expires = now + Lifetime, Dir = Path.Combine(Scratch, id),
            };
            Directory.CreateDirectory(plan.Dir);
            _plans[id] = plan;
            return plan;
        }
    }

    public PackagePlan? Find(string? id)
    {
        lock (_gate)
        {
            Expire();
            return id is not null && _plans.TryGetValue(id, out var p) ? p : null;
        }
    }

    public IReadOnlyList<PackagePlan> All
    {
        get
        {
            lock (_gate)
            {
                Expire();
                return [.. _plans.Values.Where(p => !p.Receiving).OrderBy(p => p.Created)];
            }
        }
    }

    public void Remove(string id)
    {
        PackagePlan? p;
        lock (_gate)
            if (_plans.Remove(id, out p)) Delete(p.Dir);
    }

    public void Expire()
    {
        lock (_gate)
        {
            var now = time.GetUtcNow();
            foreach (var p in _plans.Values.Where(p => p.Expires <= now).ToList())
            {
                _plans.Remove(p.Id);
                Delete(p.Dir);
            }
        }
    }

    internal static void Delete(string dir)
    {
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>The mod-updates module (docs/step2-lane-b-ops.md "Mod updates"): status, uploads, URL fetches, plans,
/// staging through Kiln.Admin's update_* ops, cancel, rollback, the session's mod lifecycle ops, the source allowlist.</summary>
internal static partial class UpdateRoutes
{
    public const string ModuleId = "updates";
    public const string View = "updates.view", Prepare = "updates.prepare", Stage = "updates.stage", Sources = "updates.sources";
    public const string Switch = "updates.switch";
    public const int ChunkBytes = 512 << 10;
    public static readonly string IncomingPrefix = Product.ModId + "-";

    public static ModuleSpec Spec() => new()
    {
        Id = ModuleId, Title = "Mod updates", Icon = "package-check",
        Description = "Installed mods, updates staged for the next start, rollbacks; packages from an upload or an allowed URL, checksum-verified",
        Permissions =
        [
            new PermissionSpec(View, "See mods, staged updates and rollbacks") { DefaultRole = BuiltinRole.Admin },
            new PermissionSpec(Prepare, "Upload or fetch a package and check it (owner accounts only)") { DefaultRole = BuiltinRole.Owner },
            new PermissionSpec(Stage, "Stage an update for the next start, cancel it, roll back (owner accounts only)") { DefaultRole = BuiltinRole.Owner, Sensitive = true },
            new PermissionSpec(Sources, "Change where packages may come from (owner accounts only)") { DefaultRole = BuiltinRole.Owner, Sensitive = true },
            new PermissionSpec(Switch, "Switch mods on or off for the next start (owner accounts only)") { DefaultRole = BuiltinRole.Owner, Sensitive = true },
        ],
        // page id "updates" (the KAP mods module owns the page id "mods"); both open the web app's /mods page
        Pages = [new PageSpec("updates", "Mods", PageKind.Builtin, "updates") { Icon = "puzzle", Permission = View, Order = 80 }],
    };

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex FolderRe();

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex ShaRe();

    [GeneratedRegex("^(kiln|mod\\.[A-Za-z0-9][A-Za-z0-9._-]{0,63})$")]
    private static partial Regex UnitRe();

    public sealed record Deps(PlanStore Plans, SettingsStore<UpdateSettings> Settings, SettingsStore<ServerSettings> Server, AuthService Auth, KapClient Kap,
        IPackageDownloader Downloader, WebAdminEnvironment Env, TimeProvider Time, IWebAdminLog Log, Func<string>? Incoming = null);

    public static void Map(ModuleRegistration m, Deps d)
    {
        // one download at a time per panel
        var downloads = new SemaphoreSlim(1, 1);
        m.MapGet("", RouteAccess.Requires(View), async ctx =>
        {
            var p = OpsHttp.User(ctx);
            var o = new JsonObject
            {
                ["plans"] = new JsonArray([.. d.Plans.All.Select(x => (JsonNode)x.Describe())]),
                ["restart"] = d.Kap.Session?.Restart?.DeepClone(),
                ["limits"] = new JsonObject
                {
                    ["maxUploadBytes"] = d.Settings.Current.MaxUploadBytes, ["maxDownloadBytes"] = d.Settings.Current.MaxDownloadBytes, ["chunkBytes"] = Chunk(d),
                },
                ["allowedOrigins"] = new JsonArray([.. d.Settings.Current.AllowedOrigins.Select(x => (JsonNode)x)]),
                ["owner"] = p.IsOwner,
                ["folders"] = ModSwitch.Folders(ModSwitch.ModsDirectory(d.Env), d.Env),
            };
            foreach (var (key, op) in new[] { ("mods", "mods"), ("updates", "update_list") })
            {
                var g = KapGate.CheckOp(p, op, null, d.Auth.Registry, d.Auth.Now);
                if (!g.Allowed)
                {
                    o[key] = new JsonObject { ["error"] = new JsonObject { ["code"] = g.Code, ["message"] = g.Message } };
                    continue;
                }
                var r = await d.Kap.CallAsync(p.Caller(), op, null, ctx.Aborted).ConfigureAwait(false);
                o[key] = r.Ok ? r.Data?.DeepClone() : new JsonObject { ["error"] = new JsonObject { ["code"] = r.Code, ["message"] = r.Error } };
            }
            return HttpResponse.Json(o);
        });
        m.MapGet("/plans/{id}", RouteAccess.Requires(View), ctx =>
            ValueTask.FromResult(d.Plans.Find(ctx.RouteValue("id")) is { Receiving: false } plan ? HttpResponse.Json(plan.Describe()) : HttpResponse.NotFound()));
        m.MapDelete("/plans/{id}", RouteAccess.Requires(Prepare), ctx =>
        {
            var p = OpsHttp.User(ctx);
            if (!p.IsOwner) return ValueTask.FromResult(OwnerOnly());
            var plan = d.Plans.Find(ctx.RouteValue("id"));
            if (plan is null) return ValueTask.FromResult(HttpResponse.NotFound());
            d.Plans.Remove(plan.Id);
            OpsHttp.Audit(d.Auth.Audit, "updates.plan_remove", p, ctx.ClientAddress, true, null, new JsonObject { ["plan"] = plan.Id });
            return ValueTask.FromResult(HttpResponse.Empty());
        });

        // ---- uploads: open, chunks, finish
        m.MapPost("/uploads", RouteAccess.Requires(Prepare), ctx =>
        {
            var p = OpsHttp.User(ctx);
            if (!p.IsOwner) return ValueTask.FromResult(OwnerOnly());
            var b = OpsHttp.Body(ctx);
            var name = Path.GetFileName(OpsHttp.Str(b, "name") ?? "");
            var size = OpsHttp.Long(b, "size") ?? -1;
            var sha = OpsHttp.Str(b, "sha256")?.ToLowerInvariant();
            if (!OpsHttp.IsOneLine(name, 128) || name.Length == 0) return ValueTask.FromResult(OpsHttp.Fail(422, "invalid", "name: the file's name (1 to 128 characters)"));
            if (size < 1 || size > d.Settings.Current.MaxUploadBytes)
                return ValueTask.FromResult(OpsHttp.Fail(422, "too_large", $"size: 1 to {d.Settings.Current.MaxUploadBytes} bytes (updates.json maxUploadBytes)"));
            if (sha is not null && sha.Length > 0 && !ShaRe().IsMatch(sha)) return ValueTask.FromResult(OpsHttp.Fail(422, "invalid", "sha256: 64 hex digits"));
            var sig = OpsHttp.Str(b, "signature");
            if (sig is { Length: > Minisign.MaxSignatureText }) return ValueTask.FromResult(OpsHttp.Fail(422, "invalid", "signature: a .minisig file's text (at most 4 KiB)"));
            PackagePlan plan;
            try
            {
                plan = d.Plans.Open("upload", name, p.Id, size, string.IsNullOrEmpty(sha) ? null : sha, receiving: true);
            }
            catch (InvalidOperationException e)
            {
                return ValueTask.FromResult(OpsHttp.Fail(409, "limit", e.Message));
            }
            plan.SignatureText = string.IsNullOrWhiteSpace(sig) ? null : sig;
            File.Create(plan.Zip).Dispose();
            return ValueTask.FromResult(HttpResponse.Json(new JsonObject
            {
                ["id"] = plan.Id, ["chunkBytes"] = Chunk(d), ["expiresAt"] = plan.Expires.ToString("O"),
            }, 201));
        });
        m.MapPut("/uploads/{id}", RouteAccess.Requires(Prepare), async ctx =>
        {
            var p = OpsHttp.User(ctx);
            if (!p.IsOwner) return OwnerOnly();
            var plan = d.Plans.Find(ctx.RouteValue("id"));
            if (plan is not { Receiving: true } || plan.Owner != p.Id) return HttpResponse.NotFound();
            if (!ctx.Request.QueryValues.TryGetValue("offset", out var off) || !long.TryParse(off, out var offset)) throw new HttpError(400, "?offset=<bytes received so far>");
            var body = ctx.Request.Body;
            if (body.Length == 0 || body.Length > Chunk(d)) return OpsHttp.Fail(413, "too_large", $"a chunk is 1 to {Chunk(d)} bytes");
            await plan.Lock.WaitAsync(ctx.Aborted).ConfigureAwait(false);
            try
            {
                if (offset != plan.Received) return OpsHttp.Fail(409, "offset", $"expected offset {plan.Received}");
                if (plan.Received + body.Length > plan.Size) return OpsHttp.Fail(413, "too_large", "more bytes than the upload declared");
                await using (var fs = new FileStream(plan.Zip, FileMode.Append, FileAccess.Write, FileShare.None))
                    await fs.WriteAsync(body, ctx.Aborted).ConfigureAwait(false);
                plan.Received += body.Length;
                return HttpResponse.Json(new JsonObject { ["received"] = plan.Received });
            }
            finally
            {
                plan.Lock.Release();
            }
        });
        m.MapPost("/uploads/{id}/finish", RouteAccess.Requires(Prepare), async ctx =>
        {
            var p = OpsHttp.User(ctx);
            if (!p.IsOwner) return OwnerOnly();
            var plan = d.Plans.Find(ctx.RouteValue("id"));
            if (plan is not { Receiving: true } || plan.Owner != p.Id) return HttpResponse.NotFound();
            var fb = OpsHttp.Body(ctx);
            var expectId = OpsHttp.Str(fb, "expectId");
            if (OpsHttp.Str(fb, "signature") is { } fsig)
            {
                if (fsig.Length > Minisign.MaxSignatureText) return OpsHttp.Fail(422, "invalid", "signature: a .minisig file's text (at most 4 KiB)");
                if (!string.IsNullOrWhiteSpace(fsig)) plan.SignatureText = fsig;
            }
            await plan.Lock.WaitAsync(ctx.Aborted).ConfigureAwait(false);
            try
            {
                if (plan.Received != plan.Size) return OpsHttp.Fail(409, "incomplete", $"{plan.Received} of {plan.Size} bytes received");
                plan.Receiving = false;
            }
            finally
            {
                plan.Lock.Release();
            }
            return await MakePlanAsync(ctx, p, plan, plan.Expected, expectId, d).ConfigureAwait(false);
        });

        // ---- a URL
        m.MapPost("/fetch", RouteAccess.Requires(Prepare), async ctx =>
        {
            var p = OpsHttp.User(ctx);
            if (!p.IsOwner) return OwnerOnly();
            var b = OpsHttp.Body(ctx);
            var sha = OpsHttp.Str(b, "sha256")?.ToLowerInvariant();
            if (!Uri.TryCreate(OpsHttp.Str(b, "url"), UriKind.Absolute, out var url)) return OpsHttp.Fail(422, "invalid", "url: an https URL");
            if (sha is null || !ShaRe().IsMatch(sha)) return OpsHttp.Fail(422, "invalid", "sha256: the package's SHA-256 (64 hex digits) is required for a URL");
            var sigText = OpsHttp.Str(b, "signature");
            if (sigText is { Length: > Minisign.MaxSignatureText }) return OpsHttp.Fail(422, "invalid", "signature: a .minisig file's text (at most 4 KiB)");
            Uri? sigUrl = null;
            if (OpsHttp.Str(b, "signatureUrl") is { Length: > 0 } su && !Uri.TryCreate(su, UriKind.Absolute, out sigUrl))
                return OpsHttp.Fail(422, "invalid", "signatureUrl: an https URL (the package's .minisig)");
            var settings = d.Settings.Current;
            if (sigUrl is not null && UrlPolicy.Problem(sigUrl, settings.AllowedOrigins) is { } sigWhy) return OpsHttp.Fail(422, "source_refused", "signatureUrl: " + sigWhy);
            var shown = OpsHttp.SafeUrl(url);
            if (UrlPolicy.Problem(url, settings.AllowedOrigins) is { } why)
            {
                OpsHttp.Audit(d.Auth.Audit, "updates.fetch", p, ctx.ClientAddress, false, "source_refused", new JsonObject { ["url"] = shown });
                return OpsHttp.Fail(422, "source_refused", why);
            }
            PackagePlan plan;
            try
            {
                plan = d.Plans.Open("url", shown, p.Id);
            }
            catch (InvalidOperationException e)
            {
                return OpsHttp.Fail(409, "limit", e.Message);
            }
            if (!await downloads.WaitAsync(0, ctx.Aborted).ConfigureAwait(false))
            {
                d.Plans.Remove(plan.Id);
                return OpsHttp.Fail(409, "busy", "another download is running");
            }
            try
            {
                var final = await d.Downloader.DownloadAsync(url, plan.Zip, settings.MaxDownloadBytes, settings.AllowedOrigins, ctx.Aborted).ConfigureAwait(false);
                if (final != url) plan.Warnings.Add($"redirected to {OpsHttp.SafeUrl(final)}");
                if (sigUrl is not null)
                {
                    var sigFile = Path.Combine(Path.GetDirectoryName(plan.Zip)!, "package.minisig");
                    await d.Downloader.DownloadAsync(sigUrl, sigFile, Minisign.MaxSignatureText, settings.AllowedOrigins, ctx.Aborted).ConfigureAwait(false);
                    sigText = await File.ReadAllTextAsync(sigFile, ctx.Aborted).ConfigureAwait(false);
                }
                plan.SignatureText = string.IsNullOrWhiteSpace(sigText) ? null : sigText;
            }
            catch (DownloadRefusedException e)
            {
                d.Plans.Remove(plan.Id);
                OpsHttp.Audit(d.Auth.Audit, "updates.fetch", p, ctx.ClientAddress, false, "download_failed", new JsonObject { ["url"] = shown });
                d.Log.Warn($"updates: fetching {shown} for {p.Name} failed: {e.Message}");
                return OpsHttp.Fail(502, "download_failed", e.Message);
            }
            finally
            {
                downloads.Release();
            }
            return await MakePlanAsync(ctx, p, plan, sha, OpsHttp.Str(b, "expectId"), d).ConfigureAwait(false);
        });

        // ---- stage, cancel, rollback
        m.MapPost("/plans/{id}/stage", RouteAccess.Requires(Stage), async ctx =>
        {
            var p = OpsHttp.User(ctx);
            if (!p.IsOwner) return OwnerOnly();
            var plan = d.Plans.Find(ctx.RouteValue("id"));
            if (plan is not { Receiving: false }) return HttpResponse.NotFound();
            var b = OpsHttp.Body(ctx);
            if (OpsHttp.Str(b, "sha256")?.ToLowerInvariant() != plan.Sha256)
                return OpsHttp.Fail(422, "checksum", "sha256: send the plan's SHA-256 to confirm what you checked");
            var label = OpsHttp.Str(b, "label");
            if (label is not null && !OpsHttp.IsOneLine(label, 120)) return OpsHttp.Fail(422, "invalid", "label: at most 120 characters, one line");
            if (d.Settings.Current.RequireSignature && plan.Signature.State != "verified")
                return OpsHttp.Fail(409, "signature_required", "this server stages only packages a trusted key signed (updates.json requireSignature)");
            var args = new JsonObject { ["kind"] = plan.Kind };
            var gate = KapGate.CheckOp(p, "update_stage", args, d.Auth.Registry, d.Auth.Now);
            if (!gate.Allowed) return OpsHttp.Refused(gate);
            if (!await plan.Lock.WaitAsync(0, ctx.Aborted).ConfigureAwait(false)) return OpsHttp.Fail(409, "busy", "this plan is being staged");
            var sw = Stopwatch.StartNew();
            string? target = null;
            KapResult? r = null;
            var audit = new JsonObject
            {
                ["plan"] = plan.Id, ["kind"] = plan.Kind, ["modId"] = plan.ModId, ["version"] = plan.Version, ["sha256"] = plan.Sha256,
                ["source"] = plan.SourceType, ["from"] = plan.SourceName, ["signature"] = plan.Signature.State, ["signedBy"] = plan.Signature.Key,
            };
            try
            {
                // the scratch copy is what was checked; refuse when it changed since
                if (PackageArchive.Sha256File(plan.Zip) != plan.Sha256) return OpsHttp.Fail(409, "checksum", "the package changed since it was checked: upload it again");
                var incoming = await IncomingAsync(p, d, ctx.Aborted).ConfigureAwait(false);
                if (incoming.Problem is { } bad) return OpsHttp.Fail(409, "incoming", bad);
                target = Path.Combine(incoming.Path!, IncomingPrefix + plan.Id);
                IReadOnlyList<(string Path, string Sha256, long Size)> files;
                try
                {
                    files = PackageArchive.Extract(plan.Zip, target, d.Settings.Current.Limits);
                }
                catch (ArchiveRefusedException e)
                {
                    return OpsHttp.Fail(422, "archive", e.Message);
                }
                args["source"] = IncomingPrefix + plan.Id;
                args["files"] = new JsonArray([.. files.Select(f => (JsonNode)new JsonObject { ["path"] = f.Path, ["sha256"] = f.Sha256 })]);
                args["label"] = label ?? $"{Product.ModId}: {plan.SourceType} {Path.GetFileName(plan.SourceName)}";
                if (plan.Kind == "mod") args["id"] = plan.ModId;
                else args["sha256"] = plan.Sha256;
                r = await d.Kap.CallAsync(p.Caller(), "update_stage", args, ctx.Aborted).ConfigureAwait(false);
                if (r.Ok) d.Plans.Remove(plan.Id);
                return r.Ok ? HttpResponse.Json(r.Data?.DeepClone() ?? new JsonObject()) : OpsHttp.KapFailed(r);
            }
            finally
            {
                plan.Lock.Release();
                // Kiln.Admin copies from incoming, never moves: the folder goes, unless a timed-out staging may still be copying
                if (target is not null && r?.Code != "timeout") PlanStore.Delete(target);
                OpsHttp.Audit(d.Auth.Audit, "updates.stage", p, ctx.ClientAddress, r?.Ok == true, r is null ? "not_sent" : r.Ok ? null : r.Code, audit,
                    sw.Elapsed.TotalMilliseconds);
            }
        });
        foreach (var (verb, op) in new[] { ("cancel", "update_cancel"), ("rollback", "update_rollback") })
            m.MapPost($"/units/{{unit}}/{verb}", RouteAccess.Requires(Stage), async ctx =>
            {
                var p = OpsHttp.User(ctx);
                if (!p.IsOwner) return OwnerOnly();
                var unit = ctx.RouteValue("unit");
                if (!UnitRe().IsMatch(unit)) return OpsHttp.Fail(422, "invalid", "unit: kiln or mod.<folder>");
                return await KapAsync(ctx, p, d, op, new JsonObject { ["unit"] = unit }, "updates." + verb).ConfigureAwait(false);
            });

        // ---- this session's mod lifecycle (Kiln.Admin's own ops and permissions)
        foreach (var (verb, op, field) in new[] { ("load", "mod_load", "folder"), ("unload", "mod_unload", "id"), ("reload", "mod_reload", "id") })
            m.MapPost($"/mods/{verb}", RouteAccess.Requires(View), async ctx =>
            {
                var p = OpsHttp.User(ctx);
                var v = OpsHttp.Str(OpsHttp.Body(ctx), field);
                if (v is null || !FolderRe().IsMatch(v)) return OpsHttp.Fail(422, "invalid", $"{field}: a mod {(field == "id" ? "id" : "folder name")}");
                return await KapAsync(ctx, p, d, op, new JsonObject { [field] = v }, "updates.mod_" + verb).ConfigureAwait(false);
            });

        // ---- persistent switches: a .disabled file in the mod's folder (Kiln skips that folder at the next start)
        m.MapPost("/mods/switch", RouteAccess.Requires(Switch), async ctx =>
        {
            var p = OpsHttp.User(ctx);
            if (!p.IsOwner) return OwnerOnly();
            var b = OpsHttp.Body(ctx);
            var folder = OpsHttp.Str(b, "folder");
            if (b["enabled"] is not JsonValue ev || !ev.TryGetValue<bool>(out var enabled)) return OpsHttp.Fail(422, "invalid", "enabled: true or false");
            var now = b["now"] is JsonValue nv && nv.TryGetValue<bool>(out var n) && n;
            if (folder is null || !FolderRe().IsMatch(folder)) return OpsHttp.Fail(422, "invalid", "folder: a folder name under kiln/mods");
            var audit = new JsonObject { ["folder"] = folder, ["enabled"] = enabled, ["now"] = now };
            ModSwitch.Result result;
            try
            {
                result = ModSwitch.Set(ModSwitch.ModsDirectory(d.Env), d.Env, folder, !enabled, $"{Product.ModId} (web:{p.Name})", d.Time.GetUtcNow());
            }
            catch (ModSwitchException e)
            {
                OpsHttp.Audit(d.Auth.Audit, "updates.mod_switch", p, ctx.ClientAddress, false, e.Code, audit);
                return OpsHttp.Fail(e.Status, e.Code, e.Message);
            }
            OpsHttp.Audit(d.Auth.Audit, "updates.mod_switch", p, ctx.ClientAddress, true, null, audit);
            var o = new JsonObject { ["folder"] = folder, ["id"] = result.Id, ["enabled"] = enabled, ["changed"] = result.Changed, ["now"] = null };
            if (now)
            {
                // this session too, through Kiln.Admin's own ops and permissions (mod_load takes the folder, mod_unload the id)
                var (op, field, value) = enabled ? ("mod_load", "folder", folder) : ("mod_unload", "id", result.Id ?? folder);
                var r = await KapAsync(ctx, p, d, op, new JsonObject { [field] = value }, "updates.mod_" + (enabled ? "load" : "unload")).ConfigureAwait(false);
                o["now"] = r.Status == 200 ? new JsonObject { ["ok"] = true } : new JsonObject { ["ok"] = false, ["status"] = r.Status };
            }
            return HttpResponse.Json(o);
        });

        // ---- sources
        m.MapGet("/sources", RouteAccess.Requires(View), _ =>
            ValueTask.FromResult(HttpResponse.Json(new { settings = d.Settings.Current, problems = d.Settings.Problems })));
        m.MapPut("/sources", RouteAccess.Requires(Sources), ctx =>
        {
            var p = OpsHttp.User(ctx);
            if (!p.IsOwner) return ValueTask.FromResult(OwnerOnly());
            var next = ctx.ReadJson<UpdateSettings>();
            try
            {
                d.Settings.Save(next);
            }
            catch (ArgumentException e)
            {
                return ValueTask.FromResult(OpsHttp.Fail(422, "invalid", e.Message));
            }
            OpsHttp.Audit(d.Auth.Audit, "updates.sources", p, ctx.ClientAddress, true, null,
                new JsonObject
                {
                    ["allowedOrigins"] = new JsonArray([.. next.AllowedOrigins.Select(o => (JsonNode)o)]),
                    ["trustedKeys"] = new JsonArray([.. next.Keys().Select(k => (JsonNode)$"{k.Name} ({k.KeyIdText})")]), ["requireSignature"] = next.RequireSignature,
                });
            return ValueTask.FromResult(HttpResponse.Json(new { ok = true }));
        });
    }

    private static HttpResponse OwnerOnly() => OpsHttp.Fail(403, "owner_only", "only an owner account may install or stage code");

    private static int Chunk(Deps d) => (int)Math.Max(4096, Math.Min(ChunkBytes, d.Server.Current.Limits.MaxBodyBytes - 1024));

    private static async Task<HttpResponse> KapAsync(RequestContext ctx, WebPrincipal p, Deps d, string op, JsonObject args, string auditOp)
    {
        var g = KapGate.CheckOp(p, op, args, d.Auth.Registry, d.Auth.Now);
        if (!g.Allowed)
        {
            OpsHttp.Audit(d.Auth.Audit, auditOp, p, ctx.ClientAddress, false, g.Code, (JsonObject)args.DeepClone());
            return OpsHttp.Refused(g);
        }
        var r = await d.Kap.CallAsync(p.Caller(), op, args, ctx.Aborted).ConfigureAwait(false);
        OpsHttp.Audit(d.Auth.Audit, auditOp, p, ctx.ClientAddress, r.Ok, r.Ok ? null : r.Code, (JsonObject)args.DeepClone());
        return r.Ok ? HttpResponse.Json(r.Data?.DeepClone() ?? new JsonObject()) : OpsHttp.KapFailed(r);
    }

    /// <summary>Checks the received bytes (digest, archive, manifest) and turns the upload or download into a plan.</summary>
    private static async Task<HttpResponse> MakePlanAsync(RequestContext ctx, WebPrincipal p, PackagePlan plan, string? expected, string? expectId, Deps d)
    {
        var audit = new JsonObject { ["plan"] = plan.Id, ["source"] = plan.SourceType, ["from"] = plan.SourceName };
        HttpResponse Refuse(int status, string code, string message)
        {
            d.Plans.Remove(plan.Id);
            OpsHttp.Audit(d.Auth.Audit, "updates.plan", p, ctx.ClientAddress, false, code, audit);
            return OpsHttp.Fail(status, code, message);
        }

        var sha = PackageArchive.Sha256File(plan.Zip);
        audit["sha256"] = sha;
        if (expected is not null && sha != expected) return Refuse(422, "checksum", $"the SHA-256 is {sha}, not the expected {expected}");
        ArchiveInfo info;
        try
        {
            info = PackageArchive.Inspect(plan.Zip, d.Settings.Current.Limits);
        }
        catch (ArchiveRefusedException e)
        {
            return Refuse(422, "archive", e.Message);
        }
        if (expectId is not null && info.Id != expectId) return Refuse(422, "wrong_package", $"the package is '{info.Id ?? info.Kind}', not '{expectId}'");
        // a signature that does not match is never accepted; unsigned or an unknown key is the owner's call (or refused
        // at staging while updates.json requireSignature is on)
        var check = Minisign.VerifyFile(plan.SignatureText, plan.Zip, d.Settings.Current.Keys());
        audit["signature"] = check.State;
        if (check.Key is not null) audit["signedBy"] = check.Key;
        if (check.State == "invalid") return Refuse(422, "bad_signature", "the signature does not match this package: " + check.Problem);
        plan.Signature = check;
        if (check.State == "unsigned")
            plan.Warnings.Add(d.Settings.Current.RequireSignature ? "not signed: this server stages signed packages only (updates.json requireSignature)" : "not signed");
        if (check.State == "unknown_key")
            plan.Warnings.Add($"signed by key {check.KeyId}, which is not one of the trusted keys (Mods > Sources)");
        plan.Sha256 = sha;
        plan.Kind = info.Kind;
        plan.ModId = info.Id;
        plan.Name = info.Name;
        plan.Version = info.Version;
        plan.Files = info.Files.Count;
        plan.Bytes = info.Bytes;
        if (expected is null) plan.Warnings.Add("no expected SHA-256 was given: compare the one shown with the publisher's before you stage it");
        if (info.Prefix.Length > 0) plan.Warnings.Add($"the archive's top folder '{info.Prefix.TrimEnd('/')}' is left out");
        // what is installed now (update_list as the owner); unknown when Kiln.Admin cannot say
        var g = KapGate.CheckOp(p, "update_list", null, d.Auth.Registry, d.Auth.Now);
        if (g.Allowed && await d.Kap.CallAsync(p.Caller(), "update_list", null, ctx.Aborted).ConfigureAwait(false) is { Ok: true, Data: JsonObject list }
            && list["units"] is JsonArray units)
        {
            var unit = units.OfType<JsonObject>().FirstOrDefault(u => info.Kind == "kiln"
                ? KapFrames.Str(u, "kind") == "kiln"
                : KapFrames.Str(u, "kind") == "mod" && string.Equals(KapFrames.Str(u, "id"), info.Id, StringComparison.OrdinalIgnoreCase));
            plan.Folder = unit is null ? info.Id : KapFrames.Str(unit, "folder") ?? info.Id;
            plan.Current = unit?["current"] is JsonObject cur ? KapFrames.Str(cur, "version") : null;
            if (unit?["staged"] is JsonObject) plan.Warnings.Add("an update of this unit is already staged: cancel it before you stage this one");
            if (unit?["rollback"] is JsonObject) plan.Warnings.Add("a rollback of this unit is waiting: cancel it before you stage this one");
            if (plan.Current == info.Version) plan.Warnings.Add($"version {info.Version} is the one installed");
        }
        else
        {
            plan.Folder = info.Kind == "mod" ? info.Id : null;
            plan.Warnings.Add("Kiln.Admin did not say what is installed now");
        }
        audit["kind"] = info.Kind;
        audit["modId"] = info.Id;
        audit["version"] = info.Version;
        OpsHttp.Audit(d.Auth.Audit, "updates.plan", p, ctx.ClientAddress, true, null, audit);
        return HttpResponse.Json(plan.Describe(), 201);
    }

    /// <summary>kiln/staged/incoming: the folder update_list names, which must be the one in this server folder, a plain
    /// folder (no link or junction on the way from the server folder).</summary>
    private static async Task<(string? Path, string? Problem)> IncomingAsync(WebPrincipal p, Deps d, CancellationToken ct)
    {
        var server = Path.GetFullPath(d.Env.ServerDirectory);
        var expected = d.Incoming?.Invoke() ?? Path.Combine(server, "kiln", "staged", "incoming");
        var r = await d.Kap.CallAsync(p.Caller(), "update_list", null, ct).ConfigureAwait(false);
        if (r is { Ok: true, Data: JsonObject o } && KapFrames.Str(o, "incoming") is { } named)
        {
            var cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!string.Equals(Path.GetFullPath(named).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(expected).TrimEnd(Path.DirectorySeparatorChar), cmp))
                return (null, "Kiln.Admin's incoming folder is not this server's kiln/staged/incoming");
        }
        Directory.CreateDirectory(expected);
        for (var dir = new DirectoryInfo(expected); dir is not null && dir.FullName.Length > server.Length; dir = dir.Parent)
            if (dir.Attributes.HasFlag(FileAttributes.ReparsePoint)) return (null, $"{dir.Name} is a link or junction: Kiln does not stage there");
        return (expected, null);
    }

    /// <summary>Removes this panel's leftovers in incoming (a staging that timed out) older than an hour.</summary>
    public static void SweepIncoming(string incoming, TimeProvider time)
    {
        try
        {
            if (!Directory.Exists(incoming)) return;
            foreach (var dir in Directory.EnumerateDirectories(incoming, IncomingPrefix + "*"))
                if (time.GetUtcNow() - Directory.GetCreationTimeUtc(dir) > TimeSpan.FromHours(1)) PlanStore.Delete(dir);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
