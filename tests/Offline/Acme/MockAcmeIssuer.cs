// SPDX-License-Identifier: MIT
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;

namespace Kilnwarden.Offline.Acme;

/// <summary>An in-memory ACME CA (RFC 8555 server side) behind an HttpMessageHandler: no socket for the protocol, a
/// real loopback HTTP request for each http-01 validation (to the panel's answerer, with the name as Host). It checks
/// every JWS (RS256 signature, one-use nonce, url, jwk/kid) and issues leaf + intermediate from a test root.</summary>
internal sealed class MockAcmeIssuer : HttpMessageHandler
{
    public const string Base = "https://acme.test";
    public const string Directory = Base + "/directory";
    public const string Relay = "https://relay.example.test";

    private readonly Func<int> _answererPort;
    private readonly ConcurrentDictionary<string, bool> _nonces = new();
    private readonly ConcurrentDictionary<string, RSA> _accounts = new();
    private readonly ConcurrentDictionary<int, Order> _orders = new();
    private readonly ConcurrentDictionary<int, Authz> _authz = new();
    private int _next;

    public readonly RSA RootKey = RSA.Create(2048), IntermediateKey = RSA.Create(2048);
    public readonly X509Certificate2 Root, Intermediate;

    public bool FailValidation;
    public bool BadNonceOnce;
    public string? ExtraAuthzName;
    public int Validations, Issued, Requests;
    public string? DnsPublicKey, DnsRecord;
    public int DnsChanges;

    /// <summary>The relay's address does not answer (a network error, not an HTTP status).</summary>
    public bool RelayDown;
    public TimeSpan Lifetime = TimeSpan.FromDays(90);
    public readonly List<string> ValidatedHosts = [];

    private sealed class Order
    {
        public required List<string> Names;
        public required List<int> Authz;
        public string Status = "pending";
        public string? CertPem;
    }

    private sealed class Authz
    {
        public required string Name;
        public required string Token;
        public required string Account;
        public string Status = "pending";
        public string? Error;
    }

