// SPDX-License-Identifier: MIT
using Kilnwarden.Offline.Support;
using Kilnwarden.Storage;

namespace Kilnwarden.Offline.Storage;

public class DataStoreTests
{
    private sealed class Doc
    {
        public string Name { get; set; } = "";
        public int N { get; set; }
    }

    [Theory]
    [InlineData("../x.json")]
    [InlineData("a/../../x.json")]
    [InlineData("/etc/passwd")]
    [InlineData("")]
    public void Paths_stay_inside(string rel)
    {
        using var dir = new TempDir();
        Assert.Throws<ArgumentException>(() => new DataStore(dir.Path).PathOf(rel));
    }

    [Fact]
    public void Json_round_trip_and_missing_is_null()
    {
        using var dir = new TempDir();
        var d = new DataStore(dir.Path);
        Assert.Null(d.ReadJson<Doc>("a/b.json"));
        d.WriteJson("a/b.json", new Doc { Name = "x", N = 3 });
        var back = d.ReadJson<Doc>("a/b.json")!;
        Assert.Equal(("x", 3), (back.Name, back.N));
    }

    [Fact]
    public void Secret_files_are_0600_on_unix()
    {
        if (OperatingSystem.IsWindows()) return;
        using var dir = new TempDir();
        var d = new DataStore(dir.Path);
        d.WriteBytes("k.pem", "secret"u8, secret: true);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(d.PathOf("k.pem")));
    }

    [Fact]
    public void Jsonl_appends_rotates_and_reads_back()
    {
        using var dir = new TempDir();
        using var w = new DataStore(dir.Path).OpenJsonl("audit/audit.jsonl", rotateBytes: 4096, keep: 2);
        for (var i = 0; i < 400; i++) w.Append(new { i, pad = new string('x', 40) });
        var files = w.Files();
        Assert.Equal(3, files.Count);
        Assert.EndsWith("audit.jsonl", files[0]);
        Assert.EndsWith("audit.1.jsonl", files[1]);
        var last = JsonlWriter.ReadLines(files[0]).Last();
        Assert.Equal(399, last.GetProperty("i").GetInt32());
        w.Dispose(); // Windows: no second writer while the log holds the file open
        File.AppendAllText(files[0], "{torn");
        Assert.Equal(399, JsonlWriter.ReadLines(files[0]).Last().GetProperty("i").GetInt32());
    }

    [Fact]
    public async Task Jsonl_is_safe_from_many_threads()
    {
        using var dir = new TempDir();
        using var w = new DataStore(dir.Path).OpenJsonl("e.jsonl");
        await Task.WhenAll(Enumerable.Range(0, 8).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < 200; i++) w.Append(new { t, i });
        })));
        Assert.Equal(1600, JsonlWriter.ReadLines(w.FilePath).Count());
    }
}
