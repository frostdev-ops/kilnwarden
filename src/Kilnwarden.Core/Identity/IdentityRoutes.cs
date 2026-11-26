// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Http;
using Kilnwarden.Modules;
using Kilnwarden.Routing;

namespace Kilnwarden.Identity;

/// <summary>/api/identity: Steam and Discord sign-in and the account's own links (docs/lanes/identity.md has the
/// contract). Flow: POST /start (same-origin; sets the binder cookie, SameSite=Lax) -> the provider -> GET
/// /{provider}/callback (only with that browser's binder: verifies, settles the intent, 303 to the app with #ext=state)
/// -> POST /complete from the same browser (the binder again).</summary>
internal static class IdentityRoutes
{
    public const string BinderCookie = "wa-ext";
    public const long BodyCap = 4096;

    /// <summary>/start's body. <c>back</c> (0.1.3, links only): "setup" returns the answer to the setup wizard instead of
    /// My account; nothing else is accepted, so it never names a URL.</summary>
    internal sealed record StartBody(string? Provider, string? Intent, string? Back = null);
    internal sealed record CompleteBody(string? State);

    public static void Map(ModuleRegistration m, IdentityService id)
    {
        var auth = id.Auth;

        m.MapPost("/start", RouteAccess.Public, ctx =>
        {
            var b = ctx.ReadJson<StartBody>();
            var kind = b.Intent switch { "login" => IntentKind.Login, "link" => IntentKind.Link, _ => (IntentKind?)null };
            if (kind is null) return Done(HttpResponse.Error(400, "invalid", "intent is \"login\" or \"link\""));
            var back = b.Back switch { null or "" or "account" => IntentReturn.Account, "setup" => IntentReturn.Setup, _ => (IntentReturn?)null };
            if (back is null || (back == IntentReturn.Setup && kind != IntentKind.Link)) return Done(HttpResponse.Error(400, "invalid", "back is \"account\" or, for a link, \"setup\""));
            if (b.Provider is not { } provider || !id.IsEnabled(provider))
                return Done(HttpResponse.Error(404, "unknown_provider", "that sign-in method is not available"));
            string? session = null;
            if (kind == IntentKind.Link)
            {
                if (ctx.User is not WebPrincipal p) return Done(HttpResponse.Error(401, "unauthenticated", "sign in first"));
                if (p.Restriction != Restriction.None) return Done(HttpResponse.Error(403, "restricted", "finish your account setup first"));
                if (back == IntentReturn.Setup && !p.IsOwner) return Done(HttpResponse.Error(403, "forbidden", "only the owner runs the setup"));
                if (!id.Allows(provider, p.Account)) return Done(HttpResponse.Error(403, "owner_only", "on this panel only the owner signs in with Discord"));
                if (!auth.RecentlyAuthenticated(p))
                    return Done(HttpResponse.Error(403, "reauth_required", "re-authenticate with your password or enrolled two-factor code, then retry"));
                session = p.Session.Handle;
            }
            if (auth.Limiter.Attempt(ctx.ClientAddress, null) is { } wait)
                return Done(AuthRoutes.Refusal(new SignInResult.Refused(429, "rate_limited", "too many attempts", wait)));
            var (url, binder) = id.Start(provider, kind.Value, session, ctx.Origin, back.Value);
            // Lax (not Strict): the provider's redirect back is a cross-site top-level GET, and the callback must see it
            ctx.ResponseHeaders.Add("Set-Cookie", new SetCookie(AuthCookies.Name(ctx, BinderCookie), binder)
            {
                Secure = ctx.IsHttps, MaxAge = LoginIntents.Lifetime, SameSite = SameSite.Lax,
            }.ToString());
            return Done(HttpResponse.Json(new JsonObject { ["url"] = url }));
        }, BodyCap);

        m.MapGet("/steam/callback", RouteAccess.Public, async ctx =>
        {
            var q = QueryString.Parse(ctx.Request.Query);
            var state = q.GetValueOrDefault("state");
            var intent = id.Intents.Find(state);
            if (intent is null || intent.Provider != "steam") return Back(null, null);
            // only the browser that started it may settle it (security review S1): an answer brought by any other
            // browser (someone who followed a link the starter sent) is not even verified
            if (!FromStarter(ctx, intent)) return Back(null, null);
            if (!intent.Settled)
            {
                var (who, error) = await id.Steam.VerifyAsync(q, intent, ctx.Aborted).ConfigureAwait(false);
                intent.Settle(who, error);
            }
            return Back(intent, state);
        });

        m.MapGet("/discord/callback", RouteAccess.Public, ctx =>
        {
            var q = QueryString.Parse(ctx.Request.Query);
            var state = q.GetValueOrDefault("state");
            var intent = id.Intents.Find(state);
            if (intent is null || intent.Provider != "discord") return Done(Back(null, null));
            if (!FromStarter(ctx, intent)) return Done(Back(null, null));
            if (!intent.Settled)
            {
                // the broker's codes (docs/protocol-relay-broker.md 6.2): a cancel and "not a member of the server" are
                // told apart; any other code is a refusal
                if (q.GetValueOrDefault("error") is { } err)
                    intent.Settle(null, err switch { "access_denied" => "canceled", "not_member" => "not_member", _ => "the broker refused the sign-in" });
                else
                {
                    var (who, error) = id.Discord.Verify(q.GetValueOrDefault("assertion"), intent, id.Settings.Discord, id.PanelId);
                    intent.Settle(who, error);
                }
            }
            return Done(Back(intent, state));
        });

        m.MapPost("/complete", RouteAccess.Public, async ctx =>
        {
            var b = ctx.ReadJson<CompleteBody>();
            var binder = AuthCookies.Read(ctx, BinderCookie);
            var found = id.Intents.Find(b.State);
            if (found is { Settled: false }) return (HttpResponse.Error(409, "ext_pending", "the provider has not answered yet"));
            var intent = id.Intents.Take(b.State, binder);
            if (intent is null) return (HttpResponse.Error(400, "ext_expired", "that sign-in expired or belongs to another browser: start again"));
            AuthCookies.Clear(ctx, BinderCookie);
            var client = AuthRoutes.Client(ctx);
            if (intent.Verified is not { } who)
            {
                auth.Audit.Record(AuditLog.Entry(intent.Kind == IntentKind.Link ? "auth.link" : "auth.login", ctx.User as WebPrincipal, ctx.ClientAddress, false,
                    intent.Error, new JsonObject { ["via"] = intent.Provider }));
                return (intent.Error switch
                {
                    "canceled" => HttpResponse.Error(400, "ext_canceled", "the sign-in was canceled"),
                    "not_allowed" => HttpResponse.Error(403, "not_allowed", "your Discord roles do not allow signing in to this panel"),
                    "not_member" => HttpResponse.Error(403, "not_member", "that Discord account is not a member of the panel's Discord server"),
                    _ => HttpResponse.Error(401, "ext_failed", $"the {intent.Provider} sign-in could not be verified: {intent.Error}"),
                });
            }
            if (!id.IsEnabled(intent.Provider)) return (HttpResponse.Error(404, "unknown_provider", "that sign-in method is not available"));
            if (intent.Kind == IntentKind.Login)
            {
                // discord.ownerOnly: an account that is not an owner is refused before anything else happens
                var linkedTo = auth.Accounts.All.Where(x => x.Links.TryGetValue(who.Provider, out var subject) && subject == who.Subject).Take(2).ToList();
                if (linkedTo is [{ } only] && !id.Allows(who.Provider, only))
                {
                    auth.Audit.Record(AuditLog.Entry("auth.login", null, ctx.ClientAddress, false, $"{who.Provider} sign-in is for the owner only on this panel",
                        new JsonObject { ["via"] = who.Provider }, userName: only.Name) with { User = only.Id });
                    return HttpResponse.Error(403, "owner_only", "on this panel only the owner signs in with Discord");
                }
                // the provider's role mapping first (identity.json roleMap), so the new session starts with those roles
                await id.SyncMappedRolesAsync(who, ctx.ClientAddress, ctx.Aborted).ConfigureAwait(false);
                var r = auth.SignInExternal(who, client, (ctx.User as WebPrincipal)?.Session.Handle);
                return AuthRoutes.Answer(ctx, auth, r);
            }
            // a link: the same session that started it, still unrestricted and with recent proof
            if (ctx.User is not WebPrincipal p || p.Session.Handle != intent.SessionHandle)
                return (HttpResponse.Error(401, "unauthenticated", "link from the session that started it"));
            if (p.Restriction != Restriction.None) return (HttpResponse.Error(403, "restricted", "finish your account setup first"));
            if (!id.Allows(who.Provider, p.Account)) return HttpResponse.Error(403, "owner_only", "on this panel only the owner signs in with Discord");
            if (!auth.RecentlyAuthenticated(p))
                return (HttpResponse.Error(403, "reauth_required", "re-authenticate with your password or enrolled two-factor code, then retry"));
            if (auth.Link(p, who, client) is { } refused) return AuthRoutes.Refusal(refused);
            await id.SyncMappedRolesAsync(who, ctx.ClientAddress, ctx.Aborted).ConfigureAwait(false);
            return HttpResponse.Json(new JsonObject { ["ok"] = true, ["linked"] = new JsonObject { ["provider"] = who.Provider, ["subject"] = who.Subject } });
        }, BodyCap);

        m.MapGet("/links", RouteAccess.SignedIn, ctx =>
        {
            var p = (WebPrincipal)ctx.User!;
            return Done(HttpResponse.Json(new JsonObject
            {
                ["providers"] = new JsonArray([.. id.EnabledFor(p.Account).Select(x => (JsonNode)new JsonObject { ["id"] = x.Id, ["title"] = x.Title })]),
                ["links"] = new JsonArray([.. p.Account.Links.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => (JsonNode)new JsonObject { ["provider"] = kv.Key, ["subject"] = kv.Value, ["enabled"] = id.IsEnabled(kv.Key) && id.Allows(kv.Key, p.Account) })]),
            }));
        });

