// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;
using Kilnwarden.Http;
using Kilnwarden.Kap;
using Kilnwarden.Modules;
using Kilnwarden.Routing;

namespace Kilnwarden.Auth;

/// <summary>Who may hand out what: nobody gives more than they have. The owner may do anything (but never leave the
/// panel without an enabled owner). Anyone else may only manage accounts whose roles they cover, and only assign or edit
/// roles they cover: every declared permission of the role is theirs, its command level is not above theirs, and it is
/// not the owner role. Nobody changes their own roles or state through the users API.</summary>
public static class Delegation
{
    public static bool Covers(WebPrincipal actor, IEnumerable<RoleDef> roles, ModuleRegistry registry)
    {
        if (actor.IsOwner) return true;
        var mine = actor.Effective().ToHashSet(StringComparer.Ordinal);
        foreach (var r in roles)
        {
            if (r.Id == BuiltinRoles.Owner) return false;
            if (r.CommandLevel > actor.CommandLevel) return false;
            if (!WebPrincipal.Effective(r, registry).IsSubsetOf(mine)) return false;
        }
        return true;
    }

    /// <summary>Why <paramref name="actor"/> may not manage <paramref name="target"/>, or null.</summary>
    public static string? CannotManage(WebPrincipal actor, Account target, RoleStore roles, ModuleRegistry registry)
    {
        if (actor.Id == target.Id) return "change your own account from your account page";
        if (actor.IsOwner) return null;
        if (target.IsOwner) return "only an owner can change an owner's account";
        return Covers(actor, target.Roles.Select(roles.Find).OfType<RoleDef>(), registry) ? null : "that account has permissions you do not have";
    }
}

/// <summary>/api/users: accounts and roles (the Users and Roles page). users.view to look, users.manage to change
/// accounts, users.roles to change roles; every change is audited, and the delegation rules apply on top.</summary>
internal static class UserRoutes
{
    public const string View = "users.view", Manage = "users.manage", RolesPerm = "users.roles";

    internal sealed record CreateBody(string? Name, string? Password, List<string>? Roles);
    internal sealed record PatchBody(List<string>? Roles, bool? Disabled);
    internal sealed record PasswordBody(string? Password);
    internal sealed record RoleBody(string? Id, string? Name, List<string>? Grants, List<string>? Revokes, string? CommandLevel,
        List<string>? CommandDeny, List<string>? CommandOnly, bool? Require2fa);

    public static void Map(ModuleRegistration m, AuthService auth)
    {
        var reg = auth.Registry;

        m.MapGet("/", RouteAccess.Requires(View), _ => Ok(new JsonArray([.. auth.Accounts.All.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).Select(a => (JsonNode)Describe(a, auth))])));

        m.MapPost("/", RouteAccess.Requires(Manage), async ctx =>
        {
            var actor = (WebPrincipal)ctx.User!;
            var b = ctx.ReadJson<CreateBody>();
            var name = b.Name?.Trim() ?? "";
            if (!AccountNames.IsValid(name)) return Bad("a name is 3 to 32 of A-Z a-z 0-9 . _ - (starting with a letter or digit)");
            if (auth.Accounts.FindByName(name) is not null) return HttpResponse.Error(409, "name_taken", $"the name '{name}' is taken");
            if (RolesOf(auth, b.Roles, out var roles) is { } bad) return Bad(bad);
            if (!Delegation.Covers(actor, roles, reg)) return Forbidden(actor, ctx, "users.create", "you cannot give roles with permissions you do not have");
            if (auth.PasswordProblem(name, b.Password) is { } weak) return HttpResponse.Error(422, "weak_password", weak);
            var hash = await auth.Hasher.HashAsync(b.Password!, ctx.Aborted).ConfigureAwait(false);
            if (RecheckAfterHash(auth, actor) is { } changed) return changed;
            Account a;
            try
            {
                a = auth.Accounts.Add(new Account
                {
                    Name = name, PasswordHash = hash, Roles = [.. roles.Select(r => r.Id)], MustChangePassword = true, CreatedBy = actor.Name,
                });
            }
            catch (InvalidOperationException e)
            {
                return HttpResponse.Error(409, "name_taken", e.Message);
            }
            Audit(auth, actor, ctx, "users.create", new JsonObject { ["account"] = a.Id, ["name"] = a.Name, ["roles"] = Arr(a.Roles) });
            return HttpResponse.Json(Describe(a, auth), 201);
        }, AuthRoutes.BodyCap);

