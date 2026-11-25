// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Config;
using Kilnwarden.Hosting;
using Kilnwarden.Kap;
using Kilnwarden.Server;

namespace Kilnwarden.Offline.Support;

/// <summary>The real host with the auth component (weak hashing for speed), listening itself on a free loopback port
/// with TLS off, and optionally a fake Kiln.Admin. Accounts are made straight in the store.</summary>
public sealed class AuthHarness : IAsyncDisposable
{
    public const string Password = "correct horse battery";

    public readonly TempDir Tmp;
    private readonly bool _ownsTmp;

    private AuthHarness(TempDir? root)
    {
        Tmp = root ?? new TempDir();
        _ownsTmp = root is null;
    }
    public readonly TestLog Log = new();
    public FakeKap? Kap { get; private set; }
    public WebAdminHost Host { get; private set; } = null!;

    public string DataDir => Path.Combine(Tmp.Path, "kiln", "data", Product.ModId);
    public string AdminDir => Path.Combine(Tmp.Path, "kiln", "data", "kiln-admin");
    public AuthService Auth => Host.Services.Get<AuthService>();
    public KapClient KapClient => Host.Services.Get<KapClient>();
    public int Port => Host.Server!.LocalEndPoint.Port;
    public string Origin => $"http://127.0.0.1:{Port}";

    public static async Task<AuthHarness> StartAsync(bool kap = true, Action<FakeKap>? setup = null, Action<AuthHarness>? beforeHost = null,
        TimeProvider? time = null, bool waitKap = true, IEnumerable<IWebAdminComponent>? extra = null, TempDir? root = null)
    {
        // root: a folder the test keeps (a second panel on the same data); the harness then leaves it in place
        var h = new AuthHarness(root);
        if (kap)
        {
            h.Kap = new FakeKap(h.AdminDir);
            setup?.Invoke(h.Kap);
        }
        // the per-address attempt bucket would throttle the flows of one test; the lockout tests set their own
        Directory.CreateDirectory(h.DataDir);
        File.WriteAllText(Path.Combine(h.DataDir, "auth.json"), "{\"signIn\":{\"attemptsPerMinutePerIp\":600,\"attemptsPerMinutePerAccount\":60}}");
        // the host's own listener: plain HTTP on a free loopback port
        var settings = new ServerSettings { Port = FreePort() };
        settings.Tls.Mode = TlsMode.Off;
        settings.Limits.RequestsPerSecondPerIp = 10_000;
        settings.Limits.RequestBurstPerIp = 10_000;
        settings.Limits.ConnectionsPerSecondPerIp = 10_000;
        new Kilnwarden.Storage.DataStore(h.DataDir).WriteJson("settings.json", settings);
        beforeHost?.Invoke(h);
        var env = new WebAdminEnvironment
        {
            DataDirectory = h.DataDir, ModDirectory = Path.Combine(h.Tmp.Path, "mod"), ServerDirectory = h.Tmp.Path, Log = h.Log,
        };
        var comp = new AuthComponent(new AuthOptions
        {
            Iterations = 1000,
            Time = time,
            SocketRecheck = TimeSpan.FromMilliseconds(200),
            Kap = o => new KapClientOptions
            {
                AdminDirectory = o.AdminDirectory, Log = o.Log, Backoff = [TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100)],
                KeepaliveTick = TimeSpan.FromMilliseconds(100),
            },
        });
        h.Host = new WebAdminHost(env, [comp, .. extra ?? []]);
        h.Host.Start();
        await h.Host.Started;
        if (kap && waitKap) await Eventually.True(() => h.KapClient.IsReady, 5000, "the KAP client");
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

    /// <summary>An account straight in the store (password <see cref="Password"/>).</summary>
    public async Task<Account> AccountAsync(string name, params string[] roles)
    {
        var hash = await Auth.Hasher.HashAsync(Password, CancellationToken.None);
        return Auth.Accounts.Add(new Account { Name = name, PasswordHash = hash, Roles = roles.Length == 0 ? [BuiltinRoles.Viewer] : roles });
    }

    public Client NewClient() => new(this);

    public async ValueTask DisposeAsync()
    {
        Host.Stop(500);
        Kap?.Dispose();
        await Task.Yield();
        if (_ownsTmp) Tmp.Dispose();
    }

    /// <summary>A browser: cookies, the panel's Origin on every request.</summary>
    public sealed class Client : IDisposable
    {
        private readonly AuthHarness _h;
        public readonly CookieContainer Cookies = new();
        public readonly HttpClient Http;

        public Client(AuthHarness h)
        {
            _h = h;
            Http = new HttpClient(new SocketsHttpHandler { CookieContainer = Cookies, UseCookies = true, AllowAutoRedirect = false })
            {
                BaseAddress = new Uri(h.Origin + "/"),
            };
            Http.DefaultRequestHeaders.Add("Origin", h.Origin);
        }

