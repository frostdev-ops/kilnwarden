// SPDX-License-Identifier: MIT
using System.Diagnostics;
using System.Text.Json.Nodes;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Routing;
using Kilnwarden.Security;

namespace Kilnwarden.Modules;

/// <summary>The route an action runs behind: POST /api/&lt;module&gt;/actions/&lt;name&gt; with a JSON object body. The
/// gate has checked the permission before this runs; this does the game-thread hop and the audit record.</summary>
internal static class ActionRoutes
{
    public static RouteSpec RouteFor(string moduleId, ActionSpec action) => new()
    {
        Method = "POST",
        Pattern = $"{ModuleRegistry.ApiPrefix}{moduleId}/actions/{action.Name}",
        Access = RouteAccess.Requires(action.Permission),
        ModuleId = moduleId,
        Handler = ctx => RunAsync(ctx, moduleId, action),
    };

    private static async ValueTask<HttpResponse> RunAsync(RequestContext ctx, string moduleId, ActionSpec action)
    {
        var user = ctx.User ?? throw new HttpError(401, "sign in first");
        var args = ctx.Request.Body.IsEmpty ? new JsonObject() : ctx.ReadJson<JsonObject>();
        var call = new ActionCall(moduleId, action.Name, args, user, ctx.Aborted);
        var sw = Stopwatch.StartNew();
        string? error = null;
        try
        {
            JsonNode? result;
            if (action.RunOn == RunOn.GameThread)
            {
                var game = ctx.Services.TryGet<IGameThread>() ?? throw new HttpError(503, "the game thread is not available");
                // the handler's own awaits continue off the tick thread; its synchronous part runs on it
                result = await (await game.InvokeAsync(() => action.Handler(call).AsTask(), ctx.Aborted).ConfigureAwait(false)).ConfigureAwait(false);
            }
            else result = await action.Handler(call).ConfigureAwait(false);
            return HttpResponse.Json(new JsonObject { ["ok"] = true, ["result"] = result });
        }
        catch (HttpError e)
        {
            error = e.Message;
            throw;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // the exception's text stays in kiln.log and the audit log; the browser gets a plain message (review I2)
            error = e.Message;
            ctx.Services.TryGet<Hosting.WebAdminEnvironment>()?.Log.Error($"{moduleId}.{action.Name}: the action failed", e);
            return HttpResponse.Error(500, "action_failed", "the action failed: its mod reported an error (details in kiln.log)");
        }
        finally
        {
            ctx.Services.TryGet<IAuditSink>()?.Record(new AuditEntry(DateTimeOffset.UtcNow, user.Id, user.Name, $"{moduleId}.{action.Name}",
                Redact(args, action.Redact), error is null, error, sw.Elapsed.TotalMilliseconds, ctx.ClientAddress));
        }
    }

    /// <summary>The arguments as the audit log keeps them: the action's declared Redact names and anything that looks
    /// like a secret (password, secret, token, key in the name, any case) masked at any depth (review I2).</summary>
    internal static JsonObject Redact(JsonObject args, IReadOnlyList<string> names)
    {
        var copy = (JsonObject)args.DeepClone();
        Mask(copy, names, 0);
        return copy;
    }

    private static bool Secret(string name, IReadOnlyList<string> names)
    {
        if (names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase))) return true;
        var l = name.ToLowerInvariant();
        return l.Contains("password") || l.Contains("secret") || l.Contains("token") || l.Contains("key") || l.Contains("passwd");
    }

    private static void Mask(JsonNode? node, IReadOnlyList<string> names, int depth)
    {
        if (depth > 32) return;
        switch (node)
        {
            case JsonObject o:
                foreach (var k in o.Select(kv => kv.Key).ToList())
                {
                    if (Secret(k, names)) o[k] = "***";
                    else Mask(o[k], names, depth + 1);
                }
                break;
            case JsonArray a:
                foreach (var item in a) Mask(item, names, depth + 1);
                break;
        }
    }
}