        m.MapGet("/{id}", RouteAccess.Requires(View), ctx =>
            Ok(auth.Accounts.Find(ctx.RouteValue("id")) is { } a ? Describe(a, auth) : null));

        m.MapPatch("/{id}", RouteAccess.Requires(Manage), ctx =>
        {
            var actor = (WebPrincipal)ctx.User!;
            if (Target(auth, actor, ctx, out var target) is { } refusal) return Done(refusal);
            var b = ctx.ReadJson<PatchBody>();
            List<RoleDef>? roles = null;
            if (b.Roles is not null)
            {
                if (RolesOf(auth, b.Roles, out var r) is { } bad) return Done(Bad(bad));
                if (!Delegation.Covers(actor, r, reg)) return Done(Forbidden(actor, ctx, "users.update", "you cannot give roles with permissions you do not have"));
                roles = r;
            }
            Account updated;
            try
            {
                updated = auth.Accounts.Update(target!.Id, x =>
                {
                    var next = x;
                    if (roles is not null && !roles.Select(r => r.Id).SequenceEqual(x.Roles))
                        next = next with { Roles = [.. roles.Select(r => r.Id)], PrivilegeStamp = x.PrivilegeStamp + 1 };
                    if (b.Disabled is { } d && d != x.Disabled) next = next with { Disabled = d, CredentialStamp = x.CredentialStamp + 1 };
                    return next;
                });
            }
            catch (InvalidOperationException e)
            {
                return Done(HttpResponse.Error(409, "last_owner", e.Message));
            }
            if (updated.Disabled) auth.Sessions.RevokeAccount(updated.Id);
            Audit(auth, actor, ctx, "users.update", new JsonObject { ["account"] = updated.Id, ["roles"] = b.Roles is null ? null : Arr(updated.Roles), ["disabled"] = b.Disabled });
            return Done(HttpResponse.Json(Describe(updated, auth)));
        }, AuthRoutes.BodyCap);

        m.MapPost("/{id}/password", RouteAccess.Requires(Manage), async ctx =>
        {
            var actor = (WebPrincipal)ctx.User!;
            if (Target(auth, actor, ctx, out var target) is { } refusal) return refusal;
            var b = ctx.ReadJson<PasswordBody>();
            if (auth.PasswordProblem(target!.Name, b.Password) is { } weak) return HttpResponse.Error(422, "weak_password", weak);
            var hash = await auth.Hasher.HashAsync(b.Password!, ctx.Aborted).ConfigureAwait(false);
            if (RecheckAfterHash(auth, actor) is { } changed) return changed;
            auth.Accounts.Update(target.Id, x => x with { PasswordHash = hash, MustChangePassword = true, CredentialStamp = x.CredentialStamp + 1 });
            auth.Sessions.RevokeAccount(target.Id);
            Audit(auth, actor, ctx, "users.password_reset", new JsonObject { ["account"] = target.Id });
            return HttpResponse.Empty();
        }, AuthRoutes.BodyCap);

        m.MapPost("/{id}/2fa/reset", RouteAccess.Requires(Manage), ctx =>
        {
            var actor = (WebPrincipal)ctx.User!;
            if (Target(auth, actor, ctx, out var target) is { } refusal) return Done(refusal);
            auth.Accounts.Update(target!.Id, x => x with { TotpSecret = null, TotpLastStep = 0, PendingTotpSecret = null, RecoveryCodes = [], CredentialStamp = x.CredentialStamp + 1 });
            auth.Sessions.RevokeAccount(target.Id);
            Audit(auth, actor, ctx, "users.2fa_reset", new JsonObject { ["account"] = target.Id });
            return Done(HttpResponse.Empty());
        });

