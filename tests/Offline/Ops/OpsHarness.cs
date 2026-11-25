// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Config;
using Kilnwarden.Hosting;
using Kilnwarden.Kap;
using Kilnwarden.Offline.Support;
using Kilnwarden.Ops;

namespace Kilnwarden.Offline.Ops;

/// <summary>A clock the tests move (and a local zone they choose). Timers are the system's (the scheduler's own timer
/// is off in these tests: they call RunDueAsync).</summary>
public sealed class OpsTime(DateTimeOffset start, TimeZoneInfo? local = null) : TimeProvider
{
    private DateTimeOffset _now = start;
    public override DateTimeOffset GetUtcNow() => _now;
    public override TimeZoneInfo LocalTimeZone => local ?? TimeZoneInfo.Utc;
    public void Advance(TimeSpan by) => _now += by;
    public void Set(DateTimeOffset at) => _now = at;
}

/// <summary>The real host with the auth and ops components (no scheduler timer), plain HTTP on loopback, a fake
/// Kiln.Admin, and a clock the test moves. Accounts are made straight in the store.</summary>
public sealed class OpsHarness : IAsyncDisposable
{
    public const string Password = "correct horse battery";

    public TempDir Tmp { get; private init; } = null!;
    public readonly TestLog Log = new();
    public FakeKap Kap { get; private set; } = null!;
    public OpsTime Time { get; private set; } = null!;
    public WebAdminHost Host { get; private set; } = null!;
    internal OpsComponent Ops { get; private set; } = null!;
    public FakeDownloader Downloader { get; } = new();

    public string DataDir => Path.Combine(Tmp.Path, "kiln", "data", Product.ModId);
    public string AdminDir => Path.Combine(Tmp.Path, "kiln", "data", "kiln-admin");
    public string Incoming => Path.Combine(Tmp.Path, "kiln", "staged", "incoming");
    public AuthService Auth => Host.Services.Get<AuthService>();
    public KapClient KapClient => Host.Services.Get<KapClient>();
    public AuditLog Audit => Host.Services.Get<AuditLog>();
    public int Port => Host.Server!.LocalEndPoint.Port;
    public string Origin => $"http://127.0.0.1:{Port}";

    public static readonly DateTimeOffset Start = new(2026, 11, 23, 5, 0, 0, TimeSpan.Zero);

