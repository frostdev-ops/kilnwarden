// SPDX-License-Identifier: MIT
using System.Net;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Kap;
using Kilnwarden.Routing;

namespace Kilnwarden.Auth;

/// <summary>What the panel does on a Kiln older than its floor (Kiln's host loads the mod on older dev builds anyway;
/// <see cref="KapClient.MissingCapabilities"/> tells): it refuses to serve. PipelineStage.Early: every request gets 503,
/// a static page for browsers and {"error":{"code":"kiln_too_old"}} for /api; nothing else runs. The setup state says
/// "no owner" so the host keeps the listener on loopback, whatever settings.json asks.</summary>
public sealed class KilnTooOld(KapClient kap, ISetupState accounts) : IHttpMiddleware, ISetupState
{
    public static string Message(IReadOnlyList<string> missing) =>
        $"{Product.Name} needs Kiln {Product.KilnFloor} or newer (Kiln.Admin is missing: {string.Join(", ", missing)})";

    public ValueTask<HttpResponse> InvokeAsync(RequestContext ctx, RequestDelegate next)
    {
        if (kap.MissingCapabilities is not { } missing) return next(ctx);
        var text = Message(missing);
        if (ctx.Request.Path.StartsWith(Modules.ModuleRegistry.ApiPrefix, StringComparison.Ordinal) || ctx.Request.Path == "/api")
            return ValueTask.FromResult(HttpResponse.Error(503, "kiln_too_old", text));
        var html = "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>" + WebUtility.HtmlEncode(Product.Name) +
                   "</title></head><body><h1>" + WebUtility.HtmlEncode(Product.Name) + "</h1><p>" + WebUtility.HtmlEncode(text) +
                   ".</p><p>Update Kiln on this server and restart it.</p></body></html>";
        return ValueTask.FromResult(HttpResponse.Html(503, html));
    }

    // ISetupState: the accounts' answer, except "no owner" while Kiln is too old (loopback only, and no remote setup)
    public bool OwnerExists => accounts.OwnerExists && kap.MissingCapabilities is null;
    public bool SetupOpen => !accounts.OwnerExists && kap.MissingCapabilities is null;

    public event Action? Changed
    {
        add
        {
            accounts.Changed += value;
            if (value is not null) _changed += value;
        }
        remove
        {
            accounts.Changed -= value;
            if (value is not null) _changed -= value;
        }
    }

    private event Action? _changed;

    /// <summary>The client found Kiln.Admin too old: the host re-applies its listeners (loopback only).</summary>
    public void OnIncompatible(IReadOnlyList<string> _) => _changed?.Invoke();
}
