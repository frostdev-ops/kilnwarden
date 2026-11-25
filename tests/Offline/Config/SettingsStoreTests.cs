// SPDX-License-Identifier: MIT
using Kilnwarden.Config;
using Kilnwarden.Offline.Support;
using Kilnwarden.Storage;

namespace Kilnwarden.Offline.Config;

public class SettingsStoreTests
{
    [Fact]
    public void Writes_defaults_when_missing_and_they_are_the_safe_ones()
    {
        using var dir = new TempDir();
        var s = new SettingsStore<ServerSettings>(new DataStore(dir.Path), "settings.json", new TestLog(), SettingsRules.Validate);
        Assert.True(File.Exists(dir.File("settings.json")));
        Assert.Equal("127.0.0.1", s.Current.Bind);
        Assert.Equal(TlsMode.SelfSigned, s.Current.Tls.Mode);
        Assert.False(s.Current.ReverseProxy.Enabled);
        Assert.Contains("\"self-signed\"", File.ReadAllText(dir.File("settings.json")));
    }

    [Fact]
    public void Hot_reload_takes_valid_changes_and_keeps_the_last_good_on_bad_ones()
    {
        using var dir = new TempDir();
        var log = new TestLog();
        var s = new SettingsStore<ServerSettings>(new DataStore(dir.Path), "settings.json", log, SettingsRules.Validate);
        ServerSettings? seen = null;
        s.Changed += (_, n) => seen = n;

        File.WriteAllText(dir.File("settings.json"), "{ // hand edit\n \"bind\": \"0.0.0.0\", \"port\": 15800, \"tls\": {\"mode\": \"off\"}, }");
        Assert.True(s.Poll());
        Assert.Equal("0.0.0.0", seen!.Bind);
        Assert.Equal(TlsMode.Off, s.Current.Tls.Mode);
        Assert.False(s.Poll()); // unchanged file

        File.WriteAllText(dir.File("settings.json"), "{ \"bind\": \"not-an-ip\" }");
        Assert.False(s.Poll());
        Assert.Equal("0.0.0.0", s.Current.Bind);
        Assert.NotEmpty(s.Problems);
        Assert.True(log.Has("keeping the last good settings"));

        File.WriteAllText(dir.File("settings.json"), "{ broken");
        Assert.False(s.Poll());
        Assert.Equal(15800, s.Current.Port);
    }

    [Fact]
    public void Save_validates_and_writes_atomically()
    {
        using var dir = new TempDir();
        var s = new SettingsStore<ServerSettings>(new DataStore(dir.Path), "settings.json", new TestLog(), SettingsRules.Validate);
        var next = new ServerSettings { Port = 99999 };
        Assert.Throws<ArgumentException>(() => s.Save(next));
        next.Port = 16000;
        s.Save(next);
        next.Port = 1; // the store keeps its own copy
        Assert.Equal(16000, s.Current.Port);
        Assert.False(s.Poll());
        Assert.Empty(Directory.GetFiles(dir.Path, "*.tmp"));
    }

    [Fact]
    public void Unreadable_file_at_start_uses_defaults_without_overwriting()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("settings.json"), "{ nope");
        var log = new TestLog();
        var s = new SettingsStore<ServerSettings>(new DataStore(dir.Path), "settings.json", log, SettingsRules.Validate);
        Assert.Equal("127.0.0.1", s.Current.Bind);
        Assert.Equal("{ nope", File.ReadAllText(dir.File("settings.json")));
        Assert.True(log.Has("unreadable"));
    }

    [Theory]
    [InlineData("10.0.0.0/8", "10.200.1.1", true)]
    [InlineData("10.0.0.0/8", "11.0.0.1", false)]
    [InlineData("192.168.1.7", "192.168.1.7", true)]
    [InlineData("192.168.1.0/24", "::ffff:192.168.1.9", true)]
    [InlineData("fd00::/8", "fd12::1", true)]
    [InlineData("fd00::/8", "10.0.0.1", false)]
    [InlineData("0.0.0.0/0", "8.8.8.8", true)]
    public void Ip_ranges(string range, string addr, bool inside)
    {
        Assert.True(IpRange.TryParse(range, out var r));
        Assert.Equal(inside, r.Contains(System.Net.IPAddress.Parse(addr)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("10.0.0.0/33")]
    [InlineData("10.0.0.0/-1")]
    [InlineData("10.0.0.0/+8")]
    [InlineData("host.example")]
    [InlineData("fe80::1%eth0")]
    public void Bad_ranges(string s) => Assert.False(IpRange.TryParse(s, out _));
}

public class SettingsNullTests
{
    [Theory]
    [InlineData("{\"tls\": null}")]
    [InlineData("{\"limits\": null}")]
    [InlineData("{\"ipAllowlist\": null}")]
    [InlineData("{\"reverseProxy\": {\"trustedProxies\": null}}")]
    [InlineData("{\"tls\": {\"hostnames\": [null]}}")]
    [InlineData("{\"bind\": null}")]
    [InlineData("{\"allowedHosts\": [null]}")]
    public void Nulled_parts_are_refused_not_crashes(string json)
    {
        using var dir = new Support.TempDir();
        var log = new Support.TestLog();
        var s = new SettingsStore<ServerSettings>(new Kilnwarden.Storage.DataStore(dir.Path), "settings.json", log, SettingsRules.Validate);
        File.WriteAllText(dir.File("settings.json"), json);
        Assert.False(s.Poll());
        Assert.NotEmpty(s.Problems);
        Assert.Equal("127.0.0.1", s.Current.Bind);
    }
}
