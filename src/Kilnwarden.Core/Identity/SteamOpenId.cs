// SPDX-License-Identifier: MIT
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Kilnwarden.Auth;

namespace Kilnwarden.Identity;

/// <summary>Sign-in with Steam: OpenID 2.0 straight to steamcommunity.com (DESIGN §3), with the panel's own origin as the
/// realm. The callback's answer is checked against the exact endpoint, the exact return URL of this sign-in (which
/// carries its one-use state), a fresh response nonce and the signed field list, and then verified by Steam itself
/// (check_authentication, a direct server-to-server POST). Only the SteamID64 is kept.</summary>
public sealed partial class SteamOpenId : ISignInProvider
{
    public const string Endpoint = "https://steamcommunity.com/openid/login";
    public const string Ns = "http://specs.openid.net/auth/2.0";
    public const string IdentifierSelect = "http://specs.openid.net/auth/2.0/identifier_select";
    public static readonly TimeSpan NonceWindow = TimeSpan.FromMinutes(5);
    public const int MaxResponseBytes = 4096;

    private static readonly string[] MustSign = ["op_endpoint", "claimed_id", "identity", "return_to", "response_nonce", "assoc_handle"];

    private readonly HttpMessageInvoker _http;
    private readonly TimeProvider _time;
    private readonly TimeSpan _timeout;

    /// <param name="http">Sends check_authentication (tests pass a fake Steam; production a handler with no redirects).</param>
    public SteamOpenId(HttpMessageInvoker http, TimeProvider time, TimeSpan? timeout = null)
    {
        _http = http;
        _time = time;
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
    }

    public string Id => "steam";
    public string Title => "Steam";

    /// <summary>The panel's callback for one sign-in: origin + path + ?state=.</summary>
    public static string ReturnUrl(string origin, string state) => $"{origin}/api/identity/steam/callback?state={state}";

    /// <summary>Where the browser goes: Steam's sign-in page for this intent.</summary>
    public static string RedirectUrl(string origin, LoginIntent intent)
    {
        var q = new (string, string)[]
        {
            ("openid.ns", Ns), ("openid.mode", "checkid_setup"), ("openid.return_to", intent.ReturnUrl), ("openid.realm", origin + "/"),
            ("openid.identity", IdentifierSelect), ("openid.claimed_id", IdentifierSelect),
        };
        return Endpoint + "?" + string.Join('&', q.Select(kv => Uri.EscapeDataString(kv.Item1) + "=" + Uri.EscapeDataString(kv.Item2)));
    }

    /// <summary>Checks a callback's openid.* fields for <paramref name="intent"/>; the verified identity, or why not.</summary>
    public async Task<(ExternalIdentity? Id, string? Error)> VerifyAsync(IReadOnlyDictionary<string, string> q, LoginIntent intent, CancellationToken ct)
    {
        string? F(string k) => q.TryGetValue("openid." + k, out var v) ? v : null;
        if (F("mode") == "cancel") return (null, "canceled");
        if (F("ns") != Ns || F("mode") != "id_res") return (null, "not an OpenID 2.0 positive assertion");
        if (F("op_endpoint") != Endpoint) return (null, "wrong OpenID endpoint");
        if (F("return_to") != intent.ReturnUrl) return (null, "wrong return URL");
        var claimed = F("claimed_id");
        if (claimed is null || F("identity") != claimed || ClaimedRe().Match(claimed) is not { Success: true } m) return (null, "not a Steam identity");
        var signed = (F("signed") ?? "").Split(',');
        if (MustSign.Any(f => !signed.Contains(f)) || F("sig") is not { Length: > 0 } || F("assoc_handle") is not { Length: > 0 }) return (null, "the assertion is not signed as required");
        if (!FreshNonce(F("response_nonce"))) return (null, "stale or malformed response nonce");
        // Steam verifies its own signature: every openid.* field back, mode changed
        var form = q.Where(kv => kv.Key.StartsWith("openid.", StringComparison.Ordinal) && kv.Key != "openid.mode")
            .Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)).Append(new("openid.mode", "check_authentication")).ToList();
        using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = new FormUrlEncodedContent(form) };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);
        try
        {
            using var res = await _http.SendAsync(req, cts.Token).ConfigureAwait(false);
            if ((int)res.StatusCode != 200) return (null, $"Steam answered {(int)res.StatusCode}");
            var body = await ReadCapped(res, cts.Token).ConfigureAwait(false);
            var valid = body?.Split('\n').Select(l => l.Trim()).Any(l => l == "is_valid:true") == true;
            if (!valid) return (null, "Steam did not confirm the assertion");
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or IOException)
        {
            return (null, "Steam could not be reached to confirm the sign-in");
        }
        return (new ExternalIdentity(Id, m.Groups[1].Value), null);
    }

    private bool FreshNonce(string? nonce)
    {
        // "2026-11-23T19:56:00Z" + unique suffix
        if (nonce is not { Length: >= 20 and <= 255 } || !DateTimeOffset.TryParseExact(nonce[..20], "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var at)) return false;
        var now = _time.GetUtcNow();
        return at <= now + TimeSpan.FromMinutes(1) && now - at <= NonceWindow;
    }

    private static async Task<string?> ReadCapped(HttpResponseMessage res, CancellationToken ct)
    {
        await using var s = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var buf = new byte[MaxResponseBytes + 1];
        var n = 0;
        while (n < buf.Length)
        {
            var r = await s.ReadAsync(buf.AsMemory(n), ct).ConfigureAwait(false);
            if (r == 0) break;
            n += r;
        }
        return n > MaxResponseBytes ? null : Encoding.UTF8.GetString(buf, 0, n);
    }

    [GeneratedRegex("^https://steamcommunity\\.com/openid/id/(7656119[0-9]{10})$")]
    private static partial Regex ClaimedRe();
}
