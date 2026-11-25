// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using Kilnwarden.Ops;

namespace Kilnwarden.Offline.Ops;

public class PackageTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("kwa-pkg-").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    private string Zip(byte[] bytes)
    {
        var p = Path.Combine(_dir, Guid.NewGuid().ToString("n") + ".zip");
        File.WriteAllBytes(p, bytes);
        return p;
    }

    [Fact]
    public void A_mod_in_a_top_folder_is_read_and_unpacked_with_hashes()
    {
        var zip = Zip(Zips.Make(("cool-mod/kiln.mod.json", Zips.Mod()), ("cool-mod/Cool.dll", "MZ fake"), ("cool-mod/assets/a.txt", "a"), ("cool-mod/assets/", "")));
        var info = PackageArchive.Inspect(zip, ArchiveLimits.Default);
        Assert.Equal(("mod", "cool-mod", "1.2.0", "cool-mod/"), (info.Kind, info.Id, info.Version, info.Prefix));
        Assert.Equal(["Cool.dll", "assets/a.txt", "kiln.mod.json"], info.Files.Select(f => f.Path));
        var target = Path.Combine(_dir, "out");
        var files = PackageArchive.Extract(zip, target, ArchiveLimits.Default);
        Assert.Equal(3, files.Count);
        var a = files.Single(f => f.Path == "assets/a.txt");
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData("a"u8)), a.Sha256);
        Assert.Equal("a", File.ReadAllText(Path.Combine(target, "assets", "a.txt")));
        Assert.Throws<ArchiveRefusedException>(() => PackageArchive.Extract(zip, target, ArchiveLimits.Default)); // never into an existing folder
    }

    [Fact]
    public void A_kiln_bundle_is_recognised()
    {
        var info = PackageArchive.Inspect(Zip(Zips.Make(("kiln-runtime.json", "{\"version\": \"0.2.0-dev.30.10\"}"), ("dinput8.dll", "x"))), ArchiveLimits.Default);
        Assert.Equal(("kiln", "0.2.0-dev.30.10"), (info.Kind, info.Version));
    }

    [Theory]
    [InlineData("../evil.dll")]
    [InlineData("/abs.dll")]
    [InlineData("a\\b.dll")]
    [InlineData("C:x.dll")]
    [InlineData("sub/CON.txt")]
    [InlineData("sub/trail.")]
    [InlineData("sub/PROGRA~1/x")]
    [InlineData("sub/a:b")]
    [InlineData("sub//x")]
    public void Bad_names_are_refused(string name)
    {
        var zip = Zip(Zips.Make(("kiln.mod.json", Zips.Mod()), (name, "x")));
        Assert.Throws<ArchiveRefusedException>(() => PackageArchive.Inspect(zip, ArchiveLimits.Default));
        Assert.Throws<ArchiveRefusedException>(() => PackageArchive.Extract(zip, Path.Combine(_dir, "o"), ArchiveLimits.Default));
        Assert.False(File.Exists(Path.Combine(_dir, "evil.dll")));
    }

    [Fact]
    public void Links_case_twins_file_folder_clashes_and_missing_manifests_are_refused()
    {
        var link = Zips.Make(("kiln.mod.json", Encoding.UTF8.GetBytes(Zips.Mod()), 0), ("link", Encoding.UTF8.GetBytes("/etc/passwd"), 0xA1FF));
        Assert.Contains("link", Assert.Throws<ArchiveRefusedException>(() => PackageArchive.Inspect(Zip(link), ArchiveLimits.Default)).Message);
        var twins = Zips.Make(("kiln.mod.json", Zips.Mod()), ("A.dll", "1"), ("a.DLL", "2"));
        Assert.Throws<ArchiveRefusedException>(() => PackageArchive.Inspect(Zip(twins), ArchiveLimits.Default));
        var clash = Zips.Make(("kiln.mod.json", Zips.Mod()), ("x", "1"), ("x/y", "2"));
        Assert.Throws<ArchiveRefusedException>(() => PackageArchive.Inspect(Zip(clash), ArchiveLimits.Default));
        Assert.Throws<ArchiveRefusedException>(() => PackageArchive.Inspect(Zip(Zips.Make(("readme.txt", "hi"))), ArchiveLimits.Default));
        Assert.Throws<ArchiveRefusedException>(() => PackageArchive.Inspect(Zip(Zips.Make(("kiln.mod.json", "{\"id\": \"../x\", \"version\": \"1\"}"))), ArchiveLimits.Default));
        Assert.Throws<ArchiveRefusedException>(() => PackageArchive.Inspect(Zip(Encoding.UTF8.GetBytes("not a zip")), ArchiveLimits.Default));
    }

    [Fact]
    public void Bombs_and_limits_are_refused_on_counted_bytes()
    {
        var bomb = Zips.Make(("kiln.mod.json", Encoding.UTF8.GetBytes(Zips.Mod()), 0), ("zeros.bin", new byte[8 << 20], 0));
        Assert.Contains("compressed more than", Assert.Throws<ArchiveRefusedException>(() => PackageArchive.Inspect(Zip(bomb), ArchiveLimits.Default)).Message);
        var many = Zips.Make([("kiln.mod.json", Zips.Mod()), .. Enumerable.Range(0, 20).Select(i => ($"f{i}", "x"))]);
        Assert.Throws<ArchiveRefusedException>(() => PackageArchive.Inspect(Zip(many), new ArchiveLimits(10, 1 << 20, 1 << 20)));
        var big = Zips.Make(("kiln.mod.json", Zips.Mod()), ("big", new string('x', 5000)));
        Assert.Throws<ArchiveRefusedException>(() => PackageArchive.Extract(Zip(big), Path.Combine(_dir, "b"), new ArchiveLimits(100, 4000, 4000)));
    }

    [Fact]
    public void Url_policy()
    {
        string[] allowed = ["https://github.com", "https://dl.example.org:8443"];
        Assert.Null(UrlPolicy.Problem(new Uri("https://github.com/a/b/releases/download/v1/x.zip"), allowed));
        Assert.Null(UrlPolicy.Problem(new Uri("https://dl.example.org:8443/x.zip"), allowed));
        Assert.NotNull(UrlPolicy.Problem(new Uri("http://github.com/x.zip"), allowed));
        Assert.NotNull(UrlPolicy.Problem(new Uri("https://user:pw@github.com/x.zip"), allowed));
        Assert.NotNull(UrlPolicy.Problem(new Uri("https://github.com.evil.example/x.zip"), allowed));
        Assert.NotNull(UrlPolicy.Problem(new Uri("https://dl.example.org/x.zip"), allowed)); // port 443, not the allowed 8443
        Assert.Null(UrlPolicy.ParseOrigin("https://github.com/path"));
        Assert.Null(UrlPolicy.ParseOrigin("http://github.com"));
        foreach (var ip in new[] { "127.0.0.1", "10.1.2.3", "172.16.0.1", "192.168.1.1", "169.254.169.254", "100.64.0.1", "0.0.0.0", "::1", "fe80::1", "fd00::1", "::ffff:10.0.0.1", "64:ff9b::a00:1", "2002:a00:1::1", "224.0.0.1" })
            Assert.False(UrlPolicy.IsPublic(IPAddress.Parse(ip)), ip);
        foreach (var ip in new[] { "140.82.112.3", "8.8.8.8", "2606:4700::1111" })
            Assert.True(UrlPolicy.IsPublic(IPAddress.Parse(ip)), ip);
    }

    private sealed class Fake(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public readonly List<Uri> Asked = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Asked.Add(request.RequestUri!);
            return Task.FromResult(answer(request));
        }
    }

    private sealed class Unseekable(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }

    private static HttpResponseMessage Redirect(string to) => new(HttpStatusCode.Found) { Headers = { Location = new Uri(to, UriKind.RelativeOrAbsolute) } };

    [Fact]
    public async Task The_downloader_checks_every_redirect_and_counts_bytes()
    {
        string[] allowed = ["https://github.com", "https://objects.githubusercontent.com"];
        var file = () => Path.Combine(_dir, Guid.NewGuid().ToString("n"));
        // allowed redirect, then the bytes
        var ok = new Fake(r => r.RequestUri!.Host == "github.com" ? Redirect("https://objects.githubusercontent.com/x?token=secret")
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
        using (var d = new HttpPackageDownloader(ok))
        {
            var f = file();
            var final = await d.DownloadAsync(new Uri("https://github.com/a.zip"), f, 100, allowed, default);
            Assert.Equal("objects.githubusercontent.com", final.Host);
            Assert.Equal(3, new FileInfo(f).Length);
        }
        // a redirect to an origin that is not allowed (or to plain http) is never requested
        foreach (var to in new[] { "https://evil.example/x", "http://objects.githubusercontent.com/x", "https://169.254.169.254/latest" })
        {
            var bad = new Fake(r => r.RequestUri!.Host == "github.com" ? Redirect(to) : new HttpResponseMessage(HttpStatusCode.OK));
            using var d = new HttpPackageDownloader(bad);
            await Assert.ThrowsAsync<DownloadRefusedException>(() => d.DownloadAsync(new Uri("https://github.com/a.zip"), file(), 100, allowed, default));
            Assert.Single(bad.Asked);
        }
        // a loop
        var loop = new Fake(r => Redirect("https://github.com/again"));
        using (var d = new HttpPackageDownloader(loop))
            await Assert.ThrowsAsync<DownloadRefusedException>(() => d.DownloadAsync(new Uri("https://github.com/a.zip"), file(), 100, allowed, default));
        Assert.Equal(HttpPackageDownloader.MaxRedirects + 1, loop.Asked.Count);
        // too large: declared, and streamed without a length
        var declared = new Fake(r => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[200]) });
        using (var d = new HttpPackageDownloader(declared))
            await Assert.ThrowsAsync<DownloadRefusedException>(() => d.DownloadAsync(new Uri("https://github.com/a.zip"), file(), 100, allowed, default));
        var streamed = new Fake(r => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new Unseekable(new byte[200])) });
        using (var d = new HttpPackageDownloader(streamed))
            await Assert.ThrowsAsync<DownloadRefusedException>(() => d.DownloadAsync(new Uri("https://github.com/a.zip"), file(), 100, allowed, default));
    }

    /// <summary>A zip whose central directory holds <paramref name="records"/> entries for one tiny file (each record
    /// points at the same local header): small on disk, many records to parse.</summary>
    private static byte[] ManyRecords(int records, bool lieInEocd = false)
    {
        var name = "f"u8.ToArray();
        using var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        // one local file header + 1 stored byte
        w.Write(0x04034b50); w.Write((ushort)20); w.Write((ushort)0); w.Write((ushort)0); w.Write(0u);
        const uint crc = 0x8cdc1683; // CRC-32 of "x"
        w.Write(crc); w.Write(1u); w.Write(1u); w.Write((ushort)name.Length); w.Write((ushort)0); w.Write(name); w.Write((byte)'x');
        var cdStart = ms.Position;
        for (var i = 0; i < records; i++)
        {
            w.Write(0x02014b50); w.Write((ushort)20); w.Write((ushort)20); w.Write((ushort)0); w.Write((ushort)0); w.Write(0u);
            w.Write(crc); w.Write(1u); w.Write(1u); w.Write((ushort)name.Length); w.Write((ushort)0); w.Write((ushort)0);
            w.Write((ushort)0); w.Write((ushort)0); w.Write(0u); w.Write(0u); w.Write(name);
        }
        var cdSize = ms.Position - cdStart;
        var count = lieInEocd ? (ushort)1 : (ushort)Math.Min(records, 0xFFFF);
        w.Write(0x06054b50); w.Write((ushort)0); w.Write((ushort)0); w.Write(count); w.Write(count); w.Write((uint)cdSize); w.Write((uint)cdStart); w.Write((ushort)0);
        return ms.ToArray();
    }

    /// <summary>Security review S4: the central directory's records are counted before ZipArchive turns each into an
    /// object, so a package with millions of tiny records is refused without allocating for them (whatever its EOCD says).</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Too_many_central_directory_records_are_refused_before_they_are_loaded(bool lie)
    {
        var zip = Zip(ManyRecords(60_000, lie));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var e = Assert.Throws<ArchiveRefusedException>(() => PackageArchive.Inspect(zip, ArchiveLimits.Default));
        var used = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Contains("entries", e.Message);
        Assert.True(used < 2_000_000, $"{used} bytes allocated");
        Assert.Throws<ArchiveRefusedException>(() => PackageArchive.Extract(zip, Path.Combine(Path.GetTempPath(), "kwa-never-" + Guid.NewGuid().ToString("N")), ArchiveLimits.Default));
    }
}