        m.MapDelete("/{id}", RouteAccess.Requires(Manage), ctx =>
        {
            var actor = (WebPrincipal)ctx.User!;
            if (Target(auth, actor, ctx, out var target) is { } refusal) return Done(refusal);
            try
            {
                auth.Accounts.Remove(target!.Id);
            }
            catch (InvalidOperationException e)
            {
                return Done(HttpResponse.Error(409, "last_owner", e.Message));
            }
            auth.Sessions.RevokeAccount(target.Id);
            Audit(auth, actor, ctx, "users.delete", new JsonObject { ["account"] = target.Id, ["name"] = target.Name });
            return Done(HttpResponse.Empty());
        });

        m.MapGet("/{id}/sessions", RouteAccess.Requires(View), ctx =>
            Ok(auth.Accounts.Find(ctx.RouteValue("id")) is { } a ? AuthRoutes.Sessions(auth.Sessions.ForAccount(a.Id), null) : null));

        m.MapDelete("/{id}/sessions", RouteAccess.Requires(Manage), ctx =>
        {
            var actor = (WebPrincipal)ctx.User!;
            if (Target(auth, actor, ctx, out var target) is { } refusal) return Done(refusal);
            var n = auth.Sessions.RevokeAccount(target!.Id);
            Audit(auth, actor, ctx, "users.session_revoke", new JsonObject { ["account"] = target.Id, ["revoked"] = n });
            return Done(HttpResponse.Json(new JsonObject { ["revoked"] = n }));
        });

        // ---- roles

        m.MapGet("/roles", RouteAccess.Requires(View), _ => Ok(new JsonArray([.. auth.Roles.All.Select(r => (JsonNode)DescribeRole(r, auth))])));

        m.MapPost("/roles", RouteAccess.Requires(RolesPerm), ctx =>
        {
            var b = ctx.ReadJson<RoleBody>();
            if (b.Id is null || auth.Roles.Find(b.Id) is not null) return Done(HttpResponse.Error(409, "role_exists", "pick a new role id"));
            return Done(PutRole(auth, ctx, b.Id, b, creating: true));
        }, 64 << 10);

        m.MapPut("/roles/{id}", RouteAccess.Requires(RolesPerm), ctx =>
        {
            var b = ctx.ReadJson<RoleBody>();
            var id = ctx.RouteValue("id");
            if (auth.Roles.Find(id) is null) return Done(HttpResponse.NotFound());
            return Done(PutRole(auth, ctx, id, b, creating: false));
        }, 64 << 10);

        m.MapDelete("/roles/{id}", RouteAccess.Requires(RolesPerm), ctx =>
        {
            var actor = (WebPrincipal)ctx.User!;
            var id = ctx.RouteValue("id");
            if (auth.Roles.Find(id) is not { } role) return Done(HttpResponse.NotFound());
            if (role.IsBuiltin) return Done(Bad("built-in roles cannot be removed"));
            if (!Delegation.Covers(actor, [role], reg)) return Done(Forbidden(actor, ctx, "users.role_delete", "that role has permissions you do not have"));
            foreach (var a in auth.Accounts.All.Where(a => a.Roles.Contains(id)).ToList())
                auth.Accounts.Update(a.Id, x => x with { Roles = [.. x.Roles.Where(r => r != id)], PrivilegeStamp = x.PrivilegeStamp + 1 });
            auth.Roles.Remove(id);
            Audit(auth, actor, ctx, "users.role_delete", new JsonObject { ["role"] = id });
            return Done(HttpResponse.Empty());
        });

