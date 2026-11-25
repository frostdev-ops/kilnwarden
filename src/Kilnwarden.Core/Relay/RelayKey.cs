// SPDX-License-Identifier: MIT
using System.Security.Cryptography;
using System.Text;
using Kilnwarden.Auth;
using Kilnwarden.Storage;

namespace Kilnwarden.Relay;

internal sealed class RelayKey : IDisposable
{
    private readonly RSA _rsa;

    public RelayKey(DataStore data)
    {
        _rsa = RSA.Create(2048);
        try
        {
            if (data.ReadBytes("relay-key.pem") is { } privateKey)
            {
                try { _rsa.ImportFromPem(Encoding.ASCII.GetString(privateKey)); }
                finally { CryptographicOperations.ZeroMemory(privateKey); }
            }
            else
            {
                var pkcs8 = _rsa.ExportPkcs8PrivateKeyPem();
                var bytes = Encoding.ASCII.GetBytes(pkcs8);
                try { data.WriteBytes("relay-key.pem", bytes, secret: true); }
                finally { CryptographicOperations.ZeroMemory(bytes); }
            }
            data.WriteBytes("relay-public.pem", Encoding.ASCII.GetBytes(_rsa.ExportSubjectPublicKeyInfoPem()));
        }
        catch
        {
            _rsa.Dispose();
            throw;
        }
    }

    // Signed requests are bound to their operation, hostname and stream/challenge identifier; the relay counts nonces.
    public void Authorize(Action<string, string> header, string kind, string host, string id, TimeProvider time)
    {
        var ts = time.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var nonce = SecretToken.New();
        var data = Encoding.UTF8.GetBytes($"kwa-relay/1\n{kind}\n{host}\n{id}\n{ts}\n{nonce}");
        byte[] sig;
        lock (_rsa) sig = _rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        header("X-Kwa-Host", host);
        header("X-Kwa-Id", id);
        header("X-Kwa-Time", ts);
        header("X-Kwa-Nonce", nonce);
        header("X-Kwa-Signature", System.Buffers.Text.Base64Url.EncodeToString(sig));
    }

    /// <summary>The public key as the relay registers it: SubjectPublicKeyInfo DER (relay-public.pem's content).</summary>
    public byte[] Spki()
    {
        lock (_rsa) return _rsa.ExportSubjectPublicKeyInfo();
    }

    /// <summary>SHA-256 hex of the SPKI: how the relay's operator, the claim pages and Panel settings name this key.</summary>
    public string Fingerprint => Convert.ToHexStringLower(SHA256.HashData(Spki()));

    /// <summary>The claim link's query (docs/protocol-relay-broker.md §1.1): the signed request of kind <c>claim</c>
    /// for <paramref name="host"/>, its id the key's fingerprint, carried as
    /// <c>v=1&amp;host&amp;key&amp;time&amp;nonce&amp;sig</c> (key = the SPKI DER, base64url). Whoever opens the link first
    /// may claim the name for this key, once, within 15 minutes; only the private key's holder can make one.</summary>
    public string ClaimQuery(string host, TimeProvider time)
    {
        var spki = Spki();
        var fp = Convert.ToHexStringLower(SHA256.HashData(spki));
        var ts = time.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var nonce = SecretToken.New();
        var data = Encoding.UTF8.GetBytes($"kwa-relay/1\nclaim\n{host}\n{fp}\n{ts}\n{nonce}");
        byte[] sig;
        lock (_rsa) sig = _rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"v=1&host={Uri.EscapeDataString(host)}&key={System.Buffers.Text.Base64Url.EncodeToString(spki)}&time={ts}&nonce={nonce}" +
               $"&sig={System.Buffers.Text.Base64Url.EncodeToString(sig)}";
    }

    public void Dispose() => _rsa.Dispose();
}
