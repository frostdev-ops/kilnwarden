// SPDX-License-Identifier: MIT
// The panel's public address for the Kiln Launcher: launcher-endpoint.json names https://<name>.kiln.nexus/ (or an own
// address) only while it really works, which is the wizard's "Ready": the relay connected, the name claimed, the trusted
// certificate installed and served, and the panel's own check of that address passing for that certificate. It names
// none before the owner exists or while the launcher's login is off, and the name goes as soon as any of those stops.
// The check backs off while it keeps passing (RecheckScheduleTests) and is not repeated while nothing could use the
// address. The launcher's one-time login works at that address (through the relay); before the owner exists it grants
// nothing there.
using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Config;
using Kilnwarden.Hosting;
using Kilnwarden.Offline.Acme;
using Kilnwarden.Offline.Support;
using Kilnwarden.Relay;
using Kilnwarden.Setup;
using Kilnwarden.Tls.Acme;

namespace Kilnwarden.Offline.Setup;

[Collection("setup")]
public sealed class PublicAddressTests
{
    private const string Host = "alpha.kiln.nexus";
    private static readonly AcmePolling Fast = new(TimeSpan.FromMilliseconds(20), 50, TimeSpan.FromSeconds(10));

    private sealed class Rig : IAsyncDisposable
    {
        public LaneHost H = null!;
        public FakeRelay Relay = new();
        public MockAcmeIssuer Ca = null!;
        public SetupComponent Setup = null!;
        public FakeKap? Kap;
        public readonly ConcurrentDictionary<string, byte> Logins = new();
        public volatile bool Reachable = true;
        /// <summary>Address checks started so far.</summary>
        public int Probes;
        /// <summary>While set, an address check waits for it before it connects.</summary>
        public volatile TaskCompletionSource? Hold;
        public AcmeManager Acme => H.Host.Services.Get<AcmeManager>();

        public async ValueTask DisposeAsync()
        {
            await H.DisposeAsync();
            Kap?.Dispose();
            Ca.Dispose();
        }

        public JsonObject? Endpoint()
        {
            try { return H.Data.Exists("launcher-endpoint.json") ? H.Data.ReadJson<JsonObject>("launcher-endpoint.json") : null; }
            catch (Exception e) when (e is IOException or System.Text.Json.JsonException) { return null; }
        }

        public string? Public() => Endpoint()?["public"]?.GetValue<string>();
    }