        public string? Cookie(string name) => Cookies.GetCookies(new Uri(_h.Origin)).FirstOrDefault(c => c.Name == name)?.Value;
        public string? Session => Cookie(AuthCookies.Session);

        public Task<HttpResponseMessage> PostAsync(string path, object? body = null) =>
            Http.PostAsync(path.TrimStart('/'), body is null ? null : Json(body));

        /// <summary>A JSON body with a Content-Length (the server refuses chunked bodies, as JsonContent would send).</summary>
        public static StringContent Json(object body) =>
            new(System.Text.Json.JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        public Task<HttpResponseMessage> GetAsync(string path) => Http.GetAsync(path.TrimStart('/'));

        public async Task<JsonNode?> JsonAsync(HttpResponseMessage r) => JsonNode.Parse(await r.Content.ReadAsStringAsync());

        public async Task<HttpResponseMessage> SignInAsync(string name, string password = Password)
        {
            var r = await PostAsync("/api/auth/login", new { name, password });
            return r;
        }

        /// <summary>The admin pages' KAP socket with this browser's cookies and Origin.</summary>
        public async Task<KapSocket> KapAsync()
        {
            var ws = new ClientWebSocket();
            ws.Options.Cookies = Cookies;
            ws.Options.SetRequestHeader("Origin", _h.Origin);
            await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{_h.Port}/api/server/kap"), CancellationToken.None);
            return new KapSocket(ws);
        }

        public void Dispose() => Http.Dispose();
    }

    /// <summary>The page's side of the KAP socket.</summary>
    public sealed class KapSocket(ClientWebSocket ws) : IDisposable
    {
        public ClientWebSocket Ws { get; } = ws;
        private readonly List<JsonObject> _held = [];
        private ulong _id;

        public async Task SendAsync(JsonObject f) =>
            await Ws.SendAsync(Encoding.UTF8.GetBytes(f.ToJsonString()), WebSocketMessageType.Text, true, CancellationToken.None);

        public async Task SendRawAsync(string text) =>
            await Ws.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);

        /// <summary>The next frame; null when the server closed (see <see cref="ClientWebSocket.CloseStatus"/>).</summary>
        public async Task<JsonObject?> ReadAsync(int ms = 5000)
        {
            using var cts = new CancellationTokenSource(ms);
            var buf = new byte[1 << 20];
            var len = 0;
            while (true)
            {
                var r = await Ws.ReceiveAsync(buf.AsMemory(len), cts.Token);
                if (r.MessageType == WebSocketMessageType.Close) return null;
                len += r.Count;
                if (r.EndOfMessage) return (JsonObject)JsonNode.Parse(buf.AsSpan(0, len))!;
            }
        }

        /// <summary>The next frame that matches (others are kept for later).</summary>
        public async Task<JsonObject> NextAsync(Func<JsonObject, bool> match, int ms = 5000)
        {
            var i = _held.FindIndex(f => match(f));
            if (i >= 0)
            {
                var f = _held[i];
                _held.RemoveAt(i);
                return f;
            }
            var until = Environment.TickCount64 + ms;
            while (true)
            {
                var left = (int)(until - Environment.TickCount64);
                if (left <= 0) throw new TimeoutException("no matching frame; held: " + string.Join(" ", _held.Select(h => h.ToJsonString())));
                var f = await ReadAsync(left) ?? throw new InvalidOperationException($"closed: {Ws.CloseStatus} {Ws.CloseStatusDescription}");
                if (match(f)) return f;
                _held.Add(f);
            }
        }

        public async Task<JsonObject> CallAsync(string op, JsonObject? args = null)
        {
            var id = ++_id;
            await SendAsync(new JsonObject { ["t"] = "req", ["id"] = id, ["op"] = op, ["args"] = args ?? new JsonObject() });
            return await NextAsync(f => f["t"]?.GetValue<string>() == "res" && f["id"]?.GetValue<ulong>() == id);
        }

        public async Task<(JsonObject Hello, JsonObject Ready)> OpenAsync()
        {
            var hello = await NextAsync(f => f["t"]?.GetValue<string>() == "hello");
            var ready = await NextAsync(f => f["t"]?.GetValue<string>() == "ready");
            return (hello, ready);
        }

        /// <summary>Reads until the server closes; the close status.</summary>
        public async Task<WebSocketCloseStatus?> ClosedAsync(int ms = 5000)
        {
            try
            {
                while (await ReadAsync(ms) is not null)
                {
                }
            }
            catch (WebSocketException)
            {
            }
            return Ws.CloseStatus;
        }

        public void Dispose() => Ws.Dispose();
    }
}
