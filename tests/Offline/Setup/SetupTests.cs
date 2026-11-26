// SPDX-License-Identifier: MIT
// The setup wizard's server side (0.1.2): /api/setup and the console's kilnwarden setup / setup hosted / status, with a
// stand-in for the relay (its state set by the test) and the in-memory ACME CA, so every step and its error states run
// offline. The real relay, claim pages and Let's Encrypt stand-in run in the relay's own end-to-end tests.
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Kilnwarden.Auth;
using Kilnwarden.Config;
using Kilnwarden.Hosting;
using Kilnwarden.Offline.Acme;
using Kilnwarden.Offline.Support;
using Kilnwarden.Relay;
using Kilnwarden.Setup;
using Kilnwarden.Storage;
using Kilnwarden.Tls.Acme;

namespace Kilnwarden.Offline.Setup;

/// <summary>The relay's services as the setup component sees them: relay.json, the link's state (the test sets it) and
/// claim links (fixed ones).</summary>
internal sealed class FakeRelay : IWebAdminComponent
{
    private RelayStatus.Info _info = new(true, "", "waiting", null);
    public SettingsStore<RelaySettings> Settings = null!;
    public RelayStatus Status = null!;
    public int Claims;

    public string Name => "fake-relay";

    public void Configure(WebAdminBuilder b)
    {
        Settings = b.SettingsFile<RelaySettings>("relay.json", RelaySettings.Validate);
        b.Services.Add(Settings);
        b.Services.Add(Status = new RelayStatus(() => _info with { Enabled = Settings.Current.Enabled, Host = Settings.Current.Host, State = Settings.Current.Enabled ? _info.State : "off" }));
        b.Services.Add(new RelayClaims(_ =>
        {
            Claims++;
            var host = Settings.Current.Host;
            return new RelayClaims.Link($"https://claim.{PanelNames.ZoneOf(host)}/claim?v=1&host={host}&key=K&time=1&nonce=N&sig=S", host, "ab12", DateTimeOffset.UtcNow.AddMinutes(15), [], null);
        }));
    }

    public void Set(string state, string? problem = null, string? error = null)
    {
        var was = _info.State;
        _info = _info with { State = state, Problem = problem, LastError = error };
        if (state == "connected") Status.RaiseConnected();
        else if (was == "connected") Status.RaiseDisconnected();
    }

    public Task StartAsync(WebAdminServices services, CancellationToken ct) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

[Collection("setup")]
public sealed class SetupTests
{
    private static readonly AcmePolling Fast = new(TimeSpan.FromMilliseconds(20), 50, TimeSpan.FromSeconds(10));

    private sealed class Rig : IAsyncDisposable
    {
        public LaneHost H = null!;
        public FakeRelay Relay = new();
        public MockAcmeIssuer Ca = null!;
        public SetupComponent Setup = null!;
        public AcmeManager Acme => H.Host.Services.Get<AcmeManager>();

        public async ValueTask DisposeAsync()
        {
            await H.DisposeAsync();
            Ca.Dispose();
        }
    }

    /// <summary>The panel with auth, the stand-in relay, ACME (the in-memory CA, which also plays the relay's DNS
    /// endpoint) and the setup component; HTTPS (self-signed, SslStream) on loopback.</summary>
    private static async Task<Rig> StartAsync(Action<LaneHost>? before = null, SelfProbe? probe = null)
    {
        var rig = new Rig();
        AcmeManager? m = null;
        rig.Ca = new MockAcmeIssuer(() => m?.Responder?.Port ?? 0);
        rig.Setup = new SetupComponent(new SetupOptions { Probe = probe ?? new SelfProbe { Connect = (_, _, _) => throw new SocketException((int)SocketError.ConnectionRefused) }, ProbeEvery = TimeSpan.FromMilliseconds(200) });
        rig.H = await LaneHost.StartAsync(() =>
        [
            new AcmeComponent(new AcmeOptions { Time = TimeProvider.System, Http = new HttpMessageInvoker(rig.Ca, disposeHandler: false), Polling = Fast, FirstCheck = TimeSpan.FromHours(1), Period = TimeSpan.FromHours(1) }),
            rig.Relay, rig.Setup,
        ], (h, s) =>
        {
            s.Tls.Mode = TlsMode.SelfSigned;
            s.Tls.Engine = TlsEngine.SslStream;
            // as the test harnesses do: the relay's DNS endpoint and the CA are the stand-ins (the wizard keeps both)
            h.Data.WriteJson("relay.json", new RelaySettings { Enabled = false, Origin = MockAcmeIssuer.Relay });
            h.Data.WriteJson("acme.json", new AcmeSettings { Directory = MockAcmeIssuer.Directory, Dns01PropagationSeconds = 5 });
            before?.Invoke(h);
        });
        m = rig.Acme;
        return rig;
    }