    /// <summary>A browser that reaches the panel through the relay: its bytes go in as a relayed stream announced from a
    /// public address.</summary>
    private static async Task<Stream> ThroughRelay(WebAdminHost host)
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var c = new TcpClient();
        var accept = l.AcceptTcpClientAsync();
        await c.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)l.LocalEndpoint).Port);
        var serverEnd = (await accept).GetStream();
        l.Stop();
        _ = host.Server!.AdmitRelayAsync(serverEnd, new IPEndPoint(IPAddress.Parse("8.8.8.8"), 50123));
        return c.GetStream();
    }

    private static async Task<Stream> Direct(int port)
    {
        var c = new TcpClient();
        await c.ConnectAsync(IPAddress.Loopback, port);
        return c.GetStream();
    }

    /// <summary>One POST with a JSON body at https://<see cref="Host"/>, through the relay or straight to this machine.</summary>
    private static async Task<RawResponse> PostAsync(Stream transport, string path, string body, string? cookie = null)
    {
        await using var ssl = new SslStream(transport, false, (_, _, _, _) => true);
        await ssl.AuthenticateAsClientAsync(Host);
        return await ServerFixture.SendAsync(ssl, $"POST {path} HTTP/1.1\r\nHost: {Host}\r\nOrigin: https://{Host}\r\n" +
                                                  (cookie is null ? "" : $"Cookie: {cookie}\r\n") +
                                                  $"Content-Type: application/json\r\nContent-Length: {body.Length}\r\n\r\n{body}");
    }

    /// <param name="probeDirect">The address check goes straight to the panel's port (an own address) instead of through
    /// the relay (hosted access).</param>
    /// <param name="recheck">The address check's first interval (then twice that, four times, at most <paramref name="longest"/>,
    /// by default six times the first, as 5 and 30 minutes).</param>
    /// <param name="grace">How long past its recheck a passed check still counts for the launcher's address.</param>
    private static async Task<Rig> StartAsync(bool kap = false, bool probeDirect = false, TimeSpan? recheck = null, Action<LaneHost, ServerSettings>? before = null,
        TimeSpan? longest = null, TimeSpan? grace = null)
    {
        var rig = new Rig();
        AcmeManager? m = null;
        rig.Ca = new MockAcmeIssuer(() => m?.Responder?.Port ?? 0);
        WebAdminHost? host = null;
        var probe = new SelfProbe
        {
            Connect = async (_, _, _) =>
            {
                Interlocked.Increment(ref rig.Probes);
                if (rig.Hold is { } hold) await hold.Task;
                return !rig.Reachable ? throw new SocketException((int)SocketError.ConnectionRefused)
                    : probeDirect ? await Direct(host!.Server!.LocalEndPoint.Port) : await ThroughRelay(host!);
            },
            Validate = (_, _, _, _) => true,
        };
        rig.Setup = new SetupComponent(new SetupOptions
        {
            Probe = probe, ProbeEvery = TimeSpan.FromMilliseconds(100), RecheckEvery = recheck, RecheckLongest = longest, RecheckGrace = grace,
        });
        rig.H = await LaneHost.StartAsync(() =>
        [
            new AcmeComponent(new AcmeOptions { Time = TimeProvider.System, Http = new HttpMessageInvoker(rig.Ca, disposeHandler: false), Polling = Fast, FirstCheck = TimeSpan.FromHours(1), Period = TimeSpan.FromHours(1) }),
            rig.Relay, rig.Setup,
        ], (h, s) =>
        {
            if (kap)
                rig.Kap = new FakeKap(Path.Combine(h.Tmp.Path, "kiln", "data", "kiln-admin"))
                {
                    Answer = req =>
                    {
                        var op = req["op"]!.GetValue<string>();
                        if (op != "login_redeem") return new JsonObject { ["t"] = "res", ["ok"] = true, ["op"] = op };
                        // single use, as Kiln.Admin's: a secret works once
                        return rig.Logins.TryRemove(req["args"]!["secret"]!.GetValue<string>(), out _)
                            ? new JsonObject { ["t"] = "res", ["ok"] = true, ["op"] = op, ["data"] = new JsonObject { ["role"] = "owner", ["mintedBy"] = "kiln-launcher/test" } }
                            : new JsonObject { ["t"] = "res", ["ok"] = false, ["op"] = op, ["code"] = "op_failed", ["error"] = "no such login" };
                    },
                };
            s.Tls.Mode = TlsMode.SelfSigned;
            s.Tls.Engine = TlsEngine.SslStream;
            h.Data.WriteJson("relay.json", new RelaySettings { Enabled = false, Origin = MockAcmeIssuer.Relay });
            h.Data.WriteJson("acme.json", new AcmeSettings { Directory = MockAcmeIssuer.Directory, Dns01PropagationSeconds = 5 });
            before?.Invoke(h, s);
        });
        host = rig.H.Host;
        m = rig.Acme;
        return rig;
    }

    /// <summary>Hosted access for alpha, from the console (the same settings as the wizard's), up to the wizard's "Ready".</summary>
    private static async Task MakeHostedReadyAsync(Rig rig)
    {
        ConsoleCommands.Setup(rig.H.Host, "hosted", "alpha");
        using (var key = new RelayKey(rig.H.Data)) rig.Ca.DnsPublicKey = File.ReadAllText(Path.Combine(rig.H.DataDir, "relay-public.pem"));
        rig.Relay.Set("connected");
        await Eventually.True(() => rig.Acme.Good is not null, 15000, "the certificate");
        await Eventually.True(() => rig.Setup.HostedSteps(out _)!.All(s => s.State == "done"), 8000, "the wizard's Ready");
    }

    private static async Task OwnerAsync(Rig rig)
    {
        await rig.H.AccountAsync("boss", BuiltinRoles.Owner);
        await Eventually.True(() => rig.H.Host.Services.Get<ISetupState>().OwnerExists, 2000, "owner");
    }

    /// <summary>auth.json launcherLogin, as the owner edits it (the file is read again within 2 s).</summary>
    private static async Task LauncherLoginAsync(Rig rig, bool on)
    {
        var auth = rig.H.Data.ReadJson<JsonObject>("auth.json")!;
        auth["launcherLogin"] = on;
        rig.H.Data.WriteJson("auth.json", auth);
        await Eventually.True(() => rig.H.Auth.Settings.LauncherLogin == on, 8000, $"auth.json launcherLogin {on}");
    }

    [Fact]
    public async Task The_hosted_address_is_in_the_launchers_file_only_while_it_really_works()
    {
        await using var rig = await StartAsync();
        var h = rig.H;
        await Eventually.True(() => rig.Endpoint() is not null, 5000, "launcher-endpoint.json");
        Assert.Null(rig.Endpoint()!["public"]);
        await OwnerAsync(rig);
        await MakeHostedReadyAsync(rig);

        await Eventually.True(() => rig.Public() is not null, 5000, "the hosted address in the launcher's file");
        var ep = rig.Endpoint()!;
        Assert.Equal($"https://{Host}/", ep["public"]!.GetValue<string>());
        Assert.Equal(rig.Acme.Good!.Info.Sha256, ep["publicSha256"]!.GetValue<string>());
        Assert.Equal(h.Host.Server!.Tls!.Certificate.Sha256, ep["publicSha256"]!.GetValue<string>());
        // format 1 as before: an older launcher reads the same fields (this machine's address and its pin) and ignores
        // the new ones
        Assert.Equal(1, ep["format"]!.GetValue<int>());
        Assert.Equal(("127.0.0.1", h.Port), (ep["host"]!.GetValue<string>(), ep["port"]!.GetValue<int>()));
        Assert.Equal(h.Host.Server!.Tls!.LoopbackCertificate.Sha256, ep["sha256"]!.GetValue<string>());
        Assert.NotEqual(ep["sha256"]!.GetValue<string>(), ep["publicSha256"]!.GetValue<string>());

        // the relay link drops: the name goes, this machine's address stays; back with the link
        rig.Relay.Set("waiting");
        await Eventually.True(() => rig.Public() is null, 5000, "gone when the relay link drops");
        Assert.Equal(h.Port, rig.Endpoint()!["port"]!.GetValue<int>());
        rig.Relay.Set("connected");
        await Eventually.True(() => rig.Public() is not null, 5000, "back with the relay link");

        // the name moved to another server's key: gone
        rig.Relay.Set("error", "moved", "alpha.kiln.nexus moved to another server's key");
        await Eventually.True(() => rig.Public() is null, 5000, "gone when the name is not this server's");
        rig.Relay.Set("connected");
        await Eventually.True(() => rig.Public() is not null, 5000, "back");

        // the panel stops serving the name's certificate (tls.mode back to self-signed): gone at once
        var settings = h.Data.ReadJson<ServerSettings>("settings.json")!;
        settings.Tls.Mode = TlsMode.SelfSigned;
        h.Host.Settings.Save(settings);
        await Eventually.True(() => h.Host.Server?.Tls?.Certificate.Source == "self-signed", 5000, "the replacement listener");
        await Eventually.True(() => rig.Public() is null, 5000, "gone with the certificate");
        Assert.NotNull(rig.Endpoint());
    }

    [Fact]
    public async Task Hosted_access_switched_off_takes_the_address_out()
    {
        await using var rig = await StartAsync();
        await OwnerAsync(rig);
        await MakeHostedReadyAsync(rig);
        await Eventually.True(() => rig.Public() is not null, 5000, "the hosted address");
        var relay = rig.H.Data.ReadJson<RelaySettings>("relay.json")!;
        relay.Enabled = false;
        rig.Relay.Settings.Save(relay);
        await Eventually.True(() => rig.Public() is null, 5000, "gone with hosted access off");
    }

    [Fact]
    public async Task A_check_of_the_address_that_fails_later_takes_it_out_until_it_passes_again()
    {
        await using var rig = await StartAsync(recheck: TimeSpan.FromMilliseconds(300));
        await OwnerAsync(rig);
        await MakeHostedReadyAsync(rig);
        await Eventually.True(() => rig.Public() is not null, 5000, "the hosted address");
        // the relay link and the certificate stay, but the address stops answering (the relay's browser side is down)
        rig.Reachable = false;
        await Eventually.True(() => rig.Public() is null, 5000, "gone once the check fails");
        rig.Reachable = true;
        // a failed check runs again 10 s later
        await Eventually.True(() => rig.Public() is not null, 15000, "back once it passes");
    }

    [Fact]
    public async Task Before_the_owner_exists_the_file_names_no_address_the_launcher_could_not_sign_in_at()
    {
        await using var rig = await StartAsync();
        // the console's hosted setup brings the name up before any owner exists (its hour)
        await MakeHostedReadyAsync(rig);
        await Task.Delay(1000); // ten rounds of the checks
        Assert.NotNull(rig.Endpoint());
        Assert.Null(rig.Public());
        await OwnerAsync(rig);
        await Eventually.True(() => rig.Public() == $"https://{Host}/", 5000, "named once the owner exists");
    }

    [Fact]
    public async Task An_own_address_with_its_trusted_certificate_is_named_too()
    {
        const string own = "panel.example.test";
        await using var rig = await StartAsync(probeDirect: true, before: (h, s) =>
        {
            s.Tls.Mode = TlsMode.Acme;
            s.AllowedHosts = [own];
            h.Data.WriteJson("acme.json", new AcmeSettings
            {
                Directory = MockAcmeIssuer.Directory, AcceptTerms = true, Names = [own], Http01Bind = "127.0.0.1", Http01Port = 0,
            });
            h.Data.WriteJson("setup.json", new SetupProgress { Route = "own", OwnHost = own, OwnPort = 8443 });
        });
        await OwnerAsync(rig);
        Assert.True(await rig.Acme.CheckAsync(false, CancellationToken.None), rig.H.Log.ToString());
        await Eventually.True(() => rig.Setup.OwnSteps(out _, out _)?.All(s => s.State == "done") == true, 8000, "the own address's Ready");
        await Eventually.True(() => rig.Public() is not null, 10000, "the own address in the launcher's file");
        var ep = rig.Endpoint()!;
        Assert.Equal($"https://{own}:8443/", ep["public"]!.GetValue<string>());
        Assert.Equal(rig.Acme.Good!.Info.Sha256, ep["publicSha256"]!.GetValue<string>());
        // the panel's certificate for the name stops being served: gone
        var settings = rig.H.Data.ReadJson<ServerSettings>("settings.json")!;
        settings.Tls.Mode = TlsMode.SelfSigned;
        rig.H.Host.Settings.Save(settings);
        await Eventually.True(() => rig.Public() is null, 5000, "gone with the certificate");
    }

    [Fact]
    public async Task The_launchers_one_time_login_signs_in_at_the_hosted_address_and_grants_no_setup_through_the_relay()
    {
        await using var rig = await StartAsync(kap: true, before: (_, s) => s.AllowedHosts = [Host]);
        var h = rig.H;
        await Eventually.True(() => h.Host.Services.Get<Kilnwarden.Kap.KapClient>().IsReady, 5000, "the KAP client");
        string Secret()
        {
            var s = Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            rig.Logins[s] = 1;
            return s;
        }

        // before the owner exists: through the relay, a launcher's login grants no setup token (setup links work on the
        // server's own computer only), and the secret is used up
        var early = Secret();
        var r = await PostAsync(await ThroughRelay(h.Host), "/api/auth/launcher", $"{{\"secret\":\"{early}\"}}");
        Assert.Equal(403, r.Status);
        Assert.Contains("launcher_setup_this_machine", r.Body);
        Assert.DoesNotContain("setupToken", r.Body);
        Assert.False(rig.Logins.ContainsKey(early));
        Assert.False(h.Auth.OwnerExists);
        // on the server's own computer it still does, as before
        r = await PostAsync(await Direct(h.Port), "/api/auth/launcher", $"{{\"secret\":\"{Secret()}\"}}");
        Assert.Equal(200, r.Status);
        Assert.Contains("\"setupToken\"", r.Body);

        // with the owner: the launcher's login signs the owner in at the hosted address, once
        await OwnerAsync(rig);
        var good = Secret();
        r = await PostAsync(await ThroughRelay(h.Host), "/api/auth/launcher", $"{{\"secret\":\"{good}\"}}");
        Assert.Equal(200, r.Status);
        var user = JsonNode.Parse(r.Body)!["user"]!;
        Assert.Equal(("boss", true, "launcher"), (user["name"]!.GetValue<string>(), user["owner"]!.GetValue<bool>(), user["session"]!["via"]!.GetValue<string>()));
        Assert.Contains("__Host-wa-session=", r.Headers["Set-Cookie"]);
        Assert.Contains("one-time login from the launcher (kiln-launcher/test) as 'boss' from 8.8.8.8", h.Log.ToString());
        r = await PostAsync(await ThroughRelay(h.Host), "/api/auth/launcher", $"{{\"secret\":\"{good}\"}}");
        Assert.Equal(401, r.Status);
    }

    [Fact]
    public async Task While_there_is_no_owner_or_the_launchers_login_is_off_a_passed_check_is_not_run_again()
    {
        await using var rig = await StartAsync(recheck: TimeSpan.FromMilliseconds(100), grace: TimeSpan.FromMilliseconds(250));
        // the console's hosted setup brings the name up before any owner exists: the check passes (the wizard's Ready) ...
        await MakeHostedReadyAsync(rig);
        await Task.Delay(300); // a check that had started already ends
        var once = rig.Probes;
        Assert.True(once >= 1);
        // ... and does not run again while there is no owner: nothing needs it, the launcher's file names no address yet
        await Task.Delay(1500);
        Assert.Equal(once, rig.Probes);
        Assert.Null(rig.Public());
        Assert.All(rig.Setup.HostedSteps(out _)!, s => Assert.Equal("done", s.State));
        // with the owner it runs again, and the address is named
        await OwnerAsync(rig);
        await Eventually.True(() => rig.Probes > once, 5000, "the check again once the owner exists");
        await Eventually.True(() => rig.Public() is not null, 5000, "the hosted address");

        // the launcher's login switched off (auth.json launcherLogin): the address goes, and the check stops again
        await LauncherLoginAsync(rig, false);
        await Eventually.True(() => rig.Public() is null, 5000, "gone with the launcher's login off");
        await Task.Delay(300);
        var off = rig.Probes;
        await Task.Delay(1500);
        Assert.Equal(off, rig.Probes);

        // back on while the check hangs: the pass from before the pause does not count any more, so the address is named
        // only once a new check passes
        rig.Hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await LauncherLoginAsync(rig, true);
        await Eventually.True(() => rig.Probes > off, 5000, "a new check");
        await Task.Delay(500);
        Assert.Null(rig.Public());
        rig.Hold.SetResult();
        rig.Hold = null;
        await Eventually.True(() => rig.Public() is not null, 5000, "named once the new check passed");
    }

    [Fact]
    public async Task The_address_check_backs_off_while_it_passes_and_starts_again_after_a_failure_or_a_change_of_relay_link_or_listener()
    {
        // 300 ms, then 600 ms, 1.2 s, 2.4 s ... (only the start of the schedule matters here)
        var first = TimeSpan.FromMilliseconds(300);
        var grown = first * 4;
        await using var rig = await StartAsync(recheck: first, longest: TimeSpan.FromHours(1));
        await OwnerAsync(rig);
        await MakeHostedReadyAsync(rig);
        await Eventually.True(() => rig.Setup.RecheckInterval >= grown, 10000, "the schedule grown while the check passes");
        Assert.NotNull(rig.Public());

        // the relay link drops and comes back: the first interval again
        rig.Relay.Set("waiting");
        rig.Relay.Set("connected");
        Assert.Equal(first, rig.Setup.RecheckInterval);
        await Eventually.True(() => rig.Setup.RecheckInterval >= grown, 10000, "grown again after the relay link's change");

        // a new listener (another port, the same certificate): the first interval again
        var before = rig.H.Host.Server;
        var sha = before!.Tls!.Certificate.Sha256;
        var settings = rig.H.Data.ReadJson<ServerSettings>("settings.json")!;
        settings.Port = LaneHost.FreePort();
        rig.H.Host.Settings.Save(settings);
        await Eventually.True(() => rig.H.Host.Server is { } now && !ReferenceEquals(now, before), 5000, "the new listener");
        Assert.Equal(sha, rig.H.Host.Server!.Tls!.Certificate.Sha256);
        Assert.Equal(first, rig.Setup.RecheckInterval);
        await Eventually.True(() => rig.Setup.RecheckInterval >= grown, 10000, "grown again after the new listener");

        // a failed check: the first interval again, and the address goes
        rig.Reachable = false;
        await Eventually.True(() => rig.Public() is null, 10000, "gone once the check fails");
        Assert.Equal(first, rig.Setup.RecheckInterval);
    }

    [Fact]
    public async Task A_recheck_that_never_ends_takes_the_address_out_once_the_pass_is_overdue_by_the_grace()
    {
        // every 300 ms and no longer, a pass counting 400 ms past its recheck (in place of 5 to 30 minutes and a minute)
        var every = TimeSpan.FromMilliseconds(300);
        await using var rig = await StartAsync(recheck: every, longest: every, grace: TimeSpan.FromMilliseconds(400));
        await OwnerAsync(rig);
        await MakeHostedReadyAsync(rig);
        await Eventually.True(() => rig.Public() is not null, 5000, "the hosted address");
        // the next recheck gets stuck before it connects (its own 12-second limit is far off): no check fails, yet the
        // address goes once the last pass no longer counts, so the docs' worst case holds even then
        rig.Hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var before = rig.Probes;
        await Eventually.True(() => rig.Probes > before, 5000, "a recheck started");
        await Eventually.True(() => rig.Public() is null, 5000, "gone while the recheck hangs");
        Assert.All(rig.Setup.HostedSteps(out _)!, s => Assert.Equal("done", s.State));
        rig.Hold.SetResult();
        rig.Hold = null;
        await Eventually.True(() => rig.Public() is not null, 5000, "back once that check passes");
    }
}
