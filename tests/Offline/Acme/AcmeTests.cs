// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Kilnwarden.Auth;
using Kilnwarden.Config;
using Kilnwarden.Offline.Support;
using Kilnwarden.Storage;
using Kilnwarden.Tls;
using Kilnwarden.Tls.Acme;

namespace Kilnwarden.Offline.Acme;

public sealed class AcmeTests : IDisposable
{
    private const string Name = "panel.example.test";
    private static readonly AcmePolling Fast = new(TimeSpan.FromMilliseconds(20), 50, TimeSpan.FromSeconds(10));

    private readonly TempDir _tmp = new();
    private readonly TestLog _log = new();
    private readonly DataStore _data;
    private readonly SettingsStore<AcmeSettings> _settings;
    private readonly ServerSettings _server = new();
    private bool _owner = true;
    private AcmeManager? _m;
    private readonly MockAcmeIssuer _ca;

    public AcmeTests()
    {
        _data = new DataStore(_tmp.Path);
        _data.WriteJson("acme.json", Settings());
        _settings = new SettingsStore<AcmeSettings>(_data, "acme.json", _log, AcmeSettings.Validate);
        _server.Tls.Mode = TlsMode.Acme;
        _ca = new MockAcmeIssuer(() => _m?.Responder?.Port ?? 0);
    }

    private static AcmeSettings Settings(Action<AcmeSettings>? f = null)
    {
        var s = new AcmeSettings { Directory = MockAcmeIssuer.Directory, AcceptTerms = true, Names = [Name], Http01Port = 0, Http01Bind = "127.0.0.1", Email = "owner@example.test" };
        f?.Invoke(s);
        return s;
    }

    private AcmeManager Manager(Relay.RelaySettings? relay = null) => _m = new AcmeManager(_data, _settings, () => _server, () => _owner, _log, new AcmeOptions
    {
        Http = new HttpMessageInvoker(_ca, disposeHandler: false), Polling = Fast, FirstCheck = TimeSpan.FromHours(1), Period = TimeSpan.FromHours(1),
    }, () => relay);

    public void Dispose()
    {
        _m?.Dispose();
        _settings.Dispose();
        _ca.Dispose();
        _tmp.Dispose();
    }

