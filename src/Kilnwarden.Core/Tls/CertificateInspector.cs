// SPDX-License-Identifier: MIT
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Kilnwarden.Tls;

/// <summary>Reads what the panel shows about a certificate chain (fingerprint, names, expiry, key type) from its PEM.
/// The fingerprint needs no platform crypto. Names, subject and expiry come from X509Certificate2, which Wine's crypt32
/// parses. The key type is read from the certificate's own ASN.1, because Wine's CNG cannot import an ECDSA public key
/// (0x80090027; the step 0 spike and the first 30.8 lab run).</summary>
public static class CertificateInspector
{
    public static CertificateInfo Inspect(ReadOnlySpan<byte> chainPem, ReadOnlySpan<byte> keyPem, string source)
    {
        var certs = Pem.Decode(chainPem).Where(b => b.Label == "CERTIFICATE").ToList();
        if (certs.Count == 0) throw new FormatException("no CERTIFICATE block in the chain PEM");
        var leaf = certs[0].Der;
        var fp = Convert.ToHexString(SHA256.HashData(leaf));
        var fingerprint = string.Join(':', Enumerable.Range(0, fp.Length / 2).Select(i => fp.Substring(i * 2, 2)));
        using var x = X509CertificateLoader.LoadCertificate(leaf);
        var names = new List<string>();
        foreach (var ext in x.Extensions)
        {
            if (ext is X509SubjectAlternativeNameExtension san)
            {
                names.AddRange(san.EnumerateDnsNames());
                names.AddRange(san.EnumerateIPAddresses().Select(a => a.ToString()));
            }
        }
        var keyType = KeyType(x.PublicKey);
        return new CertificateInfo(fingerprint, x.Subject, names, new DateTimeOffset(x.NotAfter.ToUniversalTime(), TimeSpan.Zero), keyType, source, certs.Count);
    }

    /// <summary>"ecdsa-p256", "rsa-2048", "ed25519" from the SubjectPublicKeyInfo, without platform crypto.</summary>
    internal static string KeyType(PublicKey key)
    {
        try
        {
            switch (key.Oid.Value)
            {
                case "1.2.840.10045.2.1":
                {
                    if (key.EncodedParameters?.RawData is not { Length: > 0 } p) return "ecdsa-?";
                    var curve = new AsnReader(p, AsnEncodingRules.DER).ReadObjectIdentifier();
                    return "ecdsa-" + curve switch { "1.2.840.10045.3.1.7" => "p256", "1.3.132.0.34" => "p384", "1.3.132.0.35" => "p521", _ => curve };
                }
                case "1.2.840.113549.1.1.1":
                {
                    var seq = new AsnReader(key.EncodedKeyValue.RawData, AsnEncodingRules.DER).ReadSequence();
                    var modulus = seq.ReadIntegerBytes().Span;
                    if (modulus.Length > 0 && modulus[0] == 0) modulus = modulus[1..];
                    return "rsa-" + modulus.Length * 8;
                }
                case "1.3.101.112":
                    return "ed25519";
                default:
                    return key.Oid.Value ?? "?";
            }
        }
        catch (AsnContentException)
        {
            return "?";
        }
    }
}
