// SPDX-License-Identifier: MIT
// From the step 0 spike: certificates with an RSA key that Wine's crypt32 + schannel accept. Under Wine, .NET's usual
// paths fail (CopyWithPrivateKey: CertSetCertificateContextProperty with a CNG key; ECDsa: NCrypt has no ECDSA), but a
// PKCS#12 import works for RSA: X509CertificateLoader -> PFXImportCertStore -> an rsaenh (CAPI) key container, which is
// what Wine's schannel reads (CryptAcquireCertificatePrivateKey + PRIVATEKEYBLOB). So build the PKCS#12 ourselves:
// unencrypted bags, no MAC (only ever in memory).
using System.Formats.Asn1;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Kilnwarden.Tls;

internal static class Pfx
{
    private const string Data = "1.2.840.113549.1.7.1";
    private const string KeyBag = "1.2.840.113549.1.12.10.1.1";
    private const string CertBag = "1.2.840.113549.1.12.10.1.3";
    private const string X509Cert = "1.2.840.113549.1.9.22.1";
    private const string LocalKeyId = "1.2.840.113549.1.9.21";

    /// <summary>A self-signed RSA server certificate made in process, without CopyWithPrivateKey (which fails under Wine):
    /// (certificate PEM, PKCS#8 key PEM).</summary>
    public static (byte[] CertPem, byte[] KeyPem) SelfSignedRsa(string cn, IReadOnlyList<string> names, DateTimeOffset from, DateTimeOffset to, int bits = 2048,
        IReadOnlyList<X509Extension>? extra = null)
    {
        using var rsa = RSA.Create(bits);
        var req = new CertificateRequest(new X500DistinguishedName("CN=" + cn.Replace(",", " ").Replace("=", " ")), rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        foreach (var n in names)
        {
            if (IPAddress.TryParse(n, out var ip)) san.AddIpAddress(ip);
            else san.AddDnsName(n);
        }
        req.CertificateExtensions.Add(san.Build());
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));
        foreach (var e in extra ?? []) req.CertificateExtensions.Add(e);
        var serial = RandomNumberGenerator.GetBytes(16);
        serial[0] &= 0x7F;
        using var pub = req.Create(req.SubjectName, X509SignatureGenerator.CreateForRSA(rsa, RSASignaturePadding.Pkcs1), from, to, serial);
        var key = rsa.ExportPkcs8PrivateKey();
        try
        {
            return (System.Text.Encoding.ASCII.GetBytes(Pem.Encode("CERTIFICATE", pub.RawData)), System.Text.Encoding.ASCII.GetBytes(Pem.Encode("PRIVATE KEY", key)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>PEM certificate(s) (leaf first, as fullchain.pem) and an RSA key (PKCS#1 or PKCS#8) to PKCS#12.</summary>
    public static byte[] FromPem(string certPem, string keyPem, bool withIntermediates = true)
    {
        var certs = new X509Certificate2Collection();
        certs.ImportFromPem(certPem);
        if (certs.Count == 0) throw new CryptographicException("no certificate in the PEM file");
        using var rsa = RSA.Create();
        rsa.ImportFromPem(keyPem);
        var leaf = certs[0];
        using (var leafKey = leaf.GetRSAPublicKey())
        {
            if (leafKey is null) throw new CryptographicException("the certificate's key is not RSA");
            if (!leafKey.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(rsa.ExportSubjectPublicKeyInfo()))
                throw new CryptographicException("the key does not match the certificate");
        }
        var extra = new List<byte[]>();
        for (var i = 1; withIntermediates && i < certs.Count; i++) extra.Add(certs[i].RawData);
        var pfx = Build(rsa.ExportPkcs8PrivateKey(), leaf.RawData, extra);
        foreach (var c in certs) c.Dispose();
        return pfx;
    }

    public static byte[] Build(byte[] pkcs8, byte[] leafDer, IReadOnlyList<byte[]> extraDer)
    {
        var id = SHA1.HashData(leafDer);
        var certs = new AsnWriter(AsnEncodingRules.DER);
        using (certs.PushSequence())
        {
            WriteCertBag(certs, leafDer, id);
            foreach (var der in extraDer) WriteCertBag(certs, der, null);
        }
        var keys = new AsnWriter(AsnEncodingRules.DER);
        using (keys.PushSequence())
        using (keys.PushSequence())
        {
            keys.WriteObjectIdentifier(KeyBag);
            using (keys.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true))) keys.WriteEncodedValue(pkcs8);
            WriteLocalKeyId(keys, id);
        }
        var authSafe = new AsnWriter(AsnEncodingRules.DER);
        using (authSafe.PushSequence())
        {
            WriteData(authSafe, certs.Encode());
            WriteData(authSafe, keys.Encode());
        }
        var pfx = new AsnWriter(AsnEncodingRules.DER);
        using (pfx.PushSequence())
        {
            pfx.WriteInteger(3);
            WriteData(pfx, authSafe.Encode());
        }
        CryptographicOperations.ZeroMemory(pkcs8);
        return pfx.Encode();
    }

    private static void WriteData(AsnWriter w, byte[] content)
    {
        using (w.PushSequence())
        {
            w.WriteObjectIdentifier(Data);
            using (w.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true))) w.WriteOctetString(content);
        }
    }

    private static void WriteCertBag(AsnWriter w, byte[] der, byte[]? id)
    {
        using (w.PushSequence())
        {
            w.WriteObjectIdentifier(CertBag);
            using (w.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true)))
            using (w.PushSequence())
            {
                w.WriteObjectIdentifier(X509Cert);
                using (w.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true))) w.WriteOctetString(der);
            }
            if (id is not null) WriteLocalKeyId(w, id);
        }
    }

    private static void WriteLocalKeyId(AsnWriter w, byte[] id)
    {
        using (w.PushSetOf())
        using (w.PushSequence())
        {
            w.WriteObjectIdentifier(LocalKeyId);
            using (w.PushSetOf()) w.WriteOctetString(id);
        }
    }
}
