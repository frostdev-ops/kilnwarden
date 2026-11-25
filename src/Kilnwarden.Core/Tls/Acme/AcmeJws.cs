// SPDX-License-Identifier: MIT
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Kilnwarden.Tls.Acme;

/// <summary>RFC 8555 §6.2 request signing with an RSA account key (RS256). RSA, not ECDSA: Wine's CNG cannot import
/// an ECDSA public key (CertificateInspector), and the panel runs under Wine on Linux hosts.</summary>
public sealed class AcmeJws(RSA key)
{
    public RSA Key { get; } = key;

    public static string B64(ReadOnlySpan<byte> b) => Base64Url.EncodeToString(b);

    /// <summary>The account key's JWK (members in lexicographic order, RFC 7638).</summary>
    public JsonObject Jwk()
    {
        var p = Key.ExportParameters(false);
        return new JsonObject { ["e"] = B64(p.Exponent), ["kty"] = "RSA", ["n"] = B64(p.Modulus) };
    }

    /// <summary>The RFC 7638 thumbprint (base64url SHA-256 of the canonical JWK).</summary>
    public string Thumbprint()
    {
        var p = Key.ExportParameters(false);
        var canonical = $"{{\"e\":\"{B64(p.Exponent)}\",\"kty\":\"RSA\",\"n\":\"{B64(p.Modulus)}\"}}";
        return B64(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>The key authorization of a challenge token (RFC 8555 §8.1).</summary>
    public string KeyAuthorization(string token) => token + "." + Thumbprint();

    /// <summary>A flattened JWS: kid when the account exists, else the jwk. A null payload is POST-as-GET ("").</summary>
    public string Sign(string url, string nonce, JsonNode? payload, string? kid)
    {
        var prot = new JsonObject { ["alg"] = "RS256", ["nonce"] = nonce, ["url"] = url };
        if (kid is not null) prot["kid"] = kid;
        else prot["jwk"] = Jwk();
        var p64 = B64(Encoding.UTF8.GetBytes(prot.ToJsonString()));
        var b64 = payload is null ? "" : B64(Encoding.UTF8.GetBytes(payload.ToJsonString()));
        var sig = Key.SignData(Encoding.ASCII.GetBytes(p64 + "." + b64), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return new JsonObject { ["protected"] = p64, ["payload"] = b64, ["signature"] = B64(sig) }.ToJsonString();
    }
}
