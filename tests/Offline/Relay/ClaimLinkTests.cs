// SPDX-License-Identifier: MIT
using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Kilnwarden.Auth;
using Kilnwarden.Hosting;
using Kilnwarden.Offline.Support;
using Kilnwarden.Relay;
using Kilnwarden.Storage;

namespace Kilnwarden.Offline.RelayClient;

/// <summary>Claim links for the relay's Discord claim pages (lane L1, docs/protocol-relay-broker.md §1.1): Panel
/// settings > Hosted access > Claim this name, and the console's <c>kilnwarden claim</c>.</summary>
[Collection("lane-c")]
public sealed class ClaimLinkTests
{
    private static Dictionary<string, string> Query(string q) =>
        q.Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));

    [Fact]
    public void The_claim_link_is_the_relay_keys_signed_claim_for_one_name()
    {
        using var tmp = new TempDir();
        var data = new DataStore(tmp.Path);
        using var key = new RelayKey(data);
        var time = new LaneC.ManualTime(DateTimeOffset.FromUnixTimeSeconds(1_764_000_000));
        var raw = key.ClaimQuery("alpha.kiln.nexus", time);
        Assert.StartsWith("v=1&host=alpha.kiln.nexus&key=", raw);
        var q = Query(raw);
        Assert.Equal(["v", "host", "key", "time", "nonce", "sig"], q.Keys);
        Assert.Equal("1764000000", q["time"]);
        Assert.True(SecretToken.IsWellFormed(q["nonce"]));
        // the key is relay-public.pem's SPKI, its fingerprint the SHA-256 the relay and Panel settings show
        var pem = Encoding.ASCII.GetString(data.ReadBytes("relay-public.pem")!);
        using var pub = RSA.Create();
        pub.ImportFromPem(pem);
        var spki = Base64Url.DecodeFromChars(q["key"]);
        Assert.Equal(pub.ExportSubjectPublicKeyInfo(), spki);
        var fp = Convert.ToHexStringLower(SHA256.HashData(spki));
        Assert.Equal(fp, key.Fingerprint);
        Assert.Equal(fp, RelayComponent.PemFingerprint(pem));
        // RSASSA-PKCS1-v1_5 SHA-256 over the kind-claim block, as the relay verifies it (hosted kwa-common claim.rs)
        var block = Encoding.UTF8.GetBytes($"kwa-relay/1\nclaim\nalpha.kiln.nexus\n{fp}\n1764000000\n{q["nonce"]}");
        Assert.True(pub.VerifyData(block, Base64Url.DecodeFromChars(q["sig"]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        // never valid as another kind or for another name
        Assert.False(pub.VerifyData(Encoding.UTF8.GetBytes($"kwa-relay/1\ncontrol\nalpha.kiln.nexus\n{fp}\n1764000000\n{q["nonce"]}"),
            Base64Url.DecodeFromChars(q["sig"]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        Assert.NotEqual(q["nonce"], Query(key.ClaimQuery("alpha.kiln.nexus", time))["nonce"]);
    }

    [Fact]
    public async Task Claim_this_name_needs_the_owner_a_recent_proof_and_a_name_and_never_logs_the_link()
    {
        await using var c = await LaneC.LaneC.StartAsync(extra: [new RelayComponent()]);
        using var admin = await c.UserAsync("adam", BuiltinRoles.Admin);
        using var owner = await c.UserAsync("olive", BuiltinRoles.Owner);
        await LaneC.LaneC.ReauthAsync(owner);
        // no name set yet: say what to do
        var r = await owner.PostAsync("/api/core/relay/claim", new { });
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Contains("set the name first", await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.StartsWith("No claim link: set the name first", ConsoleCommands.Claim(c.H.Host));

        r = await LaneC.LaneC.PutAsync(owner, "/api/core/settings/files/relay", new { enabled = true, origin = "https://frostdev.io", host = "olive.kiln.nexus" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync("/api/core/relay/claim", new { })).StatusCode);
        using var fresh = await c.UserAsync("oscar", BuiltinRoles.Owner);
        Assert.Equal("reauth_required", await LaneC.LaneC.CodeAsync(await fresh.PostAsync("/api/core/relay/claim", new { })));

        r = await owner.PostAsync("/api/core/relay/claim", new { });
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var link = (await LaneC.LaneC.JsonAsync(r))!;
        var url = link["url"]!.GetValue<string>();
        // lane L1b: the claim pages have an origin of their own, claim.<the name's zone>
        Assert.StartsWith("https://claim.kiln.nexus/claim?v=1&host=olive.kiln.nexus&key=", url);
        Assert.Equal("olive.kiln.nexus", link["host"]!.GetValue<string>());
        var expires = DateTimeOffset.Parse(link["expires"]!.GetValue<string>());
        Assert.InRange(expires - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(16));
        // this test panel has TLS off: the link warns that the relay link would wait for it
        Assert.Contains(link["warnings"]!.AsArray(), w => w!.GetValue<string>().Contains("TLS"));
        // Panel settings shows the same fingerprint, and where "My panel name" is
        var status = (await LaneC.LaneC.JsonAsync(await admin.GetAsync("/api/core/relay")))!;
        Assert.Equal(link["fingerprint"]!.GetValue<string>(), status["fingerprint"]!.GetValue<string>());
        Assert.Equal("https://claim.kiln.nexus/", status["namesPage"]!.GetValue<string>());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Base64Url.DecodeFromChars(Query(url.Split('?', 2)[1])["key"]))), status["fingerprint"]!.GetValue<string>());

        var answer = ConsoleCommands.Claim(c.H.Host);
        Assert.StartsWith("Claim olive.kiln.nexus for this server", answer);
        Assert.Contains("works once, for 15 minutes: https://claim.kiln.nexus/claim?v=1&host=olive.kiln.nexus&key=", answer);
        Assert.Contains($"SHA-256 {status["fingerprint"]!.GetValue<string>()}", answer);
        Assert.DoesNotContain(c.H.Log.Lines, l => l.Contains("/claim?"));
        await Eventually.True(() => c.H.Host.Services.Get<AuditLog>().Read(50).Count(e => e.GetProperty("op").GetString() == "core.relay.claim_link") >= 2,
            3000, "both links audited");
    }

    [Fact]
    public async Task The_key_a_claim_link_names_is_the_one_the_relay_client_signs_with()
    {
        // the console's link before hosted access was ever on makes the key; switching the relay on later uses that key
        await using var c = await LaneC.LaneC.StartAsync(extra: [new RelayComponent()]);
        using var owner = await c.UserAsync("olive", BuiltinRoles.Owner);
        await LaneC.LaneC.ReauthAsync(owner);
        var r = await LaneC.LaneC.PutAsync(owner, "/api/core/settings/files/relay", new { enabled = false, origin = "https://frostdev.io", host = "olive.kiln.nexus" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.False(File.Exists(Path.Combine(c.H.DataDir, "relay-public.pem")), "no key before hosted access or a claim link");
        var answer = ConsoleCommands.Claim(c.H.Host);
        Assert.Contains("hosted access is off", answer);
        var pem = File.ReadAllText(Path.Combine(c.H.DataDir, "relay-public.pem"));
        Assert.Contains($"SHA-256 {RelayComponent.PemFingerprint(pem)}", answer);
        r = await LaneC.LaneC.PutAsync(owner, "/api/core/settings/files/relay", new { enabled = true, origin = "https://frostdev.io", host = "olive.kiln.nexus" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        await Eventually.True(() => c.H.Host.Services.Get<RelayStatus>().Describe().Enabled, 5000, "relay on");
        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.Equal(pem, File.ReadAllText(Path.Combine(c.H.DataDir, "relay-public.pem")));
    }
}
