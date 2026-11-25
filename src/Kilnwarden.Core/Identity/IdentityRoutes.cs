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

    internal sealed record StartBody(string? Provider, string? Intent);
    internal sealed record CompleteBody(string? State);

    public static void Map(ModuleRegistration m, IdentityService id)
    {
        var auth = id.Auth;

        m.MapPost("/start", RouteAccess.Public, ctx =>
        {
            var b = ctx.ReadJson<StartBody>();
            var kind = b.Intent switch { "login" => IntentKind.Login, "link" => IntentKind.Link, _ => (IntentKind?)null };
            if (kind is null) return Done(HttpResponse.Error(400, "invalid", "intent is \"login\" or \"link\""));
            if (b.Provider is not { } provider || !id.IsEnabled(provider))
                return Done(HttpResponse.Error(404, "unknown_provider", "that sign-in method is not available"));
            string? session = null;
            if (kind == IntentKind.Link)
            {
                if (ctx.User is not WebPrincipal p) return Done(HttpResponse.Error(401, "unauthenticated", "sign in first"));
                if (p.Restriction != Restriction.None) return Done(HttpResponse.Error(403, "restricted", "finish your account setup first"));
                if (!auth.RecentlyAuthenticated(p))
                    return Done(HttpResponse.Error(403, "reauth_required", "re-authenticate with your password or enrolled two-factor code, then retry"));
                session = p.Session.Handle;
            }
            if (auth.Limiter.Attempt(ctx.ClientAddress, null) is { } wait)
                return Done(AuthRoutes.Refusal(new SignInResult.Refused(429, "rate_limited", "too many attempts", wait)));
            var (url, binder) = id.Start(provider, kind.Value, session, ctx.Origin);
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
                if (q.GetValueOrDefault("error") is { } err) intent.Settle(null, err == "access_denied" ? "canceled" : "the broker refused the sign-in");
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
                    _ => HttpResponse.Error(401, "ext_failed", $"the {intent.Provider} sign-in could not be verified: {intent.Error}"),
                });
            }
            if (!id.IsEnabled(intent.Provider)) return (HttpResponse.Error(404, "unknown_provider", "that sign-in method is not available"));
            if (intent.Kind == IntentKind.Login)
            {
                // the provider's role mapping first (identity.json roleMap), so the new session starts with those roles
                await id.SyncMappedRolesAsync(who, ctx.ClientAddress, ctx.Aborted).ConfigureAwait(false);
                var r = auth.SignInExternal(who, client, (ctx.User as WebPrincipal)?.Session.Handle);
                return AuthRoutes.Answer(ctx, auth, r);
            }
            // a link: the same session that started it, still unrestricted and with recent proof
            if (ctx.User is not WebPrincipal p || p.Session.Handle != intent.SessionHandle)
                return (HttpResponse.Error(401, "unauthenticated", "link from the session that started it"));
            if (p.Restriction != Restriction.None) return (HttpResponse.Error(403, "restricted", "finish your account setup first"));
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
                ["providers"] = new JsonArray([.. id.Enabled().Select(x => (JsonNode)new JsonObject { ["id"] = x.Id, ["title"] = x.Title })]),
                ["links"] = new JsonArray([.. p.Account.Links.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => (JsonNode)new JsonObject { ["provider"] = kv.Key, ["subject"] = kv.Value, ["enabled"] = id.IsEnabled(kv.Key) })]),
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
                },
            }));
        });
    }

    /// <summary>Back to the app: the sign-in page (or the account page for a link) with the state in the fragment, which
    /// never reaches a server or a Referer. An unknown state goes to the sign-in page with an error.</summary>
    /// <summary>Whether the request carries the binder cookie of the browser that started <paramref name="intent"/>.</summary>
    private static bool FromStarter(RequestContext ctx, LoginIntent intent) =>
        AuthCookies.Read(ctx, BinderCookie) is { } binder && SecretToken.HashEquals(SecretToken.Hash(binder), intent.BinderHash);

    private static HttpResponse Back(LoginIntent? intent, string? state)
    {
        var res = intent is null || state is null
            ? HttpResponse.Redirect("/signin#ext-error=expired")
            : HttpResponse.Redirect((intent.Kind == IntentKind.Link ? "/account" : "/signin") + "#ext=" + state);
        res.Headers.Set("Cache-Control", "no-store");
        res.Headers.Set("Referrer-Policy", "no-referrer");
        return res;
    }

    private static ValueTask<HttpResponse> Done(HttpResponse r) => ValueTask.FromResult(r);
}