    public MockAcmeIssuer(Func<int> answererPort)
    {
        _answererPort = answererPort;
        var now = DateTimeOffset.UtcNow;
        var rootReq = new CertificateRequest("CN=Kiln Test Root", RootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        Root = rootReq.CreateSelfSigned(now.AddDays(-1), now.AddYears(5));
        var intReq = new CertificateRequest("CN=Kiln Test Intermediate", IntermediateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        intReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        intReq.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        using var i = intReq.Create(Root, now.AddDays(-1), now.AddYears(3), [1, 2, 3, 4]);
        Intermediate = i.CopyWithPrivateKey(IntermediateKey);
    }

    private string NewNonce()
    {
        var n = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
        _nonces[n] = true;
        return n;
    }

    private HttpResponseMessage Res(HttpStatusCode code, JsonNode? body = null, string? location = null, string? contentType = null, string? text = null)
    {
        var r = new HttpResponseMessage(code)
        {
            Content = text is not null ? new StringContent(text, Encoding.ASCII, contentType ?? "application/pem-certificate-chain")
                : new StringContent(body?.ToJsonString() ?? "", Encoding.UTF8, body is null ? "text/plain" : "application/json"),
        };
        r.Headers.Add("Replay-Nonce", NewNonce());
        if (location is not null) r.Headers.Location = new Uri(location);
        return r;
    }

    private HttpResponseMessage Problem(string type, string detail, HttpStatusCode code = HttpStatusCode.BadRequest) =>
        Res(code, new JsonObject { ["type"] = "urn:ietf:params:acme:error:" + type, ["detail"] = detail });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        Interlocked.Increment(ref Requests);
        var url = req.RequestUri!.AbsoluteUri;
        if (RelayDown && url.StartsWith(Relay, StringComparison.Ordinal)) throw new HttpRequestException("relay unreachable (test)");
        if (DnsPublicKey is { } publicKey && url == Relay + "/kiln/webadmin/relay/dns")
        {
            var add = req.Method == HttpMethod.Put;
            Assert.True(add || req.Method == HttpMethod.Delete);
            string Header(string name) => req.Headers.GetValues("X-Kwa-" + name).Single();
            var kind = add ? "dns-add" : "dns-remove";
            var value = Header("Id");
            var signed = $"kwa-relay/1\n{kind}\n{Header("Host")}\n{value}\n{Header("Time")}\n{Header("Nonce")}";
            using var verify = RSA.Create();
            verify.ImportFromPem(publicKey);
            Assert.True(verify.VerifyData(Encoding.ASCII.GetBytes(signed), Base64Url.DecodeFromChars(Header("Signature")),
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            Assert.Equal(43, value.Length);
            if (!add) Assert.Equal(DnsRecord, value);
            DnsRecord = add ? value : null;
            DnsChanges++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
        if (req.Method == HttpMethod.Get && url == Directory)
            return Res(HttpStatusCode.OK, new JsonObject
            {
                ["newNonce"] = Base + "/new-nonce", ["newAccount"] = Base + "/new-account", ["newOrder"] = Base + "/new-order",
                ["meta"] = new JsonObject { ["termsOfService"] = Base + "/terms" },
            });
        if (req.Method == HttpMethod.Head && url == Base + "/new-nonce") return Res(HttpStatusCode.OK);
        if (req.Method != HttpMethod.Post) return Res(HttpStatusCode.MethodNotAllowed);
        Assert.Equal("application/jose+json", req.Content!.Headers.ContentType!.MediaType);
        var jws = JsonNode.Parse(await req.Content.ReadAsStringAsync(ct))!.AsObject();
        var protB64 = jws["protected"]!.GetValue<string>();
        var payloadB64 = jws["payload"]!.GetValue<string>();
        var prot = JsonNode.Parse(Base64Url.DecodeFromChars(protB64))!.AsObject();
        Assert.Equal("RS256", prot["alg"]!.GetValue<string>());
        Assert.Equal(url, prot["url"]!.GetValue<string>());
        var nonce = prot["nonce"]!.GetValue<string>();
        if (!_nonces.TryRemove(nonce, out _)) return Problem("badNonce", "unknown nonce");
        if (BadNonceOnce)
        {
            BadNonceOnce = false;
            return Problem("badNonce", "try again");
        }
        RSA key;
        string? account = null;
        if (prot["jwk"] is JsonObject jwk)
        {
            Assert.Null(prot["kid"]);
            key = RSA.Create();
            key.ImportParameters(new RSAParameters { Modulus = Base64Url.DecodeFromChars(jwk["n"]!.GetValue<string>()), Exponent = Base64Url.DecodeFromChars(jwk["e"]!.GetValue<string>()) });
        }
        else
        {
            account = prot["kid"]!.GetValue<string>();
            if (!_accounts.TryGetValue(account, out key!)) return Problem("accountDoesNotExist", "no such account");
        }
        var sig = Base64Url.DecodeFromChars(jws["signature"]!.GetValue<string>());
        if (!key.VerifyData(Encoding.ASCII.GetBytes(protB64 + "." + payloadB64), sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            return Problem("malformed", "bad signature");
        var payload = payloadB64.Length == 0 ? null : JsonNode.Parse(Base64Url.DecodeFromChars(payloadB64));
        var path = new Uri(url).AbsolutePath;

        if (path == "/new-account")
        {
            Assert.True(payload!["termsOfServiceAgreed"]!.GetValue<bool>());
            var kid = Base + "/acct/" + Interlocked.Increment(ref _next);
            _accounts[kid] = key;
            return Res(HttpStatusCode.Created, new JsonObject { ["status"] = "valid" }, kid);
        }
        if (account is null) return Problem("malformed", "kid required");
        if (path == "/new-order")
        {
            var names = payload!["identifiers"]!.AsArray().Select(i => i!["value"]!.GetValue<string>()).ToList();
            if (ExtraAuthzName is not null) names.Add(ExtraAuthzName);
            var ids = names.Select(n =>
            {
                var id = Interlocked.Increment(ref _next);
                _authz[id] = new Authz { Name = n, Token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(24)), Account = account };
                return id;
            }).ToList();
            var oid = Interlocked.Increment(ref _next);
            _orders[oid] = new Order { Names = ExtraAuthzName is null ? names : names[..^1], Authz = ids };
            return Res(HttpStatusCode.Created, OrderJson(oid), $"{Base}/order/{oid}");
        }
        var seg = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        switch (seg[0])
        {
            case "authz":
                return Res(HttpStatusCode.OK, AuthzJson(int.Parse(seg[1])));
            case "chall":
            {
                var a = _authz[int.Parse(seg[1])];
                if (a.Status == "pending") await ValidateAsync(a, ct);
                return Res(HttpStatusCode.OK, AuthzJson(int.Parse(seg[1]))["challenges"]![0]!.DeepClone());
            }
            case "chall-alpn":
            {
                var a = _authz[int.Parse(seg[1])];
                if (a.Status == "pending") await ValidateAlpnAsync(a, ct);
                return Res(HttpStatusCode.OK, AuthzJson(int.Parse(seg[1]))["challenges"]![1]!.DeepClone());
            }
            case "order" when seg.Length == 2:
                return Res(HttpStatusCode.OK, OrderJson(int.Parse(seg[1])));
            case "order" when seg[2] == "finalize":
            {
                var oid = int.Parse(seg[1]);
                var o = _orders[oid];
                if (o.Authz.Any(i => _authz[i].Status != "valid")) return Problem("orderNotReady", "authorizations are not valid");
                var csr = CertificateRequest.LoadSigningRequest(Base64Url.DecodeFromChars(payload!["csr"]!.GetValue<string>()), HashAlgorithmName.SHA256,
                    CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions, RSASignaturePadding.Pkcs1);
                var san = csr.CertificateExtensions.OfType<X509SubjectAlternativeNameExtension>().Single().EnumerateDnsNames().ToList();
                Assert.Equal(o.Names.OrderBy(x => x), san.OrderBy(x => x));
                var leafReq = new CertificateRequest(csr.SubjectName, csr.PublicKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var b = new SubjectAlternativeNameBuilder();
                foreach (var n in san) b.AddDnsName(n);
                leafReq.CertificateExtensions.Add(b.Build());
                leafReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
                var now = DateTimeOffset.UtcNow;
                using var leaf = leafReq.Create(Intermediate, now.AddMinutes(-1), now + Lifetime, RandomNumberGenerator.GetBytes(12));
                o.CertPem = leaf.ExportCertificatePem() + "\n" + Intermediate.ExportCertificatePem() + "\n";
                o.Status = "valid";
                Interlocked.Increment(ref Issued);
                return Res(HttpStatusCode.OK, OrderJson(oid));
            }
            case "cert":
                return Res(HttpStatusCode.OK, text: _orders[int.Parse(seg[1])].CertPem!);
        }
        return Problem("malformed", "unknown path " + path, HttpStatusCode.NotFound);
    }

    private async Task ValidateAsync(Authz a, CancellationToken ct)
    {
        Interlocked.Increment(ref Validations);
        var expected = a.Token + "." + Thumbprint(_accounts[a.Account]);
        if (DnsPublicKey is not null)
        {
            Assert.Equal(Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(expected))), DnsRecord);
            a.Status = "valid";
            return;
        }
        try
        {
            using var http = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });
            using var r = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{_answererPort()}/.well-known/acme-challenge/{a.Token}");
            r.Headers.Host = a.Name;
            lock (ValidatedHosts) ValidatedHosts.Add(a.Name);
            using var res = await http.SendAsync(r, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            var ok = !FailValidation && res.StatusCode == HttpStatusCode.OK && body == expected;
            a.Status = ok ? "valid" : "invalid";
            a.Error = ok ? null : $"answer {(int)res.StatusCode} was not the key authorization";
        }
        catch (HttpRequestException e)
        {
            a.Status = "invalid";
            a.Error = "connection refused: " + e.Message;
        }
    }

    /// <summary>TLS-ALPN-01 (RFC 8737 §3): a TLS handshake to the panel's listener with the name as SNI and only
    /// "acme-tls/1" offered; the certificate must name exactly that name and carry the critical acmeIdentifier extension
    /// holding SHA-256(key authorization).</summary>
    public Func<int>? AlpnPort;

    private async Task ValidateAlpnAsync(Authz a, CancellationToken ct)
    {
        Interlocked.Increment(ref Validations);
        var expected = SHA256.HashData(Encoding.ASCII.GetBytes(a.Token + "." + Thumbprint(_accounts[a.Account])));
        try
        {
            using var tcp = new System.Net.Sockets.TcpClient();
            await tcp.ConnectAsync(IPAddress.Loopback, AlpnPort!(), ct);
            X509Certificate2? seen = null;
            await using var ssl = new System.Net.Security.SslStream(tcp.GetStream(), false, (_, cert, _, _) =>
            {
                seen = X509CertificateLoader.LoadCertificate(cert!.GetRawCertData());
                return true;
            });
            await ssl.AuthenticateAsClientAsync(new System.Net.Security.SslClientAuthenticationOptions
            {
                TargetHost = a.Name, ApplicationProtocols = [new System.Net.Security.SslApplicationProtocol("acme-tls/1")],
            }, ct);
            lock (ValidatedHosts) ValidatedHosts.Add(a.Name);
            var ext = seen!.Extensions.FirstOrDefault(e => e.Oid?.Value == "1.3.6.1.5.5.7.1.31");
            var names = seen.Extensions.OfType<X509SubjectAlternativeNameExtension>().SingleOrDefault()?.EnumerateDnsNames().ToList() ?? [];
            var ok = !FailValidation && ssl.NegotiatedApplicationProtocol.ToString() == "acme-tls/1" && ext is { Critical: true } &&
                     new System.Formats.Asn1.AsnReader(ext.RawData, System.Formats.Asn1.AsnEncodingRules.DER).ReadOctetString().AsSpan().SequenceEqual(expected) &&
                     names.Count == 1 && names[0] == a.Name;
            a.Status = ok ? "valid" : "invalid";
            a.Error = ok ? null : "the tls-alpn-01 certificate was not the challenge's";
        }
        catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException or System.Security.Authentication.AuthenticationException)
        {
            a.Status = "invalid";
            a.Error = "tls-alpn-01 handshake failed: " + e.Message;
        }
    }

    private static string Thumbprint(RSA key)
    {
        var p = key.ExportParameters(false);
        var json = $"{{\"e\":\"{Base64Url.EncodeToString(p.Exponent)}\",\"kty\":\"RSA\",\"n\":\"{Base64Url.EncodeToString(p.Modulus)}\"}}";
        return Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    private JsonObject AuthzJson(int id)
    {
        var a = _authz[id];
        var ch = new JsonObject { ["type"] = DnsPublicKey is null ? "http-01" : "dns-01", ["url"] = $"{Base}/chall/{id}", ["token"] = a.Token, ["status"] = a.Status };
        if (a.Error is not null) ch["error"] = new JsonObject { ["detail"] = a.Error };
        return new JsonObject
        {
            ["identifier"] = new JsonObject { ["type"] = "dns", ["value"] = a.Name }, ["status"] = a.Status,
            ["challenges"] = new JsonArray(ch, new JsonObject { ["type"] = "tls-alpn-01", ["url"] = $"{Base}/chall-alpn/{id}", ["token"] = a.Token }),
        };
    }

    private JsonObject OrderJson(int oid)
    {
        var o = _orders[oid];
        var j = new JsonObject
        {
            ["status"] = o.Status, ["finalize"] = $"{Base}/order/{oid}/finalize",
            ["authorizations"] = new JsonArray([.. o.Authz.Select(i => (JsonNode)$"{Base}/authz/{i}")]),
        };
        if (o.CertPem is not null) j["certificate"] = $"{Base}/cert/{oid}";
        return j;
    }

    protected override void Dispose(bool disposing)
    {
        Root.Dispose();
        Intermediate.Dispose();
        base.Dispose(disposing);
    }
}
