// SPDX-License-Identifier: MIT
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;

namespace Kilnwarden.Tls.Acme;

/// <summary>An ACME problem (RFC 8555 §6.7) or a protocol failure.</summary>
public sealed class AcmeException(string message, string? type = null) : Exception(message)
{
    public string? Type { get; } = type;
}

/// <summary>Timings (tests shorten them).</summary>
public sealed record AcmePolling(TimeSpan Interval, int Attempts, TimeSpan RequestTimeout)
{
    public static AcmePolling Default { get; } = new(TimeSpan.FromSeconds(2), 60, TimeSpan.FromSeconds(30));
}

/// <summary>What the client needs to answer an http-01 challenge: publish token -> key authorization for a name.</summary>
public interface IHttp01Publisher
{
    void Publish(string name, string token, string keyAuthorization);
    void Clear();
}

/// <summary>A minimal RFC 8555 client: account, order, http-01 authorizations, finalize, download. Every URL it follows
/// must be https on the directory's own host (no redirects followed, bounded bodies and polling), so a hostile answer
/// cannot point it elsewhere. One order at a time per instance.</summary>
public sealed class AcmeClient
{
    public const int MaxBodyBytes = 1 << 20;

    private readonly HttpMessageInvoker _http;
    private readonly Uri _directoryUrl;
    private readonly AcmeJws _jws;
    private readonly AcmePolling _poll;
    private JsonObject? _dir;
    private string? _nonce;

    public AcmeClient(HttpMessageInvoker http, Uri directory, RSA accountKey, string? kid, AcmePolling? polling = null)
    {
        if (directory.Scheme != "https") throw new ArgumentException("the ACME directory must be https");
        _http = http;
        _directoryUrl = directory;
        _jws = new AcmeJws(accountKey);
        Kid = kid;
        _poll = polling ?? AcmePolling.Default;
    }

    /// <summary>The account URL once registered (kept with the account key).</summary>
    public string? Kid { get; private set; }

    public AcmeJws Jws => _jws;

