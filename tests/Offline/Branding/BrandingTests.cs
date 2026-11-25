// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Kilnwarden.Branding;
using Kilnwarden.Config;
using Kilnwarden.Hosting;
using Kilnwarden.Offline.Support;
using Kilnwarden.Storage;

namespace Kilnwarden.Offline.Branding;

/// <summary>The panel's branding (the server name, accent, logo and favicon the web app shows before sign-in): public,
/// read-only, from branding.json and files in branding/ that no request can name.</summary>
public sealed class BrandingTests
{
    internal static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52, 1, 2, 3];
    internal static readonly byte[] Ico = [0, 0, 1, 0, 1, 0, 16, 16, 0, 0, 1, 0, 32, 0];
    internal static readonly byte[] Webp = [.. "RIFF"u8, 30, 0, 0, 0, .. "WEBPVP8 "u8, 1, 2, 3, 4];
    internal const string Svg = "<?xml version=\"1.0\"?>\n<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 8 8\"><rect width=\"8\" height=\"8\" fill=\"#ff6a1a\"/></svg>";

    private sealed class Rig : IDisposable
    {
        public readonly TempDir Server = new();
        public readonly TestLog Log = new();
        public WebAdminHost Host = null!;
        public HttpClient Http = null!;

        public string DataDir => Path.Combine(Server.Path, "kiln", "data", Product.ModId);
        public string BrandDir => Path.Combine(DataDir, "branding");

        public Rig(Action<Rig>? before = null)
        {
            Directory.CreateDirectory(BrandDir);
            var s = new ServerSettings { Port = FreePort() };
            s.Tls.Mode = TlsMode.Off;
            new DataStore(DataDir).WriteJson("settings.json", s);
            before?.Invoke(this);
            Host = new WebAdminHost(new WebAdminEnvironment
            {
                DataDirectory = DataDir, ModDirectory = Path.Combine(Server.Path, "mod"), ServerDirectory = Server.Path, Log = Log,
            }, [new BrandingComponent()]);
            Host.Start();
            Host.Started.Wait(30_000);
            Http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{s.Port}/") };
        }

        public void Write(string name, byte[] data) => File.WriteAllBytes(Path.Combine(BrandDir, name), data);
        public void Settings(string json) => File.WriteAllText(Path.Combine(DataDir, "branding.json"), json);

        public async Task<JsonObject> BrandingAsync()
        {
            var r = await Http.GetAsync("api/core/branding");
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            return JsonNode.Parse(await r.Content.ReadAsStringAsync())!.AsObject();
        }

        public void Dispose()
        {
            Http?.Dispose();
            Host.Stop(500);
            Server.Dispose();
        }
    }

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    [Fact]
    public async Task Nothing_configured_gives_the_product_name_and_no_files()
    {
        using var rig = new Rig();
        var b = await rig.BrandingAsync();
        Assert.Equal(Product.Name, b["name"]!.GetValue<string>());
        Assert.Null(b["accent"]);
        Assert.Null(b["logo"]);
        Assert.Null(b["favicon"]);
        Assert.Equal(HttpStatusCode.NotFound, (await rig.Http.GetAsync("api/core/branding/logo")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await rig.Http.GetAsync("api/core/branding/favicon")).StatusCode);
    }

    [Fact]
    public async Task The_game_servers_name_is_the_fallback_and_branding_json_wins()
    {
        using var rig = new Rig(r => File.WriteAllText(Path.Combine(r.Server.Path, "enshrouded_server.json"), "{\"name\": \"Embervale\", \"queryPort\": 15637}"));
        Assert.Equal("Embervale", (await rig.BrandingAsync())["name"]!.GetValue<string>());
        rig.Settings("{\"name\": \"  The Hollow  \", \"accent\": \"#2A9DF4\"}");
        // the poll timer may get there first
        rig.Host.Services.Get<SettingsStore<BrandingSettings>>().Poll();
        await Eventually.True(() => rig.Host.Services.Get<SettingsStore<BrandingSettings>>().Current.Name is not null, 5000, "the reload");
        var b = await rig.BrandingAsync();
        Assert.Equal("The Hollow", b["name"]!.GetValue<string>());
        Assert.Equal("#2a9df4", b["accent"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#fff")]
    [InlineData("#12345g")]
    [InlineData("#1234567")]
    [InlineData("#123456 ")]
    [InlineData("#123456\n")]
    [InlineData("javascript:alert(1)")]
    [InlineData("#123456;background:url(x)")]
    [InlineData("")]
    public void Accent_is_strictly_hash_and_six_hex_digits(string accent)
    {
        Assert.False(BrandingRules.IsAccent(accent));
        Assert.NotEmpty(BrandingRules.Validate(new BrandingSettings { Accent = accent }));
    }

    [Theory]
    [InlineData("#000000")]
    [InlineData("#ff6A1a")]
    public void Good_accents_pass(string accent)
    {
        Assert.True(BrandingRules.IsAccent(accent));
        Assert.Empty(BrandingRules.Validate(new BrandingSettings { Accent = accent }));
    }

    [Fact]
    public async Task A_bad_accent_or_name_is_left_out_not_served()
    {
        using var rig = new Rig(r => r.Settings("{\"name\": \"bad\\u0000name\", \"accent\": \"url(javascript:x)\"}"));
        var b = await rig.BrandingAsync();
        Assert.Equal(Product.Name, b["name"]!.GetValue<string>());
        Assert.Null(b["accent"]);
        Assert.True(rig.Log.Has("branding.json"), rig.Log.ToString());
    }

    [Fact]
    public async Task Logo_and_favicon_by_their_fixed_names_with_strict_types()
    {
        using var rig = new Rig(r =>
        {
            r.Write("logo.png", Png);
            r.Write("favicon.ico", Ico);
        });
        var b = await rig.BrandingAsync();
        Assert.Equal("/api/core/branding/logo", b["logo"]!.GetValue<string>());
        Assert.Equal("/api/core/branding/favicon", b["favicon"]!.GetValue<string>());
        var logo = await rig.Http.GetAsync("api/core/branding/logo");
        Assert.Equal(HttpStatusCode.OK, logo.StatusCode);
        Assert.Equal("image/png", logo.Content.Headers.ContentType!.ToString());
        Assert.Equal(Png, await logo.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", logo.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.NotNull(logo.Headers.ETag);
        var fav = await rig.Http.GetAsync("api/core/branding/favicon");
        Assert.Equal("image/x-icon", fav.Content.Headers.ContentType!.ToString());
        // revalidation
        var again = new HttpRequestMessage(HttpMethod.Get, "api/core/branding/logo");
        again.Headers.IfNoneMatch.Add(logo.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NotModified, (await rig.Http.SendAsync(again)).StatusCode);
    }

    [Fact]
    public async Task Svg_is_not_served()
    {
        using var rig = new Rig(r => r.Write("logo.svg", Encoding.UTF8.GetBytes(Svg)));
        var r = await rig.Http.GetAsync("api/core/branding/logo");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task A_file_whose_bytes_do_not_match_its_type_is_not_served()
    {
        using var rig = new Rig(r =>
        {
            r.Write("logo.png", Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>"));
            r.Write("favicon.svg", Encoding.UTF8.GetBytes("<html><body>not an svg</body></html>"));
        });
        var b = await rig.BrandingAsync();
        Assert.Null(b["logo"]);
        Assert.Null(b["favicon"]);
        Assert.Equal(HttpStatusCode.NotFound, (await rig.Http.GetAsync("api/core/branding/logo")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await rig.Http.GetAsync("api/core/branding/favicon")).StatusCode);
    }

    [Fact]
    public async Task Files_over_the_cap_are_not_served()
    {
        var big = new byte[BrandingFiles.MaxBytes + 1];
        Png.CopyTo(big, 0);
        using var rig = new Rig(r => r.Write("logo.png", big));
        Assert.Null((await rig.BrandingAsync())["logo"]);
        Assert.Equal(HttpStatusCode.NotFound, (await rig.Http.GetAsync("api/core/branding/logo")).StatusCode);
        Assert.True(rig.Log.Has("logo.png"), rig.Log.ToString());
    }

    [Theory]
    [InlineData("../settings.json")]
    [InlineData("..\\settings.json")]
    [InlineData("sub/logo.png")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\x.png")]
    [InlineData("logo.html")]
    [InlineData("logo.png.html")]
    [InlineData(".hidden.png")]
    [InlineData("logo.png\u0000.svg")]
    [InlineData("logo")]
    public void A_named_file_is_a_plain_name_with_an_allowed_extension(string name)
    {
        Assert.False(BrandingRules.IsFileName(name));
        Assert.NotEmpty(BrandingRules.Validate(new BrandingSettings { Logo = name }));
        Assert.NotEmpty(BrandingRules.Validate(new BrandingSettings { Favicon = name }));
    }

    [Fact]
    public async Task Branding_json_may_choose_between_fixed_raster_names()
    {
        using var rig = new Rig(r =>
        {
            r.Write("logo.webp", Webp);
            r.Write("logo.png", Png);
            r.Settings("{\"logo\": \"logo.webp\"}");
        });
        var b = await rig.BrandingAsync();
        Assert.Equal("/api/core/branding/logo", b["logo"]!.GetValue<string>());
        Assert.Null(b["favicon"]);
        var logo = await rig.Http.GetAsync("api/core/branding/logo");
        Assert.Equal("image/webp", logo.Content.Headers.ContentType!.ToString());
        Assert.Equal(Webp, await logo.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task A_file_naming_a_path_is_refused_whole()
    {
        using var rig = new Rig(r =>
        {
            r.Write("crest.webp", Webp);
            File.WriteAllBytes(Path.Combine(r.DataDir, "secret.png"), Png);
            r.Settings("{\"logo\": \"logo.webp\", \"favicon\": \"../secret.png\"}");
        });
        var b = await rig.BrandingAsync();
        Assert.Null(b["logo"]);
        Assert.Null(b["favicon"]);
        Assert.Equal(HttpStatusCode.NotFound, (await rig.Http.GetAsync("api/core/branding/favicon")).StatusCode);
        Assert.True(rig.Log.Has("branding.json"), rig.Log.ToString());
    }

    [Fact]
    public async Task No_request_names_a_file()
    {
        using var rig = new Rig(r => r.Write("logo.png", Png));
        foreach (var path in new[] { "api/core/branding/logo.png", "api/core/branding/logo/x", "api/core/branding/../settings.json",
                     "api/core/branding/%2e%2e/settings.json", "api/core/branding/files/logo.png" })
            Assert.NotEqual(HttpStatusCode.OK, (await rig.Http.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task A_link_in_branding_is_refused()
    {
        if (OperatingSystem.IsWindows()) return;
        using var rig = new Rig(r =>
        {
            File.WriteAllBytes(Path.Combine(r.DataDir, "elsewhere.png"), Png);
            File.CreateSymbolicLink(Path.Combine(r.BrandDir, "logo.png"), Path.Combine(r.DataDir, "elsewhere.png"));
        });
        Assert.Null((await rig.BrandingAsync())["logo"]);
        Assert.Equal(HttpStatusCode.NotFound, (await rig.Http.GetAsync("api/core/branding/logo")).StatusCode);
    }

    [Theory]
    [InlineData("png", true)]
    [InlineData("ico", true)]
    [InlineData("webp", true)]
    [InlineData("svg", false)]
    public void Sniff_accepts_each_kind(string ext, bool ok)
    {
        var bytes = ext switch { "png" => Png, "ico" => Ico, "webp" => Webp, _ => Encoding.UTF8.GetBytes(Svg) };
        Assert.Equal(ok, BrandingFiles.Matches(ext, bytes));
        foreach (var other in new[] { "png", "ico", "webp", "svg" }.Where(o => o != ext))
            Assert.False(BrandingFiles.Matches(other, bytes), $"{ext} bytes read as {other}");
    }

    [Theory]
    [InlineData("<svg><script>alert(1)</script></svg>")]
    [InlineData("<svg onload=\"alert(1)\"/>")]
    [InlineData("<!DOCTYPE html><svg/>")]
    [InlineData("<html><svg/></html>")]
    [InlineData("")]
    public void Svg_sniff_refuses_what_is_not_a_plain_svg_document(string text) =>
        Assert.False(BrandingFiles.Matches("svg", Encoding.UTF8.GetBytes(text)));
}