        m.MapGet("/permissions", RouteAccess.Requires(View), _ => Ok(new JsonArray([.. reg.Modules.Select(mod => (JsonNode)new JsonObject
        {
            ["module"] = mod.Spec.Id, ["title"] = mod.Spec.Title, ["enabled"] = mod.Enabled,
            ["permissions"] = new JsonArray([.. mod.Spec.Permissions.Select(p => (JsonNode)new JsonObject
            {
                ["name"] = p.Name, ["title"] = p.Title, ["description"] = p.Description,
                ["defaultRole"] = p.DefaultRole.ToString().ToLowerInvariant(), ["sensitive"] = p.Sensitive,
            })]),
        })])));
    }

    private static HttpResponse? RecheckAfterHash(AuthService auth, WebPrincipal actor)
    {
        if (auth.ResolveHandle(actor.Session.Handle) is not { } live)
            return HttpResponse.Error(401, "unauthenticated", "the sign-in ended");
        if (live.Account.PrivilegeStamp != actor.Account.PrivilegeStamp)
            return HttpResponse.Error(403, "forbidden", "permissions changed: retry the request");
        return auth.RecentlyAuthenticated(live) ? null
            : HttpResponse.Error(403, "reauth_required", "re-authenticate with your password or enrolled two-factor code, then retry");
    }

    private static HttpResponse PutRole(AuthService auth, RequestContext ctx, string id, RoleBody b, bool creating)
    {
        var actor = (WebPrincipal)ctx.User!;
        var reg = auth.Registry;
        var old = auth.Roles.Find(id);
        CommandLevel level;
        if (b.CommandLevel is null) level = old?.CommandLevel ?? CommandLevel.None;
        else if (!CommandLevels.TryParse(b.CommandLevel, out level)) return Bad("commandLevel: none, everyone, admin or console");
        var role = new RoleDef
        {
            Id = id, Name = b.Name?.Trim() ?? old?.Name ?? id,
            Grants = b.Grants ?? old?.Grants ?? [], Revokes = b.Revokes ?? old?.Revokes ?? [],
            CommandLevel = level, CommandDeny = b.CommandDeny ?? old?.CommandDeny ?? [],
            CommandOnly = b.CommandOnly ?? old?.CommandOnly, Require2fa = b.Require2fa ?? old?.Require2fa ?? false,
        };
        if (RoleStore.Validate(role) is { } problem) return Bad(problem);
        if (!actor.IsOwner)
        {
            if (role.IsBuiltin) return Forbidden(actor, ctx, "users.role_update", "only an owner changes the built-in roles");
            if (old is not null && !Delegation.Covers(actor, [old], reg) || !Delegation.Covers(actor, [role], reg))
                return Forbidden(actor, ctx, "users.role_update", "a role cannot get (or already has) permissions you do not have");
        }
        auth.Roles.Put(role);
        // the role's members get new session ids at their next request (rotation on privilege change)
        foreach (var a in auth.Accounts.All.Where(a => a.Roles.Contains(id)).ToList())
            auth.Accounts.Update(a.Id, x => x with { PrivilegeStamp = x.PrivilegeStamp + 1 });
        Audit(auth, actor, ctx, creating ? "users.role_create" : "users.role_update", new JsonObject { ["role"] = id, ["def"] = DescribeRole(role, auth) });
        return HttpResponse.Json(DescribeRole(role, auth), creating ? 201 : 200);
    }

    private static string? RolesOf(AuthService auth, List<string>? ids, out List<RoleDef> roles)
    {
        roles = [];
        if (ids is null || ids.Count == 0) return "an account needs at least one role";
        if (ids.Count > 32) return "too many roles";
        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            if (auth.Roles.Find(id) is not { } r) return $"no role '{id}'";
            roles.Add(r);
        }
        return null;
    }

    private static HttpResponse? Target(AuthService auth, WebPrincipal actor, RequestContext ctx, out Account? target)
    {
        target = auth.Accounts.Find(ctx.RouteValue("id"));
        if (target is null) return HttpResponse.NotFound();
        return Delegation.CannotManage(actor, target, auth.Roles, auth.Registry) is { } why ? Forbidden(actor, ctx, "users.manage", why) : null;
    }

    public static JsonObject Describe(Account a, AuthService auth) => new()
    {
        ["id"] = a.Id, ["name"] = a.Name, ["roles"] = Arr(a.Roles), ["owner"] = a.IsOwner, ["primary"] = a.Primary, ["disabled"] = a.Disabled,
        ["twoFactor"] = a.TwoFactor, ["mustChangePassword"] = a.MustChangePassword, ["created"] = a.Created.ToString("O"),
        ["lastSignIn"] = a.LastSignIn?.ToString("O"), ["createdBy"] = a.CreatedBy, ["sessions"] = auth.Sessions.ForAccount(a.Id).Count,
        ["links"] = new JsonArray([.. a.Links.Keys.Select(k => (JsonNode)k)]),
        // roles a Discord or Steam role mapping gives now (identity.json roleMap), apart from the ones set here
        ["mappedRoles"] = Arr([.. auth.MappedRoles?.Invoke(a).Distinct() ?? []]),
    };

    public static JsonObject DescribeRole(RoleDef r, AuthService auth) => new()
    {
        ["id"] = r.Id, ["name"] = r.Name, ["builtin"] = r.IsBuiltin, ["grants"] = Arr(r.Grants), ["revokes"] = Arr(r.Revokes),
        ["commandLevel"] = CommandLevels.Name(r.CommandLevel), ["maxCommandLevel"] = CommandLevels.Name(BuiltinRoles.MaxLevel(r.Id)),
        ["commandDeny"] = Arr(r.CommandDeny), ["commandOnly"] = r.CommandOnly is null ? null : Arr(r.CommandOnly), ["require2fa"] = r.Require2fa,
        ["effective"] = Arr(WebPrincipal.Effective(r, auth.Registry).Order(StringComparer.Ordinal)),
        ["members"] = auth.Accounts.All.Count(a => a.Roles.Contains(r.Id)),
    };

    private static JsonArray Arr(IEnumerable<string> items) => new([.. items.Select(i => (JsonNode)i)]);

    private static void Audit(AuthService auth, WebPrincipal actor, RequestContext ctx, string op, JsonObject args) =>
        auth.Audit.Record(AuditLog.Entry(op, actor, ctx.ClientAddress, true, args: args));

    private static HttpResponse Forbidden(WebPrincipal actor, RequestContext ctx, string op, string why)
    {
        ctx.Services.TryGet<AuditLog>()?.Record(AuditLog.Entry(op, actor, ctx.ClientAddress, false, why));
        return HttpResponse.Error(403, "forbidden", why);
    }

    private static HttpResponse Bad(string why) => HttpResponse.Error(422, "invalid", why);
    private static ValueTask<HttpResponse> Done(HttpResponse r) => ValueTask.FromResult(r);
    private static ValueTask<HttpResponse> Ok(JsonNode? body) => ValueTask.FromResult(body is null ? HttpResponse.NotFound() : HttpResponse.Json(body));
}

/// <summary>/api/audit: the audit log, newest first (audit.view).</summary>
internal static class AuditRoutes
{
    public const string View = "audit.view";

    public static void Map(ModuleRegistration m, AuditLog audit)
    {
        m.MapGet("/", RouteAccess.Requires(View), ctx =>
        {
            var q = ctx.Request.QueryValues;
            var limit = q.TryGetValue("limit", out var l) && int.TryParse(l, out var n) ? Math.Clamp(n, 1, 1000) : 200;
            DateTimeOffset? before = q.TryGetValue("before", out var b) && DateTimeOffset.TryParse(b, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var t) ? t : null;
            var entries = audit.Read(limit, before, q.GetValueOrDefault("user"), q.GetValueOrDefault("op"));
            return ValueTask.FromResult(HttpResponse.Json(entries));
        });
    }
}
