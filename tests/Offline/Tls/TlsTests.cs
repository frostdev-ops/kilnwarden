// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using Kilnwarden.Offline.Server;
using Kilnwarden.Offline.Support;
using Kilnwarden.Storage;
using Kilnwarden.Tls;

namespace Kilnwarden.Offline.Tls;

public class TlsTests
{
    private static readonly string[] Names = ["localhost", "127.0.0.1", "::1"];

    private static SelfSignedCertificateSource Ecdsa(TempDir dir, KwtlsLibrary lib, TimeProvider? time = null, string[]? names = null) =>
        new(new DataStore(dir.Path), "ecdsa", names ?? Names, new TestLog(), time ?? TimeProvider.System, lib.SelfSigned);

    private static SelfSignedCertificateSource Rsa(TempDir dir) =>
        new(new DataStore(dir.Path), "rsa", Names, new TestLog(), TimeProvider.System, (n, cn, f, t) => Pfx.SelfSignedRsa(cn, n, f, t));

    private static string Fingerprint(System.Security.Cryptography.X509Certificates.X509Certificate? c)
    {
        var fp = Convert.ToHexString(SHA256.HashData(c!.GetRawCertData()));
        return string.Join(':', Enumerable.Range(0, fp.Length / 2).Select(i => fp.Substring(i * 2, 2)));
    }