    private Uri Checked(string? url, string what)
    {
        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != "https" ||
            !string.Equals(u.Host, _directoryUrl.Host, StringComparison.OrdinalIgnoreCase) || u.Port != _directoryUrl.Port || u.UserInfo.Length > 0)
            throw new AcmeException($"the ACME server gave an unusable {what} URL");
        return u;
    }

    private async Task<JsonObject> DirectoryAsync(CancellationToken ct)
    {
        if (_dir is not null) return _dir;
        using var req = new HttpRequestMessage(HttpMethod.Get, _directoryUrl);
        var (res, body) = await SendAsync(req, ct).ConfigureAwait(false);
        using (res)
        {
            if (!res.IsSuccessStatusCode) throw new AcmeException($"the ACME directory answered {(int)res.StatusCode}");
            return _dir = JsonNode.Parse(body) as JsonObject ?? throw new AcmeException("the ACME directory is not JSON");
        }
    }

    private string DirUrl(JsonObject dir, string name) => Checked(dir[name]?.GetValue<string>(), name).AbsoluteUri;

    private async Task<(HttpResponseMessage, byte[])> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_poll.RequestTimeout);
        HttpResponseMessage res;
        try
        {
            res = await _http.SendAsync(req, cts.Token).ConfigureAwait(false);
        }
        catch (HttpRequestException e)
        {
            throw new AcmeException("the ACME server cannot be reached: " + e.Message);
        }
        if (res.Headers.TryGetValues("Replay-Nonce", out var n) && n.FirstOrDefault() is { Length: > 0 and < 256 } nonce) _nonce = nonce;
        await using var s = await res.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
        using var ms = new MemoryStream();
        var buf = new byte[16384];
        int r;
        while ((r = await s.ReadAsync(buf, cts.Token).ConfigureAwait(false)) > 0)
        {
            if (ms.Length + r > MaxBodyBytes) throw new AcmeException("the ACME answer is too large");
            ms.Write(buf, 0, r);
        }
        return (res, ms.ToArray());
    }

    private async Task<string> NonceAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _nonce, null) is { } n) return n;
        var dir = await DirectoryAsync(ct).ConfigureAwait(false);
        using var req = new HttpRequestMessage(HttpMethod.Head, DirUrl(dir, "newNonce"));
        var (res, _) = await SendAsync(req, ct).ConfigureAwait(false);
        res.Dispose();
        return Interlocked.Exchange(ref _nonce, null) ?? throw new AcmeException("the ACME server gave no nonce");
    }

    /// <summary>A signed POST; retries once on badNonce. Returns the response (caller disposes) and its body.</summary>
    private async Task<(HttpResponseMessage Res, byte[] Body)> PostAsync(string url, JsonNode? payload, CancellationToken ct, bool useJwk = false, string? accept = null)
    {
        for (var attempt = 0; ; attempt++)
        {
            var nonce = await NonceAsync(ct).ConfigureAwait(false);
            var jws = _jws.Sign(url, nonce, payload, useJwk ? null : Kid ?? throw new InvalidOperationException("no ACME account"));
            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(jws, Encoding.UTF8) };
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/jose+json");
            if (accept is not null) req.Headers.Accept.ParseAdd(accept);
            var (res, body) = await SendAsync(req, ct).ConfigureAwait(false);
            if (res.IsSuccessStatusCode) return (res, body);
            var problem = Problem(body);
            res.Dispose();
            if (problem.Type == "urn:ietf:params:acme:error:badNonce" && attempt == 0) continue;
            throw problem;
        }
    }

    private static AcmeException Problem(byte[] body)
    {
        try
        {
            if (JsonNode.Parse(body) is JsonObject o)
            {
                var type = o["type"]?.GetValue<string>();
                var detail = o["detail"]?.GetValue<string>() ?? type ?? "error";
                return new AcmeException("ACME: " + (detail.Length > 300 ? detail[..300] : detail), type);
            }
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or InvalidOperationException)
        {
        }
        return new AcmeException("ACME: the server refused the request");
    }

    private static JsonObject Obj(byte[] body) =>
        (JsonNode.Parse(body) as JsonObject) ?? throw new AcmeException("ACME: an answer is not a JSON object");

    /// <summary>Registers (or finds) the account for the key; terms must have been accepted by the owner.</summary>
    public async Task<string> EnsureAccountAsync(string? email, CancellationToken ct)
    {
        if (Kid is not null) return Kid;
        var dir = await DirectoryAsync(ct).ConfigureAwait(false);
        var payload = new JsonObject { ["termsOfServiceAgreed"] = true };
        if (!string.IsNullOrEmpty(email)) payload["contact"] = new JsonArray("mailto:" + email);
        var (res, _) = await PostAsync(DirUrl(dir, "newAccount"), payload, ct, useJwk: true).ConfigureAwait(false);
        using (res) Kid = Checked(res.Headers.Location?.AbsoluteUri, "account").AbsoluteUri;
        return Kid;
    }

    /// <summary>Orders a certificate for <paramref name="names"/> with http-01 and returns the PEM chain (leaf first).
    /// <paramref name="csrDer"/> is the request for the new key.</summary>
    public async Task<string> OrderAsync(IReadOnlyList<string> names, byte[] csrDer, IHttp01Publisher publisher, CancellationToken ct)
    {
        try
        {
            return await OrderAsync(names, csrDer, "http-01", (name, token, keyAuthorization, _) =>
            {
                publisher.Publish(name, token, keyAuthorization);
                return Task.CompletedTask;
            }, ct).ConfigureAwait(false);
        }
        finally { publisher.Clear(); }
    }

    internal async Task<string> OrderAsync(IReadOnlyList<string> names, byte[] csrDer, string challenge,
        Func<string, string, string, CancellationToken, Task> publish, CancellationToken ct)
    {
        var dir = await DirectoryAsync(ct).ConfigureAwait(false);
        var ids = new JsonArray([.. names.Select(n => (JsonNode)new JsonObject { ["type"] = "dns", ["value"] = n })]);
        var (res, body) = await PostAsync(DirUrl(dir, "newOrder"), new JsonObject { ["identifiers"] = ids }, ct).ConfigureAwait(false);
        string orderUrl;
        using (res) orderUrl = Checked(res.Headers.Location?.AbsoluteUri, "order").AbsoluteUri;
        var order = Obj(body);
        var authzs = order["authorizations"] as JsonArray ?? throw new AcmeException("ACME: the order has no authorizations");
        if (authzs.Count > names.Count) throw new AcmeException("ACME: the order has more authorizations than names");
        var pending = new List<string>();
        foreach (var a in authzs)
        {
            var url = Checked(a?.GetValue<string>(), "authorization").AbsoluteUri;
            var authz = await GetAsync(url, ct).ConfigureAwait(false);
            var name = authz["identifier"]?["value"]?.GetValue<string>();
            if (name is null || !names.Contains(name, StringComparer.OrdinalIgnoreCase)) throw new AcmeException("ACME: an authorization is for a name not asked for");
            if (authz["status"]?.GetValue<string>() == "valid") continue;
            var ch = (authz["challenges"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(c => c["type"]?.GetValue<string>() == challenge)
                     ?? throw new AcmeException($"ACME: no {challenge} challenge for {name}");
            var token = ch["token"]?.GetValue<string>();
            if (token is null || token.Length is < 16 or > 128 || !token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
                throw new AcmeException("ACME: malformed challenge token");
            var chUrl = Checked(ch["url"]?.GetValue<string>(), "challenge").AbsoluteUri;
            await publish(name, token, _jws.KeyAuthorization(token), ct).ConfigureAwait(false);
            var (r2, _) = await PostAsync(chUrl, new JsonObject(), ct).ConfigureAwait(false);
            r2.Dispose();
            pending.Add(url);
        }
        foreach (var url in pending)
            await WaitAsync(url, "authorization", ct).ConfigureAwait(false);
        var (fres, _) = await PostAsync(Checked(order["finalize"]?.GetValue<string>(), "finalize").AbsoluteUri,
            new JsonObject { ["csr"] = AcmeJws.B64(csrDer) }, ct).ConfigureAwait(false);
        fres.Dispose();
        var done = await WaitAsync(orderUrl, "order", ct).ConfigureAwait(false);
        var certUrl = Checked(done["certificate"]?.GetValue<string>(), "certificate").AbsoluteUri;
        var (cres, cbody) = await PostAsync(certUrl, null, ct, accept: "application/pem-certificate-chain").ConfigureAwait(false);
        cres.Dispose();
        return Encoding.ASCII.GetString(cbody);
    }

    private async Task<JsonObject> GetAsync(string url, CancellationToken ct)
    {
        var (res, body) = await PostAsync(url, null, ct).ConfigureAwait(false);
        res.Dispose();
        return Obj(body);
    }

    private async Task<JsonObject> WaitAsync(string url, string what, CancellationToken ct)
    {
        for (var i = 0; i < _poll.Attempts; i++)
        {
            var o = await GetAsync(url, ct).ConfigureAwait(false);
            switch (o["status"]?.GetValue<string>())
            {
                case "valid":
                    return o;
                case "invalid":
                    var detail = (o["challenges"] as JsonArray)?.OfType<JsonObject>().Select(c => c["error"]?["detail"]?.GetValue<string>()).FirstOrDefault(d => d is not null)
                                 ?? o["error"]?["detail"]?.GetValue<string>();
                    throw new AcmeException($"ACME: the {what} failed" + (detail is null ? "" : ": " + (detail.Length > 300 ? detail[..300] : detail)));
            }
            await Task.Delay(_poll.Interval, ct).ConfigureAwait(false);
        }
        throw new AcmeException($"ACME: the {what} did not finish in time");
    }

    /// <summary>A CSR for a new RSA-2048 key (both TLS engines serve RSA; the SslStream fallback only RSA).</summary>
    public static byte[] Csr(RSA key, IReadOnlyList<string> names)
    {
        var req = new CertificateRequest(new X500DistinguishedName("CN=" + names[0]), key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        foreach (var n in names) san.AddDnsName(n);
        req.CertificateExtensions.Add(san.Build());
        return req.CreateSigningRequest();
    }
}