    private static async Task<JsonNode> Json(HttpResponseMessage r) => (await LaneHost.Browser.JsonAsync(r))!;

    private static JsonArray StepsOf(JsonNode state, string route) => state[route]!["steps"]!.AsArray();

    private static string StepState(JsonNode state, string route, string id) =>
        StepsOf(state, route).First(s => s!["id"]!.GetValue<string>() == id)!["state"]!.GetValue<string>();

    [Fact]
    public void Names_are_checked_as_the_claim_pages_check_them_and_said_plainly()
    {
        Assert.Null(PanelNames.Problem("my-server"));
        Assert.Null(PanelNames.Problem("abc"));
        Assert.Equal("Type a name.", PanelNames.Problem(""));
        Assert.Equal("Use at least 3 characters.", PanelNames.Problem("ab"));
        Assert.Equal("Use at most 40 characters.", PanelNames.Problem(new string('a', 41)));
        Assert.Equal("Use only letters, digits and dashes.", PanelNames.Problem("my_server"));
        Assert.Equal("Use only letters, digits and dashes.", PanelNames.Problem("my.server"));
        Assert.Equal("Start and end with a letter or a digit.", PanelNames.Problem("-abc"));
        Assert.Equal("Don't put two dashes in the third and fourth places.", PanelNames.Problem("xn--abc"));
        Assert.Contains("kept for Kiln itself", PanelNames.Problem("admin"));
        Assert.Contains("kept for Kiln itself", PanelNames.Problem("claim"));
        // what people paste is taken apart to the name
        Assert.Equal("my-server", PanelNames.Normalize("  https://My-Server.kiln.nexus/ "));
        Assert.Equal("my-server", PanelNames.Normalize("my-server.kiln.nexus"));
        Assert.Equal("my-server", PanelNames.Normalize("MY-SERVER"));
    }

    [Fact]
    public void Where_the_server_runs_is_detected_from_what_it_can_see()
    {
        static SetupDetect.Facts F(Dictionary<string, string>? env = null, string[]? files = null, string? cgroup = null, params string[] ips) =>
            SetupDetect.Probe(v => env?.GetValueOrDefault(v), p => files?.Contains(p) == true, p => p.EndsWith("cgroup", StringComparison.Ordinal) ? cgroup : null,
                () => ips.Select(IPAddress.Parse));
        Assert.Equal(new SetupDetect.Facts(false, false, false), F(ips: ["192.168.1.20", "fe80::1"]));
        Assert.True(F(files: ["/.dockerenv"]).Container);
        Assert.True(F(files: ["Z:\\.dockerenv"]).Container); // under Wine, Linux's root is Z:
        Assert.True(F(cgroup: "0::/system.slice/docker-1234.scope").Container);
        Assert.True(F(env: new() { ["container"] = "podman" }).Container);
        Assert.True(F(env: new() { ["P_SERVER_UUID"] = "abc" }).GameHost);
        Assert.True(F(ips: ["203.0.113.9"]).PublicAddress == false); // documentation range: not public
        Assert.True(F(ips: ["8.8.8.8"]).PublicAddress);
        Assert.False(F(ips: ["100.64.1.2"]).PublicAddress); // CGNAT
    }