    /// <summary>A TLS client pinned to <paramref name="fingerprint"/> (what the launcher does with a self-signed panel).</summary>
    private static async Task<SslStream> ConnectAsync(int port, string fingerprint, SslProtocols protocols = SslProtocols.None)
    {
        var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, port);
        var ssl = new SslStream(tcp.GetStream(), false);
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = "localhost",
            EnabledSslProtocols = protocols,
            ApplicationProtocols = [SslApplicationProtocol.Http11],
            RemoteCertificateValidationCallback = (_, cert, _, _) => Fingerprint(cert) == fingerprint,
        });
        return ssl;
    }

    [Fact]
    public void Kwtls_version_matches_props_and_cargo()
    {
        var cargo = File.ReadAllText(Path.Combine(Native.RepoRoot, "native", "kwtls", "Cargo.toml"));
        Assert.Contains($"version = \"{Product.KwtlsVersion}\"", cargo);
        var lib = Native.Kwtls();
        Assert.StartsWith(Product.KwtlsVersion + " rustls/", lib.Version);
        Assert.Throws<InvalidOperationException>(() => KwtlsLibrary.Load(Native.KwtlsPath, "9.9.9"));
    }

    [Fact]
    public void Self_signed_is_made_once_kept_and_renewed()
    {
        var lib = Native.Kwtls();
        using var dir = new TempDir();
        var time = new ManualTime();
        var first = Ecdsa(dir, lib, time);
        Assert.True(first.Generated);
        Assert.Equal("ecdsa-p256", first.Current.Info.KeyType);
        Assert.Equal(Names.Order(), first.Current.Info.Names.Select(n => n == "0:0:0:0:0:0:0:1" ? "::1" : n).Order());
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(dir.Path, "tls", "self-signed-ecdsa.key.pem")));
        var again = Ecdsa(dir, lib, time);
        Assert.False(again.Generated);
        Assert.Equal(first.Current.Info.Sha256, again.Current.Info.Sha256);
        var wider = Ecdsa(dir, lib, time, [.. Names, "panel.example.com"]);
        Assert.True(wider.Generated);
        Assert.Contains("lacks panel.example.com", wider.Reason);
        time.Advance(TimeSpan.FromDays(SelfSignedCertificateSource.ValidDays - 10));
        var late = Ecdsa(dir, lib, time, [.. Names, "panel.example.com"]);
        Assert.True(late.Generated);
        Assert.Contains("expires", late.Reason);
    }

    [Fact]
    public async Task Https_over_kwtls_is_tls13_ecdsa_with_info()
    {
        var lib = Native.Kwtls();
        using var dir = new TempDir();
        var src = Ecdsa(dir, lib);
        using var tls = new KwtlsServer(lib, src, ["http/1.1"], new TestLog());
        using var f = new ServerFixture(tls: tls, routes: m => m.MapGet("/tls", Kilnwarden.Routing.RouteAccess.Public,
            ctx => ValueTask.FromResult(Kilnwarden.Http.HttpResponse.Text(200, $"{ctx.Connection.Tls!.Protocol} {ctx.Connection.Tls.CipherSuite} {ctx.Connection.Tls.Alpn} {ctx.Connection.Tls.ServerName} {ctx.Scheme}"))));
        await using var ssl = await ConnectAsync(f.Port, src.Current.Info.Sha256);
        Assert.Equal(SslProtocols.Tls13, ssl.SslProtocol);
        var r = await ServerFixture.SendAsync(ssl, "GET /api/t/tls HTTP/1.1\r\nHost: localhost\r\n\r\n");
        Assert.Matches(@"^TLSv1\.3 TLS13_\S+ http/1\.1 localhost https$", r.Body);
        // keep-alive on the same TLS connection, and a 1 MB body through the stream
        var big = new string('z', 1 << 20);
        f.Settings.Limits.MaxBodyBytes = 2 << 20;
        var echo = await ServerFixture.SendAsync(ssl, $"POST /api/t/echo HTTP/1.1\r\nHost: localhost\r\nContent-Length: {big.Length}\r\n\r\n{big}");
        Assert.Equal(big.Length, echo.Body.Length);
    }

    [Fact]
    public async Task Tls12_clients_still_connect()
    {
        var lib = Native.Kwtls();
        using var dir = new TempDir();
        var src = Ecdsa(dir, lib);
        using var tls = new KwtlsServer(lib, src, ["http/1.1"], new TestLog());
        using var f = new ServerFixture(tls: tls);
        await using var ssl = await ConnectAsync(f.Port, src.Current.Info.Sha256, SslProtocols.Tls12);
        Assert.Equal(SslProtocols.Tls12, ssl.SslProtocol);
        Assert.Equal("hello", (await ServerFixture.SendAsync(ssl, "GET /api/t/hello HTTP/1.1\r\nHost: localhost\r\n\r\n")).Body);
    }

    [Fact]
    public async Task Wss_echo_with_concurrent_send_and_receive()
    {
        var lib = Native.Kwtls();
        using var dir = new TempDir();
        var src = Ecdsa(dir, lib);
        using var tls = new KwtlsServer(lib, src, ["http/1.1"], new TestLog());
        using var f = new ServerFixture(tls: tls, edit: s => s.Limits.MaxWebSocketMessageBytes = 1 << 20);
        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Origin", $"https://localhost:{f.Port}");
        ws.Options.RemoteCertificateValidationCallback = (_, cert, _, _) => Fingerprint(cert) == src.Current.Info.Sha256;
        await ws.ConnectAsync(new Uri($"wss://localhost:{f.Port}/api/t/ws"), CancellationToken.None);
        const int count = 300;
        var payload = Enumerable.Range(0, count).Select(i => Encoding.UTF8.GetBytes(new string((char)('a' + i % 26), 1 + i * 211 % 70_000))).ToArray();
        var send = Task.Run(async () =>
        {
            foreach (var p in payload) await ws.SendAsync(p, WebSocketMessageType.Text, true, CancellationToken.None);
        });
        var buf = new byte[80_000];
        for (var i = 0; i < count; i++)
        {
            var len = 0;
            WebSocketReceiveResult r;
            do
            {
                r = await ws.ReceiveAsync(new ArraySegment<byte>(buf, len, buf.Length - len), new CancellationTokenSource(10_000).Token);
                len += r.Count;
            } while (!r.EndOfMessage);
            Assert.Equal(payload[i].Length, len);
        }
        await send;
        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
    }

    [Fact]
    public async Task Handshake_deadline_and_garbage_do_not_hurt_the_server()
    {
        var lib = Native.Kwtls();
        using var dir = new TempDir();
        var src = Ecdsa(dir, lib);
        using var tls = new KwtlsServer(lib, src, ["http/1.1"], new TestLog());
        using var f = new ServerFixture(tls: tls, edit: s => s.Limits.HandshakeTimeoutMs = 400);
        using (var silent = f.Connect())
            Assert.True(await silent.GetStream().ClosedWithinAsync(2000));
        using (var junk = f.Connect())
        {
            await junk.GetStream().WriteAsync("GET / HTTP/1.1\r\nHost: x\r\n\r\n"u8.ToArray());
            Assert.True(await junk.GetStream().ClosedWithinAsync(2000));
        }
        // the socket closes inside the engine, just before the connection counts the failure
        for (var i = 0; i < 50 && Interlocked.Read(ref f.Shared.Counters.HandshakesFailed) < 2; i++) await Task.Delay(20);
        Assert.Equal(2, Interlocked.Read(ref f.Shared.Counters.HandshakesFailed));
        await using var ssl = await ConnectAsync(f.Port, src.Current.Info.Sha256);
        Assert.Equal("hello", (await ServerFixture.SendAsync(ssl, "GET /api/t/hello HTTP/1.1\r\nHost: localhost\r\n\r\n")).Body);
    }

    [Fact]
    public async Task File_mode_reloads_a_new_pair_and_keeps_the_old_on_a_bad_one()
    {
        var lib = Native.Kwtls();
        using var dir = new TempDir();
        var (certA, keyA) = lib.SelfSigned(Names, "A", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(90));
        var (certB, keyB) = lib.SelfSigned(Names, "B", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(90));
        File.WriteAllBytes(dir.File("fullchain.pem"), certA);
        File.WriteAllBytes(dir.File("key.pem"), keyA);
        var log = new TestLog();
        using var src = new FileCertificateSource(dir.File("fullchain.pem"), dir.File("key.pem"), log, TimeProvider.System, TimeSpan.FromHours(1));
        using var tls = new KwtlsServer(lib, src, ["http/1.1"], log);
        using var f = new ServerFixture(tls: tls);
        var fpA = src.Current.Info.Sha256;
        await using (var a = await ConnectAsync(f.Port, fpA)) Assert.Equal(SslProtocols.Tls13, a.SslProtocol);

        File.WriteAllBytes(dir.File("fullchain.pem"), certB);
        File.WriteAllBytes(dir.File("key.pem"), keyB);
        Assert.True(src.Poll());
        var fpB = tls.Certificate.Sha256;
        Assert.NotEqual(fpA, fpB);
        await using (var b = await ConnectAsync(f.Port, fpB)) Assert.Equal(SslProtocols.Tls13, b.SslProtocol);

        File.WriteAllBytes(dir.File("key.pem"), keyA);   // mismatched pair: refused by the engine
        src.Poll();
        Assert.Equal(fpB, tls.Certificate.Sha256);
        Assert.True(log.Has("keeping the previous one"));
        File.WriteAllText(dir.File("fullchain.pem"), "garbage");
        Assert.False(src.Poll());
        await using (var c = await ConnectAsync(f.Port, fpB)) Assert.Equal(SslProtocols.Tls13, c.SslProtocol);
    }

    [Fact]
    public void Mismatched_pair_is_refused_at_start()
    {
        var lib = Native.Kwtls();
        var (cert, _) = lib.SelfSigned(Names, "A", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(9));
        var (_, key) = lib.SelfSigned(Names, "B", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(9));
        using var dir = new TempDir();
        File.WriteAllBytes(dir.File("c.pem"), cert);
        File.WriteAllBytes(dir.File("k.pem"), key);
        using var src = new FileCertificateSource(dir.File("c.pem"), dir.File("k.pem"), new TestLog(), TimeProvider.System);
        var e = Assert.Throws<InvalidOperationException>(() => new KwtlsServer(lib, src, ["http/1.1"], new TestLog()));
        Assert.Contains("does not match", e.Message);
    }

    [Fact]
    public async Task SslStream_fallback_serves_rsa_and_refuses_ecdsa()
    {
        using var dir = new TempDir();
        var src = Rsa(dir);
        Assert.Equal("rsa-2048", src.Current.Info.KeyType);
        using var tls = new SslStreamServer(src, ["http/1.1"], new TestLog());
        using var f = new ServerFixture(tls: tls);
        await using var ssl = await ConnectAsync(f.Port, src.Current.Info.Sha256);
        Assert.True(ssl.SslProtocol >= SslProtocols.Tls12);
        Assert.Equal("hello", (await ServerFixture.SendAsync(ssl, "GET /api/t/hello HTTP/1.1\r\nHost: localhost\r\n\r\n")).Body);

        using var dir2 = new TempDir();
        using var ec = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var req = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=x", ec, HashAlgorithmName.SHA256);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(9));
        File.WriteAllText(dir2.File("c.pem"), cert.ExportCertificatePem());
        File.WriteAllText(dir2.File("k.pem"), ec.ExportPkcs8PrivateKeyPem());
        using var ecSrc = new FileCertificateSource(dir2.File("c.pem"), dir2.File("k.pem"), new TestLog(), TimeProvider.System);
        Assert.Throws<NotSupportedException>(() => new SslStreamServer(ecSrc, ["http/1.1"], new TestLog()));
    }
}
