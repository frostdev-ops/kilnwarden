// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Config;
using Kilnwarden.Hosting;
using Kilnwarden.Storage;

namespace Kilnwarden.Offline.Support;

/// <summary>The real host with the auth component (weak hashing, no Kiln.Admin) plus a lane's components, listening on
/// a free loopback port (plain HTTP unless the test changes settings.json first). For the step 2 identity and ACME
/// tests; it keeps AuthHarness untouched.</summary>
public sealed class LaneHost : IAsyncDisposable
{
    public const string Password = AuthHarness.Password;

    public readonly TempDir Tmp = new();
    public readonly TestLog Log = new();
    public WebAdminHost Host { get; private set; } = null!;
    public string DataDir => Path.Combine(Tmp.Path, "kiln", "data", Product.ModId);
    public DataStore Data => new(DataDir);
    public AuthService Auth => Host.Services.Get<AuthService>();
    public int Port => Host.Server!.LocalEndPoint.Port;
    public bool Https { get; private set; }
    public string Origin => $"{(Https ? "https" : "http")}://127.0.0.1:{Port}";

    public static async Task<LaneHost> StartAsync(Func<IReadOnlyList<IWebAdminComponent>> components, Action<LaneHost, ServerSettings>? before = null)
    {
        var h = new LaneHost();
        Directory.CreateDirectory(h.DataDir);
        File.WriteAllText(Path.Combine(h.DataDir, "auth.json"), "{\"signIn\":{\"attemptsPerMinutePerIp\":600,\"attemptsPerMinutePerAccount\":60}}");
        var settings = new ServerSettings { Port = FreePort() };
        settings.Tls.Mode = TlsMode.Off;
        settings.Limits.RequestsPerSecondPerIp = 10_000;
        settings.Limits.RequestBurstPerIp = 10_000;
        settings.Limits.ConnectionsPerSecondPerIp = 10_000;
        before?.Invoke(h, settings);
        h.Https = settings.Tls.Mode != TlsMode.Off;
        h.Data.WriteJson("settings.json", settings);
        var env = new WebAdminEnvironment
        {
            DataDirectory = h.DataDir, ModDirectory = Path.Combine(h.Tmp.Path, "mod"), ServerDirectory = h.Tmp.Path, Log = h.Log,
        };
        var auth = new AuthComponent(new AuthOptions { Iterations = 1000, Kap = o => new Kilnwarden.Kap.KapClientOptions { AdminDirectory = o.AdminDirectory, Log = o.Log } });
        h.Host = new WebAdminHost(env, [auth, .. components()]);
        h.Host.Start();
        await h.Host.Started;
        return h;
    }

    public static int FreePort()
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

    public Browser NewBrowser() => new(this);

    public async ValueTask DisposeAsync()
    {
        Host.Stop(500);
        await Task.Yield();
        Tmp.Dispose();
    }

    /// <summary>A browser: cookies, the panel's Origin on every request, no redirects followed.</summary>
    public sealed class Browser : IDisposable
    {
        private readonly LaneHost _h;
        public readonly CookieContainer Cookies = new();
        public readonly HttpClient Http;

        public Browser(LaneHost h)
        {
            _h = h;
            Http = new HttpClient(new SocketsHttpHandler
            {
                CookieContainer = Cookies, UseCookies = true, AllowAutoRedirect = false,
                SslOptions = new System.Net.Security.SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, _, _, _) => true },
            }) { BaseAddress = new Uri(h.Origin + "/") };
            Http.DefaultRequestHeaders.Add("Origin", h.Origin);
        }

        public string? Cookie(string name) => Cookies.GetCookies(new Uri(_h.Origin)).FirstOrDefault(c => c.Name == name)?.Value;

        public Task<HttpResponseMessage> PostAsync(string path, object? body = null) =>
            Http.PostAsync(path.TrimStart('/'), body is null ? null : new StringContent(System.Text.Json.JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

        public Task<HttpResponseMessage> GetAsync(string path) => Http.GetAsync(path.TrimStart('/'));

        public Task<HttpResponseMessage> DeleteAsync(string path) => Http.DeleteAsync(path.TrimStart('/'));

        public static async Task<JsonNode?> JsonAsync(HttpResponseMessage r) => JsonNode.Parse(await r.Content.ReadAsStringAsync());

        public async Task SignInAsync(string name)
        {
            var r = await PostAsync("/api/auth/login", new { name, password = Password });
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        public async Task ReauthAsync()
        {
            var r = await PostAsync("/api/auth/reauth", new { password = Password });
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        public void Dispose() => Http.Dispose();
    }
}