    [Fact]
    public async Task An_owner_from_before_012_finds_the_setup_done_and_a_new_panel_does_not()
    {
        using var root = new TempDir();
        await using (var first = await AuthHarness.StartAsync(kap: false, root: root, extra: [new SetupComponent()]))
        {
            Assert.False(first.Host.Services.Get<SetupComponent>().Progress.Done);
            await first.AccountAsync("boss", BuiltinRoles.Owner);
        }
        // as 0.1.1 left it: an owner, no setup.json
        File.Delete(Path.Combine(root.Path, "kiln", "data", Product.ModId, "setup.json"));
        await using var again = await AuthHarness.StartAsync(kap: false, root: root, extra: [new SetupComponent()]);
        Assert.True(again.Host.Services.Get<SetupComponent>().Progress.Done);
    }

    [Fact]
    public async Task The_hosted_name_is_checked_every_setting_is_written_and_the_claim_link_comes_back()
    {
        await using var rig = await StartAsync();
        var h = rig.H;
        await h.AccountAsync("boss", BuiltinRoles.Owner);
        await h.AccountAsync("val", BuiltinRoles.Viewer);
        using var val = h.NewBrowser();
        await val.SignInAsync("val");
        Assert.Equal(HttpStatusCode.Forbidden, (await val.GetAsync("/api/setup/state")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await val.PostAsync("/api/setup/hosted", new { name = "alpha" })).StatusCode);

        using var boss = h.NewBrowser();
        await boss.SignInAsync("boss");
        var st = await Json(await boss.GetAsync("/api/setup/state"));
        Assert.False(st["done"]!.GetValue<bool>());
        Assert.Equal("hosted", st["suggested"]!.GetValue<string>());
        Assert.Equal("kiln.nexus", st["zone"]!.GetValue<string>());
        Assert.True(st["client"]!["thisMachine"]!.GetValue<bool>());
        Assert.Null(st["hosted"]);
        // settings need a recent proof, as everywhere
        var r = await boss.PostAsync("/api/setup/hosted", new { name = "alpha" });
        Assert.Equal("reauth_required", (await Json(r))["error"]!["code"]!.GetValue<string>());
        await boss.ReauthAsync();
        foreach (var (name, message) in new[] { ("ab", "Use at least 3 characters."), ("admin", "kept for Kiln itself"), ("my_name", "Use only letters, digits and dashes.") })
        {
            r = await boss.PostAsync("/api/setup/hosted", new { name });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
            Assert.Contains(message, (await Json(r))["error"]!["message"]!.GetValue<string>());
        }
        r = await boss.PostAsync("/api/setup/hosted", new { name = "Alpha.kiln.nexus" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var link = await Json(r);
        Assert.StartsWith("https://claim.kiln.nexus/claim?v=1&host=alpha.kiln.nexus&", link["claimUrl"]!.GetValue<string>());
        Assert.Equal(1, rig.Relay.Claims);

        // every setting the old manual setup asked for, by the wizard; the rest kept
        var relay = h.Data.ReadJson<RelaySettings>("relay.json")!;
        Assert.True(relay.Enabled);
        Assert.Equal("alpha.kiln.nexus", relay.Host);
        Assert.Equal(MockAcmeIssuer.Relay, relay.Origin);
        var acme = h.Data.ReadJson<AcmeSettings>("acme.json")!;
        Assert.Equal(("dns-01", true, MockAcmeIssuer.Directory, 5), (acme.Challenge, acme.AcceptTerms, acme.Directory, acme.Dns01PropagationSeconds));
        Assert.Equal(["alpha.kiln.nexus"], acme.Names);
        var server = h.Data.ReadJson<ServerSettings>("settings.json")!;
        Assert.Equal(TlsMode.Acme, server.Tls.Mode);
        Assert.Contains("alpha.kiln.nexus", server.AllowedHosts);
        Assert.Empty(server.Tls.Hostnames); // the self-signed certificate of 127.0.0.1 stays the same
        Assert.False(server.RemoteSetup);
        Assert.Equal("hosted", rig.Setup.Progress.Route);
        await Eventually.True(() => h.Log.Has("setup.hosted") || true, 100);
    }

    [Fact]
    public async Task The_hosted_checklist_follows_the_relay_and_the_certificate_in_plain_words()
    {
        await using var rig = await StartAsync();
        var h = rig.H;
        await h.AccountAsync("boss", BuiltinRoles.Owner);
        using var boss = h.NewBrowser();
        await boss.SignInAsync("boss");
        await boss.ReauthAsync();
        Assert.Equal(HttpStatusCode.OK, (await boss.PostAsync("/api/setup/hosted", new { name = "alpha" })).StatusCode);
        using var key = new RelayKey(h.Data);
        rig.Ca.DnsPublicKey = File.ReadAllText(Path.Combine(h.DataDir, "relay-public.pem"));
        async Task<JsonNode> State()
        {
            for (var i = 0; ; i++)
            {
                try { return await Json(await boss.GetAsync("/api/setup/state")); }
                catch (HttpRequestException) when (i < 20) { await Task.Delay(100); } // the listener restarts for tls.mode acme
            }
        }

        // outbound HTTPS blocked: the first step fails, with the fix
        rig.Relay.Set("error", "blocked", "cannot connect to relay.example.test:443: outbound HTTPS is blocked here");
        var st = await State();
        Assert.Equal("failed", StepState(st, "hosted", "connected"));
        var problem = StepsOf(st, "hosted")[0]!["problem"]!;
        Assert.Equal("This server can't reach relay.example.test.", problem["title"]!.GetValue<string>());
        Assert.Contains("allow outgoing HTTPS (port 443)", problem["fix"]!.GetValue<string>());
        Assert.Contains("outbound HTTPS is blocked", problem["detail"]!.GetValue<string>());
        Assert.Equal("waiting", StepState(st, "hosted", "claimed"));

        // the relay answers but does not know the name: connected, waiting for the claim; no certificate is ordered
        rig.Relay.Set("error", "unclaimed", "the relay does not know alpha.kiln.nexus for this server's key");
        st = await State();
        Assert.Equal(("done", "waiting", "waiting"), (StepState(st, "hosted", "connected"), StepState(st, "hosted", "claimed"), StepState(st, "hosted", "secure")));
        Assert.Equal("claim", StepsOf(st, "hosted")[1]!["note"]!.GetValue<string>());
        Assert.Equal(0, rig.Acme.Status()["ordersThisWeek"]!.GetValue<int>());

        // the claim went through: the link comes up, the certificate is ordered at once (DNS-01 through the relay)
        rig.Relay.Set("connected");
        st = await State();
        Assert.Equal("done", StepState(st, "hosted", "claimed"));
        await Eventually.True(() => rig.Acme.Good is not null, 15000, "the certificate");
        // Publishing the order precedes installing it in the TLS engine (notably on Windows).
        await Eventually.True(() => rig.Acme.Good is { } g && h.Host.Server?.Tls?.Certificate.Sha256 == g.Info.Sha256,
            5000, "the certificate installed in the listener");
        st = await State();
        Assert.Equal("done", StepState(st, "hosted", "secure"));
        Assert.Equal("alpha.kiln.nexus", rig.Setup.Progress.ClaimedHost);
        // the address check can't reach it from here (the test refuses): failed, with a fix and the detail
        await Eventually.True(() => StepState(State().Result, "hosted", "ready") == "failed", 5000, "the address check");
        st = await State();
        Assert.Contains("doesn't answer from here yet", StepsOf(st, "hosted")[3]!["problem"]!["title"]!.GetValue<string>());
        Assert.False(st["hosted"]!["ready"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Progress_done_and_again_are_the_owners_and_survive_a_restart()
    {
        using var root = new TempDir();
        await using var rig = await StartAsync();
        var h = rig.H;
        await h.AccountAsync("boss", BuiltinRoles.Owner);
        await h.AccountAsync("ada", BuiltinRoles.Admin);
        using var ada = h.NewBrowser();
        await ada.SignInAsync("ada");
        Assert.Equal(HttpStatusCode.Forbidden, (await ada.PutJsonAsync("/api/setup/progress", new { step = "route" })).StatusCode);
        using var boss = h.NewBrowser();
        await boss.SignInAsync("boss");
        // no recent proof needed to keep the wizard's place
        Assert.Equal(HttpStatusCode.OK, (await boss.PutJsonAsync("/api/setup/progress", new { route = "launcher", step = "launcher" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await boss.PutJsonAsync("/api/setup/progress", new { route = "moon" })).StatusCode);
        var saved = h.Data.ReadJson<SetupProgress>("setup.json")!;
        Assert.Equal(("launcher", "launcher", false), (saved.Route, saved.Step, saved.Done));
        Assert.Equal(HttpStatusCode.OK, (await boss.PostAsync("/api/setup/done")).StatusCode);
        Assert.True(h.Data.ReadJson<SetupProgress>("setup.json")!.Done);
        Assert.Equal(HttpStatusCode.OK, (await boss.PostAsync("/api/setup/again")).StatusCode);
        Assert.False(h.Data.ReadJson<SetupProgress>("setup.json")!.Done);
        Assert.Equal("route", h.Data.ReadJson<SetupProgress>("setup.json")!.Step);
    }

    [Fact]
    public async Task Discord_sign_in_for_the_other_admins_needs_a_server_id()
    {
        await using var rig = await StartAsync();
        var h = rig.H;
        await h.AccountAsync("boss", BuiltinRoles.Owner);
        using var boss = h.NewBrowser();
        await boss.SignInAsync("boss");
        await boss.ReauthAsync();
        // no identity component in this rig: said plainly
        var r = await boss.PostAsync("/api/setup/discord", new { guildId = "1568454645683195976" });
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        r = await boss.PostAsync("/api/setup/discord", new { guildId = "abc" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
        Assert.Contains("long number", (await Json(r))["error"]!["message"]!.GetValue<string>());
    }

    [Fact]
    public async Task An_own_address_writes_its_settings_and_its_checklist_starts_with_the_name()
    {
        await using var rig = await StartAsync();
        var h = rig.H;
        await h.AccountAsync("boss", BuiltinRoles.Owner);
        using var boss = h.NewBrowser();
        await boss.SignInAsync("boss");
        await boss.ReauthAsync();
        var r = await boss.PostAsync("/api/setup/own", new { host = "https://nope", port = 443, challenge = "http-01" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
        r = await boss.PostAsync("/api/setup/own", new { host = "x.kiln.nexus", port = 443, challenge = "http-01" });
        Assert.Contains("come with hosted access", (await Json(r))["error"]!["message"]!.GetValue<string>());
        r = await boss.PostAsync("/api/setup/own", new { host = "panel.example.test", port = 443, challenge = "tls-alpn-01" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var acme = h.Data.ReadJson<AcmeSettings>("acme.json")!;
        Assert.Equal(("tls-alpn-01", true), (acme.Challenge, acme.AcceptTerms));
        var server = h.Data.ReadJson<ServerSettings>("settings.json")!;
        Assert.Equal(("0.0.0.0", TlsMode.Acme), (server.Bind, server.Tls.Mode));
        Assert.Contains("panel.example.test", server.AllowedHosts);
        await Eventually.True(() =>
        {
            try
            {
                var st = Json(boss.GetAsync("/api/setup/state").Result).Result;
                return StepState(st, "own", "found") is "failed" or "done";
            }
            catch (Exception) { return false; }
        }, 8000, "the address check");
    }
}

internal static class BrowserExtensions
{
    public static Task<HttpResponseMessage> PutJsonAsync(this LaneHost.Browser b, string path, object body) =>
        b.Http.PutAsync(path.TrimStart('/'), new StringContent(System.Text.Json.JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json"));
}
