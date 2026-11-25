// SPDX-License-Identifier: MIT
using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kilnwarden.Auth;

namespace Kilnwarden.Identity;

/// <summary>A broker signing key pinned in the build: its origin, key id and Ed25519 public key.</summary>
public sealed record BrokerKey(string Origin, string Kid, byte[] PublicKey);

/// <summary>Sign-in with Discord through the frostdev.io broker (DESIGN §3). A fixed OAuth redirect cannot serve many
/// self-hosted panels, so the broker runs the OAuth and returns a short-lived Ed25519-signed assertion (a compact JWS,
/// alg EdDSA) bound to this panel, this sign-in's return URL and its one-use nonce, carrying the Discord user id, name and
/// roles in the guild the owner chose. No Discord token ever reaches the panel (an assertion naming one is refused).
/// Only keys pinned in the build verify; there is no setting that adds one.
/// <para>Broker contract (the mock in the tests implements it; the real broker is out of this step's scope):
/// GET {broker}/kiln/auth/discord?v=1&amp;panel={panelId}&amp;guild={guildId}&amp;return={returnUrl}&amp;nonce={nonce} ->
/// 302 {returnUrl}&amp;assertion={jws} (or &amp;error={code}). JWS header {"alg":"EdDSA","typ":"kiln-broker+jws","kid"};
/// payload {"v":1,"iss":broker origin,"aud":"kiln-webadmin","panel","ret","nonce","iat","exp" (unix s, exp - iat &lt;= 300),
/// "sub" (Discord user id),"name","guild","roles":[role ids]}.</para></summary>
public sealed partial class DiscordBroker : ISignInProvider
{
    /// <summary>The broker protocol's audience (docs/protocol-relay-broker.md 6.3): a protocol constant, kept as it is when the mod is
    /// renamed (renaming it would break every deployed broker).</summary>
    public const string Audience = "kiln-webadmin";
    public const string Typ = "kiln-broker+jws";
    public const int MaxAssertionLength = 8192;
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Skew = TimeSpan.FromSeconds(60);

    /// <summary>The keys built into this assembly from the release's public broker key file.</summary>
    public static readonly IReadOnlyList<BrokerKey> PinnedKeys = ReadBuildPins();

    private static IReadOnlyList<BrokerKey> ReadBuildPins()
    {
        using var file = typeof(DiscordBroker).Assembly.GetManifestResourceStream("webadmin-broker-pins.json");
        return file is null ? [] : ReadPins(file);
    }

    private sealed record Pin(string Origin, string Kid, string PublicKeyHex);

    internal static IReadOnlyList<BrokerKey> ReadPins(Stream file)
    {
        var pins = JsonSerializer.Deserialize<Pin[]>(file, Http.JsonBody.Options)
            ?? throw new FormatException($"{Product.Name} broker pins must be a JSON list.");
        var keys = new List<BrokerKey>(pins.Length);
        foreach (var p in pins)
        {
            if (p is null || !IdentitySettings.IsBrokerOrigin(p.Origin) || p.Origin.EndsWith('/') ||
                !KeyIdRe().IsMatch(p.Kid ?? "") || p.PublicKeyHex is not { Length: 64 } ||
                !p.PublicKeyHex.All(char.IsAsciiHexDigit) || keys.Any(k => k.Origin == p.Origin && k.Kid == p.Kid))
                throw new FormatException($"Each {Product.Name} broker pin needs an HTTPS origin, unique key id and 32-byte public key.");
            keys.Add(new BrokerKey(p.Origin, p.Kid!, Convert.FromHexString(p.PublicKeyHex)));
        }
        return keys.AsReadOnly();
    }

    private readonly IReadOnlyList<BrokerKey> _keys;
    private readonly TimeProvider _time;

    public DiscordBroker(TimeProvider time, IReadOnlyList<BrokerKey>? keys = null)
    {
        _time = time;
        _keys = keys ?? PinnedKeys;
    }

    public string Id => "discord";
    public string Title => "Discord";

    public bool HasKeyFor(string brokerOrigin) => _keys.Any(k => k.Origin == brokerOrigin.TrimEnd('/'));

    public static string ReturnUrl(string origin, string state) => $"{origin}/api/identity/discord/callback?state={state}";