        m.MapDelete("/links/{provider}", RouteAccess.SignedIn, ctx =>
        {
            var p = (WebPrincipal)ctx.User!;
            if (!auth.RecentlyAuthenticated(p))
                return Done(HttpResponse.Error(403, "reauth_required", "re-authenticate with your password or enrolled two-factor code, then retry"));
            return Done(auth.Unlink(p, ctx.RouteValue("provider"), AuthRoutes.Client(ctx)) ? HttpResponse.Empty() : HttpResponse.NotFound());
        });

        m.MapGet("/status", RouteAccess.Requires(UserRoutes.View), _ =>
        {
            var s = id.Settings;
            return Done(HttpResponse.Json(new JsonObject
            {
                ["panelId"] = id.PanelId,
                ["steam"] = new JsonObject { ["enabled"] = s.Steam.Enabled, ["active"] = id.IsEnabled("steam") },
                ["discord"] = new JsonObject
                {
                    ["enabled"] = s.Discord.Enabled, ["active"] = id.IsEnabled("discord"), ["broker"] = s.Discord.Broker,
                    ["keyPinned"] = id.Discord.HasKeyFor(s.Discord.Broker), ["guildId"] = s.Discord.GuildId, ["allowedRoles"] = s.Discord.AllowedRoles.Count,
                    ["ownerOnly"] = s.Discord.OwnerOnly,
                },
            }));
        });
    }

    /// <summary>Whether the request carries the binder cookie of the browser that started <paramref name="intent"/>.</summary>
    private static bool FromStarter(RequestContext ctx, LoginIntent intent) =>
        AuthCookies.Read(ctx, BinderCookie) is { } binder && SecretToken.HashEquals(SecretToken.Hash(binder), intent.BinderHash);

    /// <summary>Back to the app: the sign-in page (or, for a link, the account page or the setup wizard) with the state in
    /// the fragment, which never reaches a server or a Referer. An unknown state goes to the sign-in page with an error.</summary>
    private static HttpResponse Back(LoginIntent? intent, string? state)
    {
        var page = intent is null ? "/signin" : intent.Kind != IntentKind.Link ? "/signin" : intent.Return == IntentReturn.Setup ? "/setup" : "/account";
        var res = intent is null || state is null
            ? HttpResponse.Redirect("/signin#ext-error=expired")
            : HttpResponse.Redirect(page + "#ext=" + state);
        res.Headers.Set("Cache-Control", "no-store");
        res.Headers.Set("Referrer-Policy", "no-referrer");
        return res;
    }

    private static ValueTask<HttpResponse> Done(HttpResponse r) => ValueTask.FromResult(r);
}
