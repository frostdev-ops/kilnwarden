// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using Kilnwarden.Auth;
using Kilnwarden.Hosting;
using Kilnwarden.Offline.Support;
using Kilnwarden.Relay;

namespace Kilnwarden.Offline.RelayClient;

/// <summary>Lane L1b (0.1.1): the relay's notices about this panel's name on the control link (a scheduled move or
/// release, which the owner can undo with the panel's relay key), the claim pages' own origin, and what the panel says
/// when its key waits for a name or the name has moved away (docs/protocol-relay-broker.md §1.1, §3.1).</summary>
[Collection("lane-c")]
public sealed class MoveNoticeTests
{
    private static RelayControl.Message Parse(string json) => RelayControl.Parse(Encoding.UTF8.GetBytes(json));

    private const string Fp = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void Announcements_still_open_streams_and_anything_else_that_is_no_notice_ends_the_link()
    {
        var a = Assert.IsType<RelayControl.Announce>(Parse("""{"id":"0123456789abcdef0123456789abcdef","ip":"203.0.113.5","port":50123}"""));
        Assert.Equal(("0123456789abcdef0123456789abcdef", IPAddress.Parse("203.0.113.5"), 50123), (a.Id, a.Peer, a.Port));
        Assert.IsType<RelayControl.Announce>(Parse("""{"ID":"0123456789abcdef0123456789abcdef","Ip":"2001:db8::1","PORT":1,"extra":true}"""));
        foreach (var bad in new[]
                 {
                     """{"id":"0123456789ABCDEF0123456789abcdef","ip":"203.0.113.5","port":1}""", """{"id":"short","ip":"203.0.113.5","port":1}""",
                     """{"id":"0123456789abcdef0123456789abcdef","ip":"nope","port":1}""", """{"id":"0123456789abcdef0123456789abcdef","ip":"203.0.113.5","port":0}""",
                     """{"id":"0123456789abcdef0123456789abcdef","ip":"203.0.113.5","port":70000}""", "[]", "null", "{", "\"x\"",
                 })
            Assert.Throws<IOException>(() => Parse(bad));
    }

    [Fact]
    public void A_name_notice_is_read_and_a_notice_this_version_does_not_know_is_ignored_never_ending_the_link()
    {
        // exactly what the relay sends (hosted kwa-relay hub.rs name_notice)
        var n = Assert.IsType<RelayControl.NameNotice>(Parse("""{"host":"alpha.kiln.nexus","notice":"name","scheduled":{"at":1764165600,"requested":1764079200,"to":"FP"}}""".Replace("FP", Fp)));
        Assert.Equal("alpha.kiln.nexus", n.Host);
        Assert.Equal(new RelayControl.Scheduled(Fp, DateTimeOffset.FromUnixTimeSeconds(1764079200), DateTimeOffset.FromUnixTimeSeconds(1764165600)), n.Change);
        Assert.False(n.Change!.IsRelease);
        var release = Assert.IsType<RelayControl.NameNotice>(Parse("""{"notice":"name","host":"alpha.kiln.nexus","scheduled":{"to":null,"requested":1,"at":86401}}"""));
        Assert.True(release.Change!.IsRelease);
        Assert.Null(Assert.IsType<RelayControl.NameNotice>(Parse("""{"notice":"name","host":"alpha.kiln.nexus","scheduled":null}""")).Change);
        Assert.Null(Assert.IsType<RelayControl.NameNotice>(Parse("""{"notice":"name","host":"alpha.kiln.nexus"}""")).Change);
        // the relay's last notice on a link whose name moved away (hub.rs done_notice)
        var done = Assert.IsType<RelayControl.NameNotice>(Parse("""{"done":{"at":1764165600,"to":"FP"},"host":"alpha.kiln.nexus","notice":"name","scheduled":null}""".Replace("FP", Fp)));
        Assert.Equal(new RelayControl.Done(Fp, DateTimeOffset.FromUnixTimeSeconds(1764165600)), done.Done);
        Assert.True(Assert.IsType<RelayControl.NameNotice>(Parse("""{"notice":"name","host":"a.kiln.nexus","scheduled":null,"done":{"to":null,"at":5}}""")).Done!.IsRelease);
        foreach (var ignored in new[]
                 {
                     """{"notice":"maintenance","at":1}""", """{"notice":7}""", """{"notice":"name"}""", """{"notice":"name","host":"alpha.kiln.nexus","scheduled":"soon"}""",
                     """{"notice":"name","host":"alpha.kiln.nexus","scheduled":{"to":"NOT-HEX","requested":1,"at":2}}""",
                     """{"notice":"name","host":"alpha.kiln.nexus","scheduled":{"to":null,"requested":"1","at":2}}""",
                     """{"notice":"name","host":"alpha.kiln.nexus","scheduled":{"to":null,"requested":1,"at":-5}}""",
                     """{"notice":"name","host":"alpha.kiln.nexus","scheduled":{"to":null,"requested":1}}""",
                     """{"notice":"name","host":"alpha.kiln.nexus","done":{"to":"x","at":5}}""", """{"notice":"name","host":"alpha.kiln.nexus","done":{"to":null}}""",
                     """{"notice":"name","host":"alpha.kiln.nexus","done":true}""",
                     // a future kind, as an announcement's look-alike: still a notice, still ignored
                     """{"notice":"later","id":"0123456789abcdef0123456789abcdef","ip":"203.0.113.5","port":1}""",
                 })
            Assert.IsType<RelayControl.Ignored>(Parse(ignored));
    }