    public static string RedirectUrl(DiscordSettings s, string panelId, LoginIntent intent) =>
        $"{s.Broker.TrimEnd('/')}/kiln/auth/discord?v=1&panel={Uri.EscapeDataString(panelId)}&guild={Uri.EscapeDataString(s.GuildId)}" +
        $"&return={Uri.EscapeDataString(intent.ReturnUrl)}&nonce={Uri.EscapeDataString(intent.Nonce)}";

    private sealed record Header(string? Alg, string? Typ, string? Kid);

    /// <summary>Verifies an assertion for <paramref name="intent"/> under the current settings; the identity, or why not.</summary>
    public (ExternalIdentity? Id, string? Error) Verify(string? jws, LoginIntent intent, DiscordSettings s, string panelId)
    {
        if (jws is not { Length: > 0 and <= MaxAssertionLength }) return (null, "missing or oversized assertion");
        var parts = jws.Split('.');
        if (parts.Length != 3) return (null, "malformed assertion");
        byte[] headerBytes, payloadBytes, sig;
        try
        {
            headerBytes = Base64Url.DecodeFromChars(parts[0]);
            payloadBytes = Base64Url.DecodeFromChars(parts[1]);
            sig = Base64Url.DecodeFromChars(parts[2]);
        }
        catch (FormatException)
        {
            return (null, "malformed assertion");
        }
        Header? h;
        JsonElement p;
        try
        {
            h = JsonSerializer.Deserialize<Header>(headerBytes, Http.JsonBody.Options);
            using var doc = JsonDocument.Parse(payloadBytes, new JsonDocumentOptions { MaxDepth = 8 });
            p = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return (null, "malformed assertion");
        }
        if (h is not { Alg: "EdDSA", Typ: Typ } || p.ValueKind != JsonValueKind.Object) return (null, "not a broker assertion");
        var origin = s.Broker.TrimEnd('/');
        var key = _keys.FirstOrDefault(k => k.Origin == origin && k.Kid == h.Kid);
        if (key is null) return (null, "the assertion's key is not pinned for this broker");
        if (!Ed25519.Verify(key.PublicKey, Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), sig)) return (null, "bad assertion signature");
        // signed: now check every claim
        foreach (var prop in p.EnumerateObject())
            if (prop.Name.Contains("token", StringComparison.OrdinalIgnoreCase)) return (null, "the assertion carries a token (refused)");
        string? Str(string n) => p.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        long? Num(string n) => p.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var x) ? x : null;
        if (Num("v") != 1 || Str("iss") != origin || Str("aud") != Audience) return (null, "wrong issuer or audience");
        if (Str("panel") != panelId) return (null, "the assertion is for another panel");
        if (Str("ret") != intent.ReturnUrl) return (null, "wrong return URL");
        if (Str("nonce") is not { } nonce || !SecretToken.HashEquals(nonce, intent.Nonce)) return (null, "wrong nonce");
        if (Num("iat") is not { } iat || Num("exp") is not { } exp) return (null, "missing times");
        var now = _time.GetUtcNow().ToUnixTimeSeconds();
        if (iat > now + (long)Skew.TotalSeconds || exp <= now || exp - iat > (long)MaxLifetime.TotalSeconds || exp <= iat) return (null, "expired or not yet valid");
        if (Str("sub") is not { } sub || !SnowflakeRe().IsMatch(sub)) return (null, "missing Discord user id");
        if (Str("guild") != s.GuildId) return (null, "the assertion is for another Discord server");
        if (!p.TryGetProperty("roles", out var rolesEl) || rolesEl.ValueKind != JsonValueKind.Array || rolesEl.GetArrayLength() > 250) return (null, "missing roles");
        var roles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in rolesEl.EnumerateArray())
        {
            if (r.ValueKind != JsonValueKind.String || !SnowflakeRe().IsMatch(r.GetString()!)) return (null, "malformed roles");
            roles.Add(r.GetString()!);
        }
        if (s.AllowedRoles.Count > 0 && !s.AllowedRoles.Any(roles.Contains)) return (null, "not_allowed");
        var name = Str("name");
        if (name is { Length: > 64 }) name = name[..64];
        return (new ExternalIdentity(Id, sub, name, [.. roles]), null);
    }

    [GeneratedRegex("^[0-9]{5,20}$")]
    private static partial Regex SnowflakeRe();

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}\\z")]
    private static partial Regex KeyIdRe();
}
