// SPDX-License-Identifier: MIT
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Identity;
using Kilnwarden.Ops;
using Kilnwarden.Offline.Support;

namespace Kilnwarden.Offline.Ops;

/// <summary>Signed update packages (DESIGN §7): minisign signatures, BLAKE2b, trusted keys in updates.json.</summary>
public class SignatureTests
{
    private static readonly JsonObject V = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "minisign-vectors.json")))!.AsObject();
    private static string S(string k) => V[k]!.GetValue<string>();

    private static SigningKey Key(string name, string pub)
    {
        var k = Minisign.ParsePublicKey(pub)!.Value;
        return new SigningKey(name, k.KeyId, k.PublicKey);
    }

    [Fact]
    public void Blake2b_matches_hashlib()
    {
        foreach (var v in V["blake2b"]!.AsArray())
        {
            byte[] input = v!["inputHex"] is JsonValue hx ? Convert.FromHexString(hx.GetValue<string>())
                : [.. Enumerable.Range(0, v["pattern251"]!.GetValue<int>()).Select(i => (byte)(i % 251))];
            Assert.Equal(v["blake2b512"]!.GetValue<string>(), Convert.ToHexStringLower(Blake2b.Hash(input)));
            // the same in uneven pieces
            var b = new Blake2b();
            for (var i = 0; i < input.Length; i += 77) b.Update(input.AsSpan(i, Math.Min(77, input.Length - i)));
            Assert.Equal(v["blake2b512"]!.GetValue<string>(), Convert.ToHexStringLower(b.Final()));
        }
    }

    [Fact]
    public void Minisign_signatures_verify_and_every_kind_of_tampering_fails()
    {
        var data = Convert.FromBase64String(S("dataBase64"));
        IReadOnlyList<SigningKey> keys = [Key("publisher", S("publicKey1"))];
        var ok = Minisign.Verify(S("signaturePrehashed"), data, keys);
        Assert.Equal(("verified", "publisher"), (ok.State, ok.Key));
        Assert.Contains("file:package.zip", ok.TrustedComment);
        Assert.Equal("verified", Minisign.Verify(S("signatureLegacy"), data, keys).State);
        Assert.Equal("unknown_key", Minisign.Verify(S("signatureOtherKey"), data, keys).State);
        Assert.Equal("invalid", Minisign.Verify(S("signatureTamperedComment"), data, keys).State);
        Assert.Equal("invalid", Minisign.Verify(S("signatureImpostor"), data, keys).State);
        var altered = (byte[])data.Clone();
        altered[100] ^= 1;
        Assert.Equal("invalid", Minisign.Verify(S("signaturePrehashed"), altered, keys).State);
        Assert.Equal("unsigned", Minisign.Verify("", data, keys).State);
        foreach (var junk in new[] { "hello", "untrusted comment: x\n!!!\ntrusted comment: y\n???", new string('A', 5000) })
            Assert.Equal("invalid", Minisign.Verify(junk, data, keys).State);
        // a .pub file parses as well as its key line
        Assert.Equal(Minisign.ParsePublicKey(S("publicKey1"))!.Value.KeyId, Minisign.ParsePublicKey(S("publicKey1File"))!.Value.KeyId);
        Assert.Null(Minisign.ParsePublicKey("RWQ" + new string('A', 10)));
        Assert.Equal("8877665544332211", keys[0].KeyIdText);
    }

    // ---- through the panel: signed packages, trusted keys, requireSignature

    private static readonly byte[] Seed = SHA256.HashData("signature tests"u8);
    private static readonly byte[] KeyId = [1, 2, 3, 4, 5, 6, 7, 8];

    private static string PublicKey() => Convert.ToBase64String([.. "Ed"u8, .. KeyId, .. Ed25519.PublicKey(Seed)]);

    private static string Sign(byte[] data, byte[]? seed = null, byte[]? keyId = null, string trusted = "timestamp:1764072000\tfile:cool-mod.zip")
    {
        seed ??= Seed;
        var sig = Ed25519.Sign(seed, Blake2b.Hash(data));
        var global = Ed25519.Sign(seed, [.. sig, .. Encoding.UTF8.GetBytes(trusted)]);
        return $"untrusted comment: test\n{Convert.ToBase64String([.. "ED"u8, .. keyId ?? KeyId, .. sig])}\ntrusted comment: {trusted}\n{Convert.ToBase64String(global)}\n";
    }

    private static byte[] ModZip() => Zips.Make(("cool-mod/kiln.mod.json", Zips.Mod()), ("cool-mod/Cool.dll", new string('M', 3000)));

    private static async Task<HttpResponseMessage> UploadAsync(OpsHarness.Client c, byte[] zip, string? signature)
    {
        var open = await c.PostAsync("/api/updates/uploads", new { name = "cool-mod.zip", size = zip.Length, signature });
        Assert.True(open.StatusCode == HttpStatusCode.Created, await open.Content.ReadAsStringAsync());
        var id = (await OpsHarness.Client.JsonAsync(open))!["id"]!.GetValue<string>();
        Assert.Equal(HttpStatusCode.OK, (await c.PutBytesAsync($"/api/updates/uploads/{id}?offset=0", zip)).StatusCode);
        return await c.PostAsync($"/api/updates/uploads/{id}/finish", new { });
    }

    [Fact]
    public async Task Trusted_keys_verify_uploads_and_require_signature_refuses_the_rest()
    {
        string? incoming = null;
        await using var h = await OpsHarness.StartAsync(k => k.Answer = req => new JsonObject
        {
            ["t"] = "res", ["ok"] = true, ["op"] = req["op"]!.GetValue<string>(), ["ms"] = 1,
            ["data"] = req["op"]!.GetValue<string>() == "update_list" ? new JsonObject { ["incoming"] = incoming, ["units"] = new JsonArray() } : new JsonObject(),
        });
        incoming = h.Incoming;
        using var owner = await h.UserAsync("olive", BuiltinRoles.Owner);
        await owner.ReauthAsync();
        // a key that is not minisign's, and requireSignature without a key, are refused
        Assert.Equal((HttpStatusCode)422, (await owner.PutAsync("/api/updates/sources", new { allowedOrigins = Array.Empty<string>(), trustedKeys = new[] { new { name = "x", publicKey = "nope" } } })).StatusCode);
        Assert.Equal((HttpStatusCode)422, (await owner.PutAsync("/api/updates/sources", new { allowedOrigins = Array.Empty<string>(), requireSignature = true })).StatusCode);
        var r = await owner.PutAsync("/api/updates/sources", new { allowedOrigins = Array.Empty<string>(), requireSignature = true, trustedKeys = new[] { new { name = "Cool Mods", publicKey = PublicKey() } } });
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());

        var zip = ModZip();
        var sha = Convert.ToHexStringLower(SHA256.HashData(zip));
        // signed by the trusted key: verified, and it stages
        var plan = (await OpsHarness.Client.JsonAsync(await UploadAsync(owner, zip, Sign(zip))))!;
        Assert.Equal(("verified", "Cool Mods"), (plan["signature"]!["state"]!.GetValue<string>(), plan["signature"]!["key"]!.GetValue<string>()));
        r = await owner.PostAsync($"/api/updates/plans/{plan["id"]}/stage", new { sha256 = sha });
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());

        // another key: a plan that says so, and staging is refused
        plan = (await OpsHarness.Client.JsonAsync(await UploadAsync(owner, zip, Sign(zip, SHA256.HashData("other"u8), [9, 9, 9, 9, 9, 9, 9, 9]))))!;
        Assert.Equal("unknown_key", plan["signature"]!["state"]!.GetValue<string>());
        r = await owner.PostAsync($"/api/updates/plans/{plan["id"]}/stage", new { sha256 = sha });
        Assert.Equal(("signature_required", HttpStatusCode.Conflict), (await OpsHarness.Client.CodeAsync(r), r.StatusCode));

        // unsigned: the same
        plan = (await OpsHarness.Client.JsonAsync(await UploadAsync(owner, zip, null)))!;
        Assert.Equal("unsigned", plan["signature"]!["state"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsync($"/api/updates/plans/{plan["id"]}/stage", new { sha256 = sha })).StatusCode);

        // a signature that does not match the bytes: no plan at all
        var other = Zips.Make(("cool-mod/kiln.mod.json", Zips.Mod()), ("cool-mod/Cool.dll", new string('N', 3000)));
        r = await UploadAsync(owner, other, Sign(zip));
        Assert.Equal(("bad_signature", (HttpStatusCode)422), (await OpsHarness.Client.CodeAsync(r), r.StatusCode));
        await Eventually.True(() => h.Audit.Read(50).Any(e => e.GetProperty("op").GetString() == "updates.plan" && !e.GetProperty("ok").GetBoolean()), 3000, "the refusal in the audit log");
        Assert.Single(h.Kap.Requests("update_stage"));
    }
}