    public static async Task<OpsHarness> StartAsync(Action<FakeKap>? setup = null, OpsHarness? reuse = null, TimeZoneInfo? local = null)
    {
        var h = new OpsHarness { Tmp = reuse?.Tmp ?? new TempDir() };
        h.Time = reuse?.Time ?? new OpsTime(Start, local);
        h.Kap = reuse?.Kap ?? new FakeKap(h.AdminDir);
        if (reuse is null) setup?.Invoke(h.Kap);
        Directory.CreateDirectory(h.DataDir);
        File.WriteAllText(Path.Combine(h.DataDir, "auth.json"), "{\"signIn\":{\"attemptsPerMinutePerIp\":600,\"attemptsPerMinutePerAccount\":60},\"sessionIdleMinutes\":1440}");
        var settings = new ServerSettings { Port = FreePort() };
        settings.Tls.Mode = TlsMode.Off;
        settings.Limits.RequestsPerSecondPerIp = 10_000;
        settings.Limits.RequestBurstPerIp = 10_000;
        settings.Limits.ConnectionsPerSecondPerIp = 10_000;
        new Kilnwarden.Storage.DataStore(h.DataDir).WriteJson("settings.json", settings);
        var env = new WebAdminEnvironment { DataDirectory = h.DataDir, ModDirectory = Path.Combine(h.Tmp.Path, "mod"), ServerDirectory = h.Tmp.Path, Log = h.Log };
        var auth = new AuthComponent(new AuthOptions
        {
            Iterations = 1000, Time = h.Time, SocketRecheck = TimeSpan.FromMilliseconds(200),
            Kap = o => new KapClientOptions
            {
                AdminDirectory = o.AdminDirectory, Log = o.Log, Backoff = [TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100)],
                KeepaliveTick = TimeSpan.FromMilliseconds(100),
            },
        });
        h.Ops = new OpsComponent(new OpsOptions { Time = h.Time, Timer = false, Downloader = h.Downloader });
        h.Host = new WebAdminHost(env, [auth, h.Ops]);
        h.Host.Start();
        await h.Host.Started;
        await Eventually.True(() => h.KapClient.IsReady, 5000, "the KAP client");
        return h;
    }

    private static int FreePort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    public async Task<Account> AccountAsync(string name, params string[] roles)
    {
        var hash = await Auth.Hasher.HashAsync(Password, CancellationToken.None);
        return Auth.Accounts.Add(new Account { Name = name, PasswordHash = hash, Roles = roles.Length == 0 ? [BuiltinRoles.Viewer] : roles });
    }

    /// <summary>A signed-in browser for a new account with <paramref name="roles"/>.</summary>
    public async Task<Client> UserAsync(string name, params string[] roles)
    {
        await AccountAsync(name, roles);
        var c = new Client(this);
        var r = await c.PostAsync("/api/auth/login", new { name, password = Password });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return c;
    }

    /// <summary>Stops the host only (the folder, the fake KAP and the clock stay for a new host).</summary>
    public void StopHost() => Host.Stop(500);

    public async ValueTask DisposeAsync()
    {
        Host.Stop(500);
        Kap.Dispose();
        await Task.Yield();
        Tmp.Dispose();
    }

    public sealed class Client(OpsHarness h) : IDisposable
    {
        public readonly HttpClient Http = Make(h);

        private static HttpClient Make(OpsHarness h)
        {
            var c = new HttpClient(new SocketsHttpHandler { CookieContainer = new CookieContainer(), UseCookies = true, AllowAutoRedirect = false })
            {
                BaseAddress = new Uri(h.Origin + "/"),
            };
            c.DefaultRequestHeaders.Add("Origin", h.Origin);
            return c;
        }

        public static StringContent Json(object body) => new(System.Text.Json.JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        public Task<HttpResponseMessage> PostAsync(string path, object? body = null) => Http.PostAsync(path.TrimStart('/'), body is null ? null : Json(body));
        public Task<HttpResponseMessage> PutAsync(string path, object body) => Http.PutAsync(path.TrimStart('/'), Json(body));
        public Task<HttpResponseMessage> GetAsync(string path) => Http.GetAsync(path.TrimStart('/'));
        public Task<HttpResponseMessage> DeleteAsync(string path) => Http.DeleteAsync(path.TrimStart('/'));

        public Task<HttpResponseMessage> PutBytesAsync(string path, byte[] bytes)
        {
            var c = new ByteArrayContent(bytes);
            c.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            return Http.PutAsync(path.TrimStart('/'), c);
        }

        public async Task ReauthAsync() =>
            Assert.Equal(HttpStatusCode.OK, (await PostAsync("/api/auth/reauth", new { password = Password })).StatusCode);

        public static async Task<JsonNode?> JsonAsync(HttpResponseMessage r) => JsonNode.Parse(await r.Content.ReadAsStringAsync());

        public static async Task<string?> CodeAsync(HttpResponseMessage r) => (await JsonAsync(r))?["error"]?["code"]?.GetValue<string>();

        public void Dispose() => Http.Dispose();
    }
}

/// <summary>A downloader that never touches the network: answers from a table of URL -> bytes.</summary>
public sealed class FakeDownloader : IPackageDownloader
{
    public readonly Dictionary<string, byte[]> Files = [];
    public readonly List<Uri> Asked = [];

    public Task<Uri> DownloadAsync(Uri url, string file, long maxBytes, IReadOnlyList<string> allowedOrigins, CancellationToken ct)
    {
        Asked.Add(url);
        if (UrlPolicy.Problem(url, allowedOrigins) is { } why) throw new DownloadRefusedException(why);
        if (!Files.TryGetValue(url.ToString(), out var bytes)) throw new DownloadRefusedException("404");
        if (bytes.Length > maxBytes) throw new DownloadRefusedException("too large");
        File.WriteAllBytes(file, bytes);
        return Task.FromResult(url);
    }
}

/// <summary>Zips made in memory for the tests.</summary>
public static class Zips
{
    public static byte[] Make(params (string Name, string Content)[] entries) =>
        Make(entries.Select(e => (e.Name, Encoding.UTF8.GetBytes(e.Content), 0)).ToArray());

    public static byte[] Make(params (string Name, byte[] Content, int UnixMode)[] entries)
    {
        using var ms = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
            foreach (var (name, content, mode) in entries)
            {
                var e = zip.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal);
                if (mode != 0) e.ExternalAttributes = mode << 16;
                if (!name.EndsWith('/'))
                    using (var s = e.Open())
                        s.Write(content);
            }
        return ms.ToArray();
    }

    public static string Mod(string id = "cool-mod", string version = "1.2.0") =>
        $"{{\"id\": \"{id}\", \"version\": \"{version}\", \"name\": \"Cool Mod\"}}";
}
