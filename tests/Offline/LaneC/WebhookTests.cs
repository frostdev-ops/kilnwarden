// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Events;
using Kilnwarden.Offline.Support;
using Kilnwarden.Security;
using Kilnwarden.Webhooks;

namespace Kilnwarden.Offline.LaneC;

[Collection("lane-c")]
public sealed class WebhookTests
{
    private const string Discord = "https://discord.com/api/webhooks/123456789012345678/abcdefghijklmnopqrstuvwxyz0123456789_-";

    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("1.1.1.1", true)]
    [InlineData("2606:4700:4700::1111", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.1.2.3", false)]
    [InlineData("172.16.0.1", false)]
    [InlineData("172.31.255.255", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("255.255.255.255", false)]
    [InlineData("198.18.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("::", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fc00::1", false)]
    [InlineData("fd12:3456::1", false)]
    [InlineData("ff02::1", false)]
    [InlineData("::ffff:127.0.0.1", false)]
    [InlineData("::ffff:8.8.8.8", true)]
    [InlineData("64:ff9b::7f00:1", false)]
    [InlineData("2001:db8::1", false)]
    [InlineData("2002:7f00:0001::1", false)]
    [InlineData("2002:0808:0808::1", true)]
    [InlineData("2001:0:4136:e378::1", false)]
    public void Only_public_addresses(string ip, bool ok) => Assert.Equal(ok, WebhookGuard.IsPublic(IPAddress.Parse(ip)));

    [Theory]
    [InlineData("discord", Discord, true)]
    [InlineData("discord", "https://discordapp.com/api/v10/webhooks/123456789012345678/abcdefghijklmnopqrstuvwxyz", true)]
    [InlineData("discord", "https://evil.example/api/webhooks/123456789012345678/abcdefghijklmnopqrstuvwxyz", false)]
    [InlineData("discord", "http://discord.com/api/webhooks/123456789012345678/abcdefghijklmnopqrstuvwxyz", false)]
    [InlineData("discord", "https://discord.com:8443/api/webhooks/123456789012345678/abcdefghijklmnopqrstuvwxyz", false)]
    [InlineData("discord", "https://discord.com/api/webhooks/123456789012345678/abcdefghijklmnopqrstuvwxyz?wait=true", false)]
    [InlineData("json", "https://hooks.example.com/in", true)]
    [InlineData("json", "https://user:pw@hooks.example.com/in", false)]
    [InlineData("json", "https://hooks.example.com/in#x", false)]
    [InlineData("json", "https://localhost/in", false)]
    [InlineData("json", "https://nas.local/in", false)]
    [InlineData("json", "https://router.lan/in", false)]
    [InlineData("json", "https://intranet/in", false)]
    [InlineData("json", "https://127.0.0.1/in", false)]
    [InlineData("json", "https://[::1]/in", false)]
    [InlineData("json", "https://10.0.0.5/in", false)]
    [InlineData("json", "https://169.254.169.254/latest", false)]
    [InlineData("json", "https://8.8.8.8/in", true)]
    [InlineData("json", "ftp://hooks.example.com/in", false)]
    [InlineData("slack", "https://hooks.example.com/in", false)]
    public void Urls_are_checked(string kind, string url, bool ok) => Assert.Equal(ok, WebhookGuard.CheckUrl(kind, url, out _) is null);

    [Fact]
    public void Urls_are_masked()
    {
        Assert.Equal("https://discord.com/api/webhooks/123456789012345678/***", WebhookGuard.Mask(Discord));
        Assert.Equal("https://hooks.example.com/***", WebhookGuard.Mask("https://hooks.example.com/secret/path?token=1"));
        Assert.DoesNotContain("abcdef", WebhookGuard.Mask(Discord));
        // a plain http hook (private-target opt-in) shows as http, not https (lab step2-fix1)
        Assert.Equal("http://192.168.1.20:8080/***", WebhookGuard.Mask("http://192.168.1.20:8080/secret"));
    }

    [Fact]
    public void Payloads_carry_only_allowed_fields_and_no_mentions()
    {
        var kind = new EventKind("player.join", "A player joined", ["player"], "t");
        var e = new PanelEvent("player.join", DateTimeOffset.UnixEpoch, new JsonObject
        {
            ["player"] = "@everyone " + new string('x', 400), ["steamId"] = "7656119", ["pos"] = new JsonObject { ["x"] = 1 },
        });
        var discord = JsonNode.Parse(WebhookPayloads.Build("discord", "{{player}} joined {{steamId}}", e, kind, "My <b>server</b>"))!;
        var text = discord.ToJsonString();
        Assert.DoesNotContain("7656119", text);
        Assert.DoesNotContain("\"pos\"", text);
        Assert.DoesNotContain("@everyone", discord["content"]!.GetValue<string>());
        Assert.Empty(discord["allowed_mentions"]!["parse"]!.AsArray());
        var json = JsonNode.Parse(WebhookPayloads.Build("json", null, e, kind, "s"))!;
        Assert.Equal(["player"], json["data"]!.AsObject().Select(kv => kv.Key));
        Assert.Equal(WebhookPayloads.MaxString, json["data"]!["player"]!.GetValue<string>().Length);
    }

    [Fact]
    public async Task Dispatcher_rate_limits_per_hook_and_logs_without_urls()
    {
        var t = new FakeTransport();
        var time = new ManualTime(DateTimeOffset.UnixEpoch.AddDays(20000));
        await using var d = new WebhookDispatcher(t, time, new TestLog(), () => "srv");
        var hook = new WebhookHook { Id = "h-1", Name = "one", Kind = "discord", Url = Discord, Events = ["*"] };
        var kind = new EventKind("webhook.test", "test", ["by"], "t");
        for (var i = 0; i < 25; i++) await d.SendOneAsync(hook, new PanelEvent("webhook.test", time.Now, []), kind);
        Assert.Equal(WebhookDispatcher.PerMinute, t.Count);
        var log = d.Deliveries();
        Assert.Equal(5, log.Count(x => x!["error"]?.GetValue<string>() == "rate_limited"));
        Assert.DoesNotContain("abcdef", log.ToJsonString());
        time.Now = time.Now.AddMinutes(1);
        await d.SendOneAsync(hook, new PanelEvent("webhook.test", time.Now, []), kind);
        Assert.Equal(WebhookDispatcher.PerMinute + 1, t.Count);
        // a redirect is a failure (never followed)
        t.Status = 302;
        await d.SendOneAsync(hook, new PanelEvent("webhook.test", time.Now, []), kind);
        Assert.Equal("redirect_refused", d.Deliveries()[0]!["error"]!.GetValue<string>());
    }

    [Fact]
    public async Task Real_sender_never_connects_to_a_local_address()
    {
        // a disposable loopback listener stands for "something on the server's own network"
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var sender = new HttpWebhookTransport((host, _) => ValueTask.FromResult(host == "rebind.example" ? new[] { IPAddress.Loopback } : Array.Empty<IPAddress>()));
        foreach (var url in new[] { $"https://127.0.0.1:{port}/x", $"https://rebind.example:{port}/x", $"https://nowhere.example:{port}/x" })
        {
            var e = await Assert.ThrowsAnyAsync<Exception>(() => sender.SendAsync(new WebhookRequest(new Uri(url), "{}"u8.ToArray()), CancellationToken.None));
            Assert.True(e is WebhookRefusedException || e.InnerException is WebhookRefusedException, e.ToString());
        }
        Assert.False(listener.Pending());
        listener.Stop();
    }

    [Fact]
    public async Task Routes_are_sensitive_mask_urls_and_send_chosen_events()
    {
        await using var c = await LaneC.StartAsync();
        using var owner = await c.UserAsync("olive", BuiltinRoles.Owner);
        using var admin = await c.UserAsync("adam", BuiltinRoles.Admin);
        var body = new { name = "Discord", kind = "discord", url = Discord, events = new[] { "player.*", "admin.action" } };

        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync("/api/webhooks/hooks", body)).StatusCode); // owner only
        var r = await owner.PostAsync("/api/webhooks/hooks", body);
        Assert.Equal("reauth_required", await LaneC.CodeAsync(r));
        await LaneC.ReauthAsync(owner);
        Assert.Equal(HttpStatusCode.UnprocessableContent, (await owner.PostAsync("/api/webhooks/hooks", body with { url = "https://192.168.1.10/api/webhooks/1/x" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableContent, (await owner.PostAsync("/api/webhooks/hooks", body with { events = new[] { "nope.event" } })).StatusCode);
        r = await owner.PostAsync("/api/webhooks/hooks", body);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var id = (await LaneC.JsonAsync(r))!["hook"]!["id"]!.GetValue<string>();

        // nobody gets the URL back, not even the owner; the file is the only copy
        var list = await (await admin.GetAsync("/api/webhooks/hooks")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("abcdefghijklmnop", list);
        Assert.Contains("/123456789012345678/***", list);
        Assert.Contains("abcdefghijklmnop", File.ReadAllText(Path.Combine(c.H.DataDir, WebhookStore.File)));

        // the test button and the chosen events reach the (fake) sender
        Assert.Equal(HttpStatusCode.Accepted, (await owner.PostAsync($"/api/webhooks/hooks/{id}/test", null)).StatusCode);
        c.PushState("""{"players":[{"name":"Ann"}]}""");
        c.PushState("""{"players":[{"name":"Ann"},{"name":"Bob"}]}""");
        c.PushState("""{"players":[{"name":"Bob"}]}""");
        await Eventually.True(() => c.Transport.Count >= 3, 5000, "three sends");
        string[] sent;
        lock (c.Transport.Sent) sent = [.. c.Transport.Sent.Select(s => Encoding.UTF8.GetString(s.Body))];
        Assert.Contains(sent, s => s.Contains("webhook.test"));
        Assert.Contains(sent, s => s.Contains("player.join") && s.Contains("Bob"));
        Assert.Contains(sent, s => s.Contains("player.leave") && s.Contains("Ann"));
        Assert.All(c.Transport.Sent, s => Assert.Equal("discord.com", s.Url.Host));

        // admin actions through the audit tap (not webhook edits themselves)
        c.H.Host.Services.Get<IAuditSink>().Record(new AuditEntry(DateTimeOffset.UtcNow, "u", "adam", "players.kick", new JsonObject { ["secret"] = "x" }, true, null, 1, null));
        await Eventually.True(() => c.Transport.Sent.Any(s => Encoding.UTF8.GetString(s.Body).Contains("players.kick")), 5000, "admin.action");
        Assert.DoesNotContain(c.Transport.Sent, s => Encoding.UTF8.GetString(s.Body).Contains("\"secret\""));

        // keep-url edit, then delete
        Assert.Equal(HttpStatusCode.OK, (await LaneC.PutAsync(owner, $"/api/webhooks/hooks/{id}", new { enabled = false })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableContent, (await LaneC.PutAsync(owner, $"/api/webhooks/hooks/{id}", new { kind = "json" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.Http.DeleteAsync($"api/webhooks/hooks/{id}")).StatusCode);
        Assert.Empty(c.Webhooks.Store!.All);
        var deliveries = await (await admin.GetAsync("/api/webhooks/deliveries")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("abcdefghijklmnop", deliveries);
    }

    /// <summary>Integration: admin.action taps the one shared audit log, so records that never touch the IAuditSink
    /// service (sign-ins, user changes and KAP-socket ops record on AuthService.Audit directly) reach webhooks too, with
    /// only the account and op names; the tap is detached when the panel stops.</summary>
    [Fact]
    public async Task Admin_action_sees_records_from_every_path_without_arguments()
    {
        var c = await LaneC.StartAsync();
        AuditLog log;
        try
        {
            using var owner = await c.UserAsync("olive", BuiltinRoles.Owner);
            await LaneC.ReauthAsync(owner);
            var r = await owner.PostAsync("/api/webhooks/hooks", new { name = "Audit", kind = "discord", url = Discord, events = new[] { "admin.action" } });
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
            log = c.H.Host.Services.Get<AuditLog>();
            Assert.Equal(1, log.TapCount);
            Assert.Same(log, c.H.Host.Services.Get<IAuditSink>()); // one sink: the service is the log itself, not a wrapper

            // a real sign-in (AuthService records it on its AuditLog directly)
            using var sam = await c.UserAsync("sam", BuiltinRoles.Admin);
            // a KAP-socket write and a user change, recorded the way KapGateway and UserRoutes do
            var auth = c.H.Host.Services.Get<AuthService>();
            auth.Audit.Record(new AuditEntry(DateTimeOffset.UtcNow, "u1", "sam", "kap.command_run", new JsonObject { ["command"] = "give", ["argc"] = 2, ["token"] = "s3cr3t-arg" }, true, null, 1, IPAddress.Parse("203.0.113.9")));
            auth.Audit.Record(new AuditEntry(DateTimeOffset.UtcNow, "u2", "olive", "users.update", new JsonObject { ["account"] = "a1" }, true, null, 1, null));
            // refused calls stay out (a password guesser must not fill the hooks)
            auth.Audit.Record(new AuditEntry(DateTimeOffset.UtcNow, "", "mallory", "auth.login", null, false, "wrong password", 1, null));

            static bool Has(LaneC c, string s)
            {
                lock (c.Transport.Sent) return c.Transport.Sent.Any(x => Encoding.UTF8.GetString(x.Body).Contains(s));
            }
            await Eventually.True(() => Has(c, "auth.login") && Has(c, "kap.command_run") && Has(c, "users.update"), 5000, "admin.action from every path");
            string[] sent;
            lock (c.Transport.Sent) sent = [.. c.Transport.Sent.Select(s => Encoding.UTF8.GetString(s.Body))];
            Assert.DoesNotContain(sent, s => s.Contains("s3cr3t-arg") || s.Contains("203.0.113.9") || s.Contains("give") || s.Contains("mallory"));
            Assert.DoesNotContain(sent, s => s.Contains("webhooks.add"));
        }
        finally
        {
            await c.DisposeAsync();
        }
        Assert.Equal(0, log.TapCount);
    }
}