    /// <summary>A TLS client handshake against a server built on <paramref name="source"/>; the leaf it was served.</summary>
    private static async Task<X509Certificate2> ServedAsync(ITlsServer server)
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        try
        {
            using var client = new TcpClient();
            var accept = l.AcceptTcpClientAsync();
            await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)l.LocalEndpoint).Port);
            using var srv = await accept;
            using var cts = new CancellationTokenSource(10_000);
            var serverSide = server.AcceptAsync(srv.GetStream(), cts.Token).AsTask();
            X509Certificate2? seen = null;
            await using var ssl = new SslStream(client.GetStream(), false, (_, cert, _, _) =>
            {
                seen = X509CertificateLoader.LoadCertificate(cert!.GetRawCertData());
                return true;
            });
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = Name }, cts.Token);
            await using var s = await serverSide;
            return seen!;
        }
        finally
        {
            l.Stop();
        }
    }

    private static CertificateMaterial SelfSigned()
    {
        var (c, k) = Pfx.SelfSignedRsa("fallback", ["localhost"], DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        return new CertificateMaterial(c, k, CertificateInspector.Inspect(c, k, "self-signed"));
    }

    private sealed class Fixed(CertificateMaterial m) : ICertificateSource
    {
        public string Mode => "self-signed";
        public CertificateMaterial Current => m;
        public event Action<CertificateMaterial>? Changed { add { } remove { } }
        public void Dispose() { }
    }

    [Fact]
    public async Task Http01_order_end_to_end_then_served_without_a_restart()
    {
        var m = Manager();
        using var source = m.Wrap(new Fixed(SelfSigned()));
        using var server = new SslStreamServer(source, ["http/1.1"], _log);
        var before = await ServedAsync(server);
        Assert.Contains("fallback", before.Subject);

        Assert.True(await m.CheckAsync(force: false, CancellationToken.None), _log.ToString());
        Assert.Equal(1, _ca.Validations);
        Assert.Equal([Name], _ca.ValidatedHosts);
        Assert.Equal(1, _ca.Issued);

        var after = await ServedAsync(server);
        Assert.Equal(m.Good!.Info.Sha256.Replace(":", ""), Convert.ToHexString(SHA256.HashData(after.RawData)));
        Assert.Contains(Name, after.GetNameInfo(X509NameType.DnsName, false));
        Assert.Equal("acme", source.Mode);
        Assert.Equal(2, m.Good.Info.ChainLength);
        // the answerer is closed again, and the pair is one owner-only file
        Assert.Null(m.Responder);
        Assert.True(_data.Exists(AcmeManager.CertFile));
        Assert.True(_data.Exists(AcmeManager.AccountFile));
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(_data.PathOf(AcmeManager.CertFile)));
        // still good: no second order
        Assert.False(await m.CheckAsync(force: false, CancellationToken.None));
        Assert.Equal(1, _ca.Issued);
        // a restart loads the stored one and serves it at once
        m.Dispose();
        var m2 = Manager();
        Assert.Equal(m.Good.Info.Sha256, m2.Good!.Info.Sha256);
        // the account is reused (one registration)
        Assert.True(await m2.CheckAsync(force: true, CancellationToken.None));
        Assert.Equal(2, _ca.Issued);
    }

    [Fact]
    public async Task A_failed_renewal_keeps_the_last_good_certificate_and_backs_off()
    {
        var m = Manager();
        Assert.True(await m.CheckAsync(false, CancellationToken.None));
        var good = m.Good!.Info.Sha256;
        _ca.FailValidation = true;
        var changed = 0;
        using var source = m.Wrap(new Fixed(SelfSigned()));
        source.Changed += _ => changed++;
        Assert.False(await m.CheckAsync(force: true, CancellationToken.None));
        Assert.Equal(good, m.Good!.Info.Sha256);
        Assert.Equal(good, source.Current.Info.Sha256);
        Assert.Equal(0, changed);
        Assert.True(_log.Has("keeping the current certificate"));
        var st = m.Status();
        Assert.Equal(1, st["failures"]!.GetValue<int>());
        Assert.Contains("failed", st["lastError"]!.GetValue<string>());
        // within the backoff nothing is ordered, even forced
        _ca.FailValidation = false;
        Assert.NotNull(m.RenewNow());
        Assert.False(await m.CheckAsync(force: true, CancellationToken.None));
        Assert.Equal(1, _ca.Issued);
        // the backoff survives a restart (acme-state.json)
        m.Dispose();
        var m2 = Manager();
        Assert.StartsWith("waiting", m2.RenewNow());
    }

    [Fact]
    public async Task The_weekly_order_budget_holds()
    {
        _settings.Save(Settings(s => s.MaxOrdersPerWeek = 2));
        var m = Manager();
        Assert.True(await m.CheckAsync(true, CancellationToken.None));
        Assert.True(await m.CheckAsync(true, CancellationToken.None));
        Assert.Contains("budget", m.RenewNow());
        Assert.False(await m.CheckAsync(true, CancellationToken.None));
        Assert.Equal(2, _ca.Issued);
    }

    [Fact]
    public async Task Nothing_is_ordered_unless_acme_mode_owner_and_terms()
    {
        var m = Manager();
        _server.Tls.Mode = TlsMode.SelfSigned;
        Assert.Equal("tls.mode is not acme", m.RenewNow());
        _server.Tls.Mode = TlsMode.Acme;
        _owner = false;
        Assert.Equal("no owner account yet (settings.json remoteSetup orders before it)", m.RenewNow());
        _owner = true;
        _settings.Save(Settings(s => s.AcceptTerms = false));
        Assert.Contains("terms", m.RenewNow());
        _settings.Save(Settings(s => s.Names = []));
        Assert.Contains("no names", m.RenewNow());
        Assert.False(await m.CheckAsync(false, CancellationToken.None));
        Assert.Equal(0, _ca.Requests);
        // names fall back to settings.json tls.hostnames (DNS names only)
        _server.Tls.Hostnames = ["192.168.1.5", Name];
        Assert.Equal([Name], m.Names);
        Assert.True(await m.CheckAsync(false, CancellationToken.None));
        Assert.Equal([Name], _ca.ValidatedHosts);
    }

    [Fact]
    public async Task Challenges_are_answered_only_for_configured_names()
    {
        _ca.ExtraAuthzName = "victim.example.test";
        var m = Manager();
        Assert.False(await m.CheckAsync(false, CancellationToken.None));
        Assert.Equal(0, _ca.Validations);
        Assert.Null(m.Good);
        Assert.Contains("authorizations", m.Status()["lastError"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_bad_nonce_is_retried_once()
    {
        _ca.BadNonceOnce = true;
        var m = Manager();
        Assert.True(await m.CheckAsync(false, CancellationToken.None), _log.ToString());
    }

    [Fact]
    public async Task The_answerer_serves_only_published_tokens_for_their_name()
    {
        using var r = new Http01Responder(new IPEndPoint(IPAddress.Loopback, 0), _log);
        r.Publish(Name, "tok_abcdefghijklmnop", "tok_abcdefghijklmnop.thumb");
        using var http = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });
        async Task<(HttpStatusCode, string)> Get(string path, string host, HttpMethod? method = null)
        {
            using var req = new HttpRequestMessage(method ?? HttpMethod.Get, $"http://127.0.0.1:{r.Port}{path}");
            req.Headers.Host = host;
            using var res = await http.SendAsync(req);
            return (res.StatusCode, await res.Content.ReadAsStringAsync());
        }
        Assert.Equal((HttpStatusCode.OK, "tok_abcdefghijklmnop.thumb"), await Get("/.well-known/acme-challenge/tok_abcdefghijklmnop", Name));
        Assert.Equal((HttpStatusCode.OK, "tok_abcdefghijklmnop.thumb"), await Get("/.well-known/acme-challenge/tok_abcdefghijklmnop", Name.ToUpperInvariant() + ":80"));
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/.well-known/acme-challenge/tok_abcdefghijklmnop", "other.example.test")).Item1);
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/.well-known/acme-challenge/other_token_123456", Name)).Item1);
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/api/auth/status", Name)).Item1);
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/.well-known/acme-challenge/tok_abcdefghijklmnop", Name, HttpMethod.Post)).Item1);
        var port = r.Port;
        r.Clear();
        Assert.Equal(0, r.Port);
        using var c = new TcpClient();
        await Assert.ThrowsAnyAsync<SocketException>(() => c.ConnectAsync(IPAddress.Loopback, port));
    }

    [Fact]
    public void Certificate_checks_refuse_bad_material()
    {
        var now = DateTimeOffset.UtcNow;
        using var leafKey = RSA.Create(2048);
        X509Certificate2 Leaf(X509Certificate2 issuer, params string[] names)
        {
            var req = new CertificateRequest("CN=" + names[0], leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var b = new SubjectAlternativeNameBuilder();
            foreach (var n in names) b.AddDnsName(n);
            req.CertificateExtensions.Add(b.Build());
            return req.Create(issuer, now.AddMinutes(-1), now.AddDays(30), [9, 9]);
        }
        var key = Encoding.ASCII.GetBytes(leafKey.ExportPkcs8PrivateKeyPem());
        byte[] Chain(params X509Certificate2[] certs) => Encoding.ASCII.GetBytes(string.Join("\n", certs.Select(c => c.ExportCertificatePem())));
        using var good = Leaf(_ca.Intermediate, Name);
        Assert.Equal(2, CertificateCheck.Check(Chain(good, _ca.Intermediate), key, [Name], now).ChainLength);
        // missing name
        Assert.Throws<CryptographicException>(() => CertificateCheck.Check(Chain(good, _ca.Intermediate), key, [Name, "b.example.test"], now));
        // leaf only
        Assert.Throws<CryptographicException>(() => CertificateCheck.Check(Chain(good), key, [Name], now));
        // someone else's key
        using var other = RSA.Create(2048);
        Assert.Throws<CryptographicException>(() => CertificateCheck.Check(Chain(good, _ca.Intermediate), Encoding.ASCII.GetBytes(other.ExportPkcs8PrivateKeyPem()), [Name], now));
        // an impostor intermediate with the same name but another key
        using var fakeKey = RSA.Create(2048);
        var fakeReq = new CertificateRequest(_ca.Intermediate.SubjectName, fakeKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        fakeReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var fake = fakeReq.CreateSelfSigned(now.AddDays(-1), now.AddDays(10));
        Assert.Throws<CryptographicException>(() => CertificateCheck.Check(Chain(good, fake), key, [Name], now));
        // expired
        Assert.Throws<CryptographicException>(() => CertificateCheck.Check(Chain(good, _ca.Intermediate), key, [Name], now.AddDays(31)));
    }

    [Theory]
    [InlineData("other", "challenge must be http-01, tls-alpn-01 or dns-01")]
    public void Unsupported_challenges_are_settings_errors(string challenge, string message)
    {
        Assert.Contains(AcmeSettings.Validate(Settings(s => s.Challenge = challenge)), p => p.Contains(message));
    }

    [Fact]
    public async Task Dns01_validates_the_registered_relay_name_and_removes_the_record()
    {
        _settings.Save(Settings(s => { s.Challenge = "dns-01"; s.Dns01PropagationSeconds = 5; }));
        using var key = new Relay.RelayKey(_data);
        _ca.DnsPublicKey = File.ReadAllText(_data.PathOf("relay-public.pem"));
        var relay = new Relay.RelaySettings { Enabled = true, Origin = MockAcmeIssuer.Relay, Host = Name };
        var manager = Manager(relay);
        Assert.True(await manager.CheckAsync(false, TestContext.Current.CancellationToken), manager.Status().ToJsonString());
        Assert.Equal(1, _ca.Issued);
        Assert.Equal(2, _ca.DnsChanges);
        Assert.Null(_ca.DnsRecord);
        Assert.Null(manager.Responder);
        Assert.Contains(Name, manager.Good!.Info.Names);
    }

    /// <summary>A listener whose TLS server is the panel's own (SslStream with a self-signed RSA certificate) wrapped in the
    /// TLS-ALPN-01 answerer, accepting until the test ends.</summary>
    private static (TcpListener Listener, Task Loop, ITlsServer Server) AlpnListener(AlpnChallenges alpn, CancellationToken ct, KwtlsLibrary? kwtls = null)
    {
        var now = DateTimeOffset.UtcNow;
        var (cert, key) = Pfx.SelfSignedRsa("panel", [Name], now.AddDays(-1), now.AddDays(30));
        var source = new FixedCertificateSource(new CertificateMaterial(cert, key, CertificateInspector.Inspect(cert, key, "self-signed")), "self-signed");
        ITlsServer Engine(ICertificateSource s, string[] alpnList) =>
            kwtls is not null ? new KwtlsServer(kwtls, s, alpnList, new TestLog()) : new SslStreamServer(s, alpnList, new TestLog());
        var main = Engine(source, ["http/1.1"]);
        // as ListenerManager does: the challenge always through SslStream (kwtls refuses the critical acmeIdentifier)
        var server = new AlpnChallengeServer(main, alpn, m => new SslStreamServer(new FixedCertificateSource(m, "acme-challenge"), [AlpnChallenges.Protocol], new TestLog()));
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var loop = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient c;
                try { c = await l.AcceptTcpClientAsync(ct); }
                catch (Exception) { return; }
                _ = Task.Run(async () =>
                {
                    using (c)
                    {
                        try
                        {
                            await using var s = await server.AcceptAsync(c.GetStream(), ct);
                            var buf = new byte[1];
                            using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
                            idle.CancelAfter(2000);
                            await s.ReadAsync(buf, idle.Token);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }, ct);
            }
        }, ct);
        return (l, loop, server);
    }

    private static async Task<(X509Certificate2 Cert, string Alpn)> HandshakeAsync(int port, string name, string alpn)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, port);
        X509Certificate2? seen = null;
        await using var ssl = new SslStream(tcp.GetStream(), false, (_, cert, _, _) =>
        {
            seen = X509CertificateLoader.LoadCertificate(cert!.GetRawCertData());
            return true;
        });
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = name, ApplicationProtocols = [new SslApplicationProtocol(alpn)] });
        return (seen!, ssl.NegotiatedApplicationProtocol.ToString());
    }

    [Fact]
    public async Task ClientHello_gives_the_server_name_and_ALPN_of_a_real_hello()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        try
        {
            using var tcp = new TcpClient();
            var accept = l.AcceptTcpClientAsync();
            await tcp.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)l.LocalEndpoint).Port);
            using var server = await accept;
            using var cts = new CancellationTokenSource(5000);
            var ssl = new SslStream(tcp.GetStream(), false, (_, _, _, _) => true);
            var hs = ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = "Panel.Example.Test", ApplicationProtocols = [new SslApplicationProtocol("acme-tls/1"), SslApplicationProtocol.Http11],
            }, cts.Token);
            var (read, hello) = await ClientHello.PeekAsync(server.GetStream(), cts.Token);
            Assert.NotNull(hello);
            Assert.Equal("panel.example.test", hello.ServerName);
            Assert.Equal(["acme-tls/1", "http/1.1"], hello.Alpn);
            var again = ClientHello.Parse(read)!;
            Assert.Equal(hello.ServerName, again.ServerName);
            Assert.Equal(hello.Alpn, again.Alpn);
            server.Close();
            await Assert.ThrowsAnyAsync<Exception>(() => hs);
            ssl.Dispose();
        }
        finally
        {
            l.Stop();
        }
        // not TLS, cut short, or garbage: no information, no exception
        Assert.Null(ClientHello.Parse("GET / HTTP/1.1\r\n\r\n"u8));
        Assert.Null(ClientHello.Parse([0x16, 0x03, 0x01, 0x00, 0x10, 0x01]));
        Assert.Null(ClientHello.Parse([0x16, 0x03, 0x01, 0xff, 0xff]));
    }

    [Theory]
    [InlineData("sslstream")]
    [InlineData("kwtls")]
    public async Task TlsAlpn01_challenges_are_answered_by_the_listener_and_other_clients_get_the_panel_certificate(string engine)
    {
        var alpn = new AlpnChallenges();
        using var cts = new CancellationTokenSource(30_000);
        var (l, loop, server) = AlpnListener(alpn, cts.Token, engine == "kwtls" ? Native.Kwtls() : null);
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        try
        {
            // no challenge: straight through
            var (plain, proto) = await HandshakeAsync(port, Name, "http/1.1");
            Assert.Null(plain.Extensions.FirstOrDefault(e => e.Oid?.Value == AlpnChallenges.AcmeIdentifierOid));
            Assert.Equal("http/1.1", proto);
            alpn.Publish(Name, "token.thumbprint");
            var (challenge, cproto) = await HandshakeAsync(port, Name, AlpnChallenges.Protocol);
            Assert.Equal("acme-tls/1", cproto);
            var ext = challenge.Extensions.Single(e => e.Oid?.Value == AlpnChallenges.AcmeIdentifierOid);
            Assert.True(ext.Critical);
            Assert.Equal(SHA256.HashData("token.thumbprint"u8), new System.Formats.Asn1.AsnReader(ext.RawData, System.Formats.Asn1.AsnEncodingRules.DER).ReadOctetString());
            // the same listener still serves browsers the panel's own certificate, and other names no challenge
            (plain, _) = await HandshakeAsync(port, Name, "http/1.1");
            Assert.Null(plain.Extensions.FirstOrDefault(e => e.Oid?.Value == AlpnChallenges.AcmeIdentifierOid));
            await Assert.ThrowsAnyAsync<Exception>(() => HandshakeAsync(port, "other.example.test", AlpnChallenges.Protocol));
        }
        finally
        {
            cts.Cancel();
            l.Stop();
            server.Dispose();
        }
    }

    /// <summary>Why the challenge is served by SslStream: kwtls (rustls/webpki) refuses to load a certificate with a
    /// critical extension it does not know, and RFC 8737 makes acmeIdentifier critical.</summary>
    [Fact]
    public void Kwtls_refuses_the_challenge_certificate_so_SslStream_serves_it()
    {
        var lib = Native.Kwtls();
        var m = AlpnChallenges.Create(Name, "token.thumbprint");
        var e = Assert.Throws<InvalidOperationException>(() => new KwtlsServer(lib, new FixedCertificateSource(m, "x"), [AlpnChallenges.Protocol], new TestLog()));
        Assert.Contains("UnsupportedCriticalExtension", e.Message);
        using var ssl = new SslStreamServer(new FixedCertificateSource(m, "x"), [AlpnChallenges.Protocol], new TestLog());
        Assert.Equal("acme-challenge", m.Info.Source);
    }

    [Fact]
    public async Task TlsAlpn01_orders_a_certificate_through_the_listener()
    {
        _settings.Save(Settings(s => s.Challenge = "tls-alpn-01"));
        var manager = Manager();
        using var cts = new CancellationTokenSource(30_000);
        var (l, loop, server) = AlpnListener(manager.Alpn, cts.Token);
        _ca.AlpnPort = () => ((IPEndPoint)l.LocalEndpoint).Port;
        try
        {
            Assert.True(await manager.CheckAsync(false, TestContext.Current.CancellationToken), manager.Status().ToJsonString());
            Assert.Equal(1, _ca.Issued);
            Assert.Contains(Name, _ca.ValidatedHosts);
            Assert.Equal(0, manager.Alpn.Count); // cleared after the order
            Assert.Null(manager.Responder); // no http-01 answerer was started
        }
        finally
        {
            cts.Cancel();
            l.Stop();
            server.Dispose();
        }
    }

    /// <summary>Security review S2: a relay that cannot be reached is an ordinary failure (backoff, counted, logged),
    /// never an exception out of the check that leaves the phase at "ordering" and burns the weekly budget.</summary>
    [Fact]
    public async Task Dns01_with_an_unreachable_relay_fails_and_backs_off()
    {
        _settings.Save(Settings(s => { s.Challenge = "dns-01"; s.Dns01PropagationSeconds = 5; }));
        using var key = new Relay.RelayKey(_data);
        _ca.DnsPublicKey = File.ReadAllText(_data.PathOf("relay-public.pem"));
        _ca.RelayDown = true;
        var manager = Manager(new Relay.RelaySettings { Enabled = true, Origin = MockAcmeIssuer.Relay, Host = Name });
        Assert.False(await manager.CheckAsync(false, TestContext.Current.CancellationToken));
        var st = manager.Status();
        Assert.Equal("failed", st["phase"]!.GetValue<string>());
        Assert.Equal(1, st["failures"]!.GetValue<int>());
        Assert.NotNull(st["nextAttempt"]);
        Assert.Contains("relay", st["lastError"]!.GetValue<string>());
        // the backoff holds: no second order right away
        Assert.False(await manager.CheckAsync(false, TestContext.Current.CancellationToken));
        Assert.Equal(1, manager.Status()["ordersThisWeek"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("*.example.test")]
    [InlineData("10.0.0.1")]
    [InlineData("localhost")]
    [InlineData("a b.example")]
    [InlineData("nodot")]
    public void Only_dns_names_are_certified(string name)
    {
        Assert.NotEmpty(AcmeSettings.Validate(Settings(s => s.Names = [name])));
        Assert.Contains(AcmeSettings.Validate(Settings(s => s.Directory = "http://acme.test/directory")), p => p.Contains("https"));
    }
}
