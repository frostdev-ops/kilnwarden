// SPDX-License-Identifier: MIT
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Kilnwarden.Tls.Acme;

/// <summary>What a newly issued (or stored) certificate must pass before it is served: it reads, its leaf covers every
/// wanted name, it is valid now, its key is the leaf's key, and it is a full chain whose every certificate is signed by
/// the next one. Done in managed code without X509Chain (crypt32 under Wine) and without importing ECDSA keys.</summary>
public static class CertificateCheck
{
    public static CertificateInfo Check(byte[] chainPem, byte[] keyPem, IReadOnlyList<string> names, DateTimeOffset now, string source = "acme")
    {
        var info = CertificateInspector.Inspect(chainPem, keyPem, source);
        var missing = names.Where(n => !info.Names.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count > 0) throw new CryptographicException("the certificate lacks " + string.Join(", ", missing));
        var certs = Pem.Decode(chainPem).Where(b => b.Label == "CERTIFICATE").Select(b => X509CertificateLoader.LoadCertificate(b.Der)).ToList();
        try
        {
            if (certs.Count < 2) throw new CryptographicException("the chain has no intermediate (a full chain is required)");
            var leaf = certs[0];
            if (now < new DateTimeOffset(leaf.NotBefore.ToUniversalTime()) - TimeSpan.FromMinutes(5) || now >= info.NotAfter)
                throw new CryptographicException("the certificate is not valid now");
            using (var rsa = RSA.Create())
            {
                rsa.ImportFromPem(System.Text.Encoding.ASCII.GetString(keyPem));
                using var pub = leaf.GetRSAPublicKey() ?? throw new CryptographicException("the leaf's key is not RSA");
                if (!rsa.ExportParameters(false).Modulus!.AsSpan().SequenceEqual(pub.ExportParameters(false).Modulus))
                    throw new CryptographicException("the key does not belong to the certificate");
            }
            for (var i = 0; i + 1 < certs.Count; i++)
            {
                if (!certs[i].IssuerName.RawData.AsSpan().SequenceEqual(certs[i + 1].SubjectName.RawData))
                    throw new CryptographicException($"certificate {i + 1} of the chain is not issued by the next one");
                if (!SignedBy(certs[i], certs[i + 1])) throw new CryptographicException($"certificate {i + 1} of the chain has a bad signature");
            }
            return info;
        }
        finally
        {
            foreach (var c in certs) c.Dispose();
        }
    }

    /// <summary>Checks <paramref name="cert"/>'s signature with <paramref name="issuer"/>'s RSA key (sha256/384/512
    /// with RSA, PKCS#1 v1.5: what public CAs sign RSA leaves and intermediates with).</summary>
    internal static bool SignedBy(X509Certificate2 cert, X509Certificate2 issuer)
    {
        var outer = new AsnReader(cert.RawData, AsnEncodingRules.DER).ReadSequence();
        var tbs = outer.ReadEncodedValue();
        var alg = outer.ReadSequence().ReadObjectIdentifier();
        var sig = outer.ReadBitString(out _);
        var hash = alg switch
        {
            "1.2.840.113549.1.1.11" => HashAlgorithmName.SHA256,
            "1.2.840.113549.1.1.12" => HashAlgorithmName.SHA384,
            "1.2.840.113549.1.1.13" => HashAlgorithmName.SHA512,
            _ => throw new CryptographicException($"unsupported chain signature algorithm {alg} (RSA chains only)"),
        };
        using var key = issuer.GetRSAPublicKey() ?? throw new CryptographicException("an issuer's key is not RSA");
        return key.VerifyData(tbs.Span, sig, hash, RSASignaturePadding.Pkcs1);
    }
}