    [Theory]
    [InlineData("myserver.kiln.nexus", "https://claim.kiln.nexus")]
    [InlineData("a-b.panel.test", "https://claim.panel.test")]
    [InlineData("x.deeper.zone.example", "https://claim.deeper.zone.example")]
    [InlineData("kiln.nexus", null)]
    [InlineData("MyServer.kiln.nexus", null)]
    [InlineData("", null)]
    [InlineData("not a name", null)]
    public void The_claim_pages_are_at_claim_dot_the_names_zone(string host, string? origin) => Assert.Equal(origin, RelayComponent.ClaimOrigin(host));

    [Fact]
    public void A_key_that_waits_for_a_name_and_a_name_that_moved_away_are_said_plainly()
    {
        var s = new RelaySettings { Enabled = true, Host = "alpha.kiln.nexus" };
        var at = DateTimeOffset.FromUnixTimeSeconds(1764165600);
        var waiting = RelayErrors.Explain(new RelayRefusedException(403, new IOException()) { MovingAt = at }, s);
        Assert.StartsWith("alpha.kiln.nexus is moving to this server's key: it switches on 2025-11-26 14:00 UTC", waiting);
        Assert.Contains("relay HTTP 403", waiting);
        var moved = RelayErrors.Explain(new RelayRefusedException(401, new IOException()), s, new RelayControl.Scheduled(Fp, at.AddDays(-1), at));
        Assert.StartsWith("alpha.kiln.nexus moved to another server's key on 2025-11-26 14:00 UTC", moved);
        var released = RelayErrors.Explain(new RelayRefusedException(403, new IOException()), s, new RelayControl.Scheduled(null, at.AddDays(-1), at));
        Assert.StartsWith("alpha.kiln.nexus was released on 2025-11-26 14:00 UTC", released);
        // without a move behind them, the 0.1.0 sentences stay
        Assert.StartsWith("the relay does not know alpha.kiln.nexus", RelayErrors.Explain(new RelayRefusedException(403, new IOException()), s));
        Assert.StartsWith("the relay refused this server's signature", RelayErrors.Explain(new RelayRefusedException(401, new IOException()), s));
    }

