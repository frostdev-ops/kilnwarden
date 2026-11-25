// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Extensions;
using Kilnwarden.Hosting;
using Kilnwarden.Metrics;
using Kilnwarden.Offline.Support;
using Kilnwarden.PublicPage;
using Kilnwarden.Webhooks;

namespace Kilnwarden.Offline.LaneC;

/// <summary>Lane C tests that start a panel run alone: the extension API's entry point (WebPanel) is process-wide, and
/// the mod load tests start panels too.</summary>
[CollectionDefinition("lane-c", DisableParallelization = true)]
public sealed class LaneCCollection;

/// <summary>A webhook sender that records instead of sending.</summary>
public sealed class FakeTransport : IWebhookTransport
{
    public readonly List<WebhookRequest> Sent = [];
    public int Status = 204;

    public Task<int> SendAsync(WebhookRequest request, CancellationToken ct)
    {
        lock (Sent) Sent.Add(request);
        return Task.FromResult(Status);
    }

    public int Count
    {
        get
        {
            lock (Sent) return Sent.Count;
        }
    }
}

/// <summary>A clock the test moves.</summary>
public sealed class ManualTime(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now = start;
    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>The real host with the auth component and the lane C components (no KAP metrics feed, a fake webhook
/// sender), plus signed-in browsers.</summary>
public sealed class LaneC : IAsyncDisposable
{
    public AuthHarness H = null!;
    public FakeTransport Transport = new();
    public ExtensionComponent Ext = new();
    public MetricsComponent Metrics = new(_ => null);
    public WebhooksComponent Webhooks = null!;
    public PublicPageComponent Public = new();
    public Kilnwarden.Activity.ActivityComponent Activity = null!;
    private Action<JsonNode?>? _push;

    /// <summary>One Kiln.Admin state push into Player activity (the harness has no KAP state feed).</summary>
    public void PushState(string json) => _push!(JsonNode.Parse(json));

    public static async Task<LaneC> StartAsync(bool kap = false, Action<FakeKap>? setup = null, IEnumerable<IWebAdminComponent>? extra = null,
        TempDir? root = null)
    {
        var c = new LaneC();
        c.Webhooks = new WebhooksComponent(c.Transport);
        c.Activity = new Kilnwarden.Activity.ActivityComponent((_, push) =>
        {
            c._push = push;
            return null;
        });
        c.H = await AuthHarness.StartAsync(kap: kap, setup: setup, extra: [c.Ext, c.Metrics, c.Webhooks, c.Public, c.Activity, .. extra ?? []], root: root);
        return c;
    }

    public ExtensionHost ExtHost => H.Host.Services.Get<ExtensionHost>();

    /// <summary>A signed-in browser for a new account with <paramref name="roles"/>.</summary>
    public async Task<AuthHarness.Client> UserAsync(string name, params string[] roles)
    {
        await H.AccountAsync(name, roles);
        var c = H.NewClient();
        var r = await c.SignInAsync(name);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return c;
    }

    public static async Task ReauthAsync(AuthHarness.Client c)
    {
        var r = await c.PostAsync("/api/auth/reauth", new { password = AuthHarness.Password });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    public static Task<HttpResponseMessage> PutAsync(AuthHarness.Client c, string path, object body) =>
        c.Http.PutAsync(path.TrimStart('/'), AuthHarness.Client.Json(body));

    public static async Task<JsonNode?> JsonAsync(HttpResponseMessage r) => JsonNode.Parse(await r.Content.ReadAsStringAsync());

    public static async Task<string> CodeAsync(HttpResponseMessage r) => (await JsonAsync(r))?["error"]?["code"]?.GetValue<string>() ?? "";

    public async ValueTask DisposeAsync() => await H.DisposeAsync();
}