    [Fact]
    public async Task A_notice_shows_in_Panel_settings_and_the_console_and_is_logged_once_per_change()
    {
        var relay = new RelayComponent();
        await using var c = await LaneC.LaneC.StartAsync(extra: [relay]);
        using var owner = await c.UserAsync("olive", BuiltinRoles.Owner);
        using var admin = await c.UserAsync("adam", BuiltinRoles.Admin);
        await LaneC.LaneC.ReauthAsync(owner);
        var r = await LaneC.LaneC.PutAsync(owner, "/api/core/settings/files/relay", new { enabled = true, origin = "https://frostdev.io", host = "olive.kiln.nexus" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var settings = c.H.Host.Services.Get<Kilnwarden.Config.SettingsStore<RelaySettings>>().Current;
        var status = (await LaneC.LaneC.JsonAsync(await admin.GetAsync("/api/core/relay")))!;
        Assert.Null(status["scheduled"]);
        Assert.Equal("https://claim.kiln.nexus/", status["namesPage"]!.GetValue<string>());

        var at = DateTimeOffset.UtcNow.AddHours(24);
        var move = new RelayControl.Scheduled(Fp, at.AddHours(-24), at);
        relay.TakeNotice(new RelayControl.NameNotice("olive.kiln.nexus", move), settings);
        relay.TakeNotice(new RelayControl.NameNotice("olive.kiln.nexus", move), settings);
        relay.TakeNotice(new RelayControl.NameNotice("other.kiln.nexus", null), settings);
        status = (await LaneC.LaneC.JsonAsync(await admin.GetAsync("/api/core/relay")))!;
        Assert.Equal(Fp, status["scheduled"]!["to"]!.GetValue<string>());
        Assert.Equal(at.ToUnixTimeSeconds(), DateTimeOffset.Parse(status["scheduled"]!["at"]!.GetValue<string>()).ToUnixTimeSeconds());
        Assert.Single(c.H.Log.Lines, l => l.Contains("olive.kiln.nexus is scheduled to move to another server's key on") && l.Contains("Undo"));
        Assert.Contains("a move to another server's key is scheduled for", ConsoleCommands.Status(c.H.Host));

        relay.TakeNotice(new RelayControl.NameNotice("olive.kiln.nexus", null), settings);
        status = (await LaneC.LaneC.JsonAsync(await admin.GetAsync("/api/core/relay")))!;
        Assert.Null(status["scheduled"]);
        Assert.Contains(c.H.Log.Lines, l => l.Contains("nothing is scheduled for olive.kiln.nexus any more"));
        Assert.DoesNotContain("is scheduled for", ConsoleCommands.Status(c.H.Host));
        relay.TakeNotice(new RelayControl.NameNotice("olive.kiln.nexus", new RelayControl.Scheduled(null, at.AddHours(-24), at)), settings);
        Assert.Contains(c.H.Log.Lines, l => l.Contains("olive.kiln.nexus is scheduled to be released on"));
        // a change whose time passed while the link was down: no banner, no Undo that can't work any more
        relay.TakeNotice(new RelayControl.NameNotice("olive.kiln.nexus", new RelayControl.Scheduled(Fp, at.AddHours(-48), DateTimeOffset.UtcNow.AddMinutes(-1))), settings);
        Assert.Null((await LaneC.LaneC.JsonAsync(await admin.GetAsync("/api/core/relay")))!["scheduled"]);
        Assert.DoesNotContain("is scheduled for", ConsoleCommands.Status(c.H.Host));
        // the relay's last notice: the name moved away; nothing is scheduled any more, and kiln.log says what happened
        relay.TakeNotice(new RelayControl.NameNotice("olive.kiln.nexus", null, new RelayControl.Done(Fp, at)), settings);
        Assert.Null((await LaneC.LaneC.JsonAsync(await admin.GetAsync("/api/core/relay")))!["scheduled"]);
        Assert.Contains(c.H.Log.Lines, l => l.Contains("olive.kiln.nexus moved to another server's key on") && l.Contains("operator"));
    }

    [Fact]
    public async Task The_undo_needs_the_owner_a_recent_proof_and_hosted_access_on_and_says_when_the_relay_does_not_answer()
    {
        await using var c = await LaneC.LaneC.StartAsync(extra: [new RelayComponent()]);
        using var owner = await c.UserAsync("olive", BuiltinRoles.Owner);
        using var admin = await c.UserAsync("adam", BuiltinRoles.Admin);
        using var fresh = await c.UserAsync("oscar", BuiltinRoles.Owner);
        await LaneC.LaneC.ReauthAsync(owner);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync("/api/core/relay/move/undo", new { })).StatusCode);
        Assert.Equal("reauth_required", await LaneC.LaneC.CodeAsync(await fresh.PostAsync("/api/core/relay/move/undo", new { })));
        var r = await owner.PostAsync("/api/core/relay/move/undo", new { });
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Contains("hosted access is off", await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        // a relay that is not there: the undo says so, and the audit log has the attempt
        r = await LaneC.LaneC.PutAsync(owner, "/api/core/settings/files/relay", new { enabled = true, origin = "https://127.0.0.1:9", host = "olive.kiln.nexus" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        r = await owner.PostAsync("/api/core/relay/move/undo", new { });
        Assert.Equal(HttpStatusCode.BadGateway, r.StatusCode);
        Assert.Contains("no answer from the relay", await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        await Eventually.True(() => c.H.Host.Services.Get<AuditLog>().Read(50).Any(e => e.GetProperty("op").GetString() == "core.relay.move_undo"), 3000, "audited");
    }
}
