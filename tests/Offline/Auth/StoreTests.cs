// SPDX-License-Identifier: MIT
using System.Net;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Kap;
using Kilnwarden.Modules;
using Kilnwarden.Offline.Support;
using Kilnwarden.Storage;
using Kilnwarden.Offline.Support;

namespace Kilnwarden.Offline.Auth;

public class AccountStoreTests
{
    private static Account Acct(string name, params string[] roles) => new() { Name = name, PasswordHash = "", Roles = roles };

    [Fact]
    public void Accounts_persist_and_the_owner_flips_the_setup_state()
    {
        using var tmp = new TempDir();
        var data = new DataStore(tmp.Path);
        var s = new AccountStore(data);
        var flips = 0;
        s.Changed += () => flips++;
        Assert.False(s.OwnerExists);
        var v = s.Add(Acct("viewer1", "viewer"));
        Assert.Matches("^u-[0-9a-f]{16}$", v.Id);
        Assert.Equal(0, flips);
        var o = s.Add(Acct("Owner", "owner"));
        Assert.True(s.OwnerExists);
        Assert.Equal(1, flips);
        var again = new AccountStore(data);
        Assert.Equal(2, again.All.Count);
        Assert.Equal(o.Id, again.FindByName("OWNER")!.Id);
        Assert.Equal(o.Id, again.PrimaryOwner!.Id);
    }

    [Fact]
    public void Names_are_unique_ignoring_case_and_shaped()
    {
        using var tmp = new TempDir();
        var s = new AccountStore(new DataStore(tmp.Path));
        s.Add(Acct("alice", "viewer"));
        Assert.Throws<InvalidOperationException>(() => s.Add(Acct("ALICE", "viewer")));
        Assert.Throws<ArgumentException>(() => s.Add(Acct("a", "viewer")));
        Assert.Throws<ArgumentException>(() => s.Add(Acct("bad name", "viewer")));
        Assert.Throws<ArgumentException>(() => s.Add(Acct("bob", "Bad Role")));
        Assert.Null(s.FindByName("../alice"));
    }

    [Fact]
    public void The_last_enabled_owner_cannot_go()
    {
        using var tmp = new TempDir();
        var s = new AccountStore(new DataStore(tmp.Path));
        var o = s.Add(Acct("owner1", "owner"));
        Assert.Throws<InvalidOperationException>(() => s.Remove(o.Id));
        Assert.Throws<InvalidOperationException>(() => s.Update(o.Id, a => a with { Disabled = true }));
        Assert.Throws<InvalidOperationException>(() => s.Update(o.Id, a => a with { Roles = ["admin"] }));
        var o2 = s.Add(Acct("owner2", "owner"));
        s.Update(o.Id, a => a with { Roles = ["admin"] });
        Assert.Throws<InvalidOperationException>(() => s.Remove(o2.Id));
    }

    [Fact]
    public void An_unreadable_accounts_file_stops_the_start()
    {
        using var tmp = new TempDir();
        File.WriteAllText(tmp.File("accounts.json"), "{ not json");
        Assert.Throws<InvalidDataException>(() => new AccountStore(new DataStore(tmp.Path)));
        File.WriteAllText(tmp.File("accounts.json"), "{\"format\":1,\"accounts\":[{\"id\":\"u-0000000000000001\",\"name\":\"x\"}]}");
        Assert.Throws<InvalidDataException>(() => new AccountStore(new DataStore(tmp.Path)));
        File.WriteAllText(tmp.File("accounts.json"), "{\"format\":1,\"accounts\":[{\"id\":null,\"name\":null,\"passwordHash\":null}]}");
        Assert.Throws<InvalidDataException>(() => new AccountStore(new DataStore(tmp.Path)));
        File.WriteAllText(tmp.File("accounts.json"), "{\"format\":2,\"accounts\":[]}");
        Assert.Throws<InvalidDataException>(() => new AccountStore(new DataStore(tmp.Path)));
    }
}

public class RoleTests
{
    private static ModuleRegistry Registry()
    {
        var r = new ModuleRegistry();
        r.Register(new ModuleSpec
        {
            Id = "spawn", Title = "Spawn",
            Permissions =
            [
                new PermissionSpec("spawn.view", "v") { DefaultRole = BuiltinRole.Viewer },
                new PermissionSpec("spawn.spawn", "s") { DefaultRole = BuiltinRole.Moderator },
                new PermissionSpec("spawn.mass", "m") { DefaultRole = BuiltinRole.Admin },
                new PermissionSpec("spawn.owner", "o") { DefaultRole = BuiltinRole.Owner },
            ],
        }, "t");
        return r;
    }

    [Fact]
    public void Builtins_have_their_rank_plus_grants_minus_revokes()
    {
        var reg = Registry();
        var mod = RoleStore.Default("moderator");
        Assert.True(WebPrincipal.Grants(mod, "spawn.view", reg));
        Assert.True(WebPrincipal.Grants(mod, "spawn.spawn", reg));
        Assert.False(WebPrincipal.Grants(mod, "spawn.mass", reg));
        Assert.True(WebPrincipal.Grants(mod with { Grants = ["spawn.mass"] }, "spawn.mass", reg));
        Assert.False(WebPrincipal.Grants(mod with { Revokes = ["spawn.*"] }, "spawn.view", reg));
        Assert.False(WebPrincipal.Grants(RoleStore.Default("admin"), "spawn.owner", reg));
        Assert.True(WebPrincipal.Grants(RoleStore.Default("owner"), "spawn.owner", reg));
        Assert.True(WebPrincipal.Grants(RoleStore.Default("owner"), "anything.at_all", reg));
        // an undeclared name: only explicit grants
        Assert.False(WebPrincipal.Grants(RoleStore.Default("admin"), "ghost.x", reg));
    }

    [Fact]
    public void Custom_roles_have_exactly_their_grants_with_wildcards()
    {
        var reg = Registry();
        var r = new RoleDef { Id = "spawner", Name = "Spawner", Grants = ["spawn.*"] };
        Assert.Null(RoleStore.Validate(r));
        Assert.True(WebPrincipal.Grants(r, "spawn.owner", reg));
        Assert.False(WebPrincipal.Grants(r, "spawnx.y", reg));
        Assert.False(WebPrincipal.Grants(new RoleDef { Id = "empty", Name = "E" }, "spawn.view", reg));
        Assert.True(PermissionPattern.Matches("spawn.*", "spawn.a.b"));
        Assert.False(PermissionPattern.Matches("spawn.*", "spawn"));
    }

    [Theory]
    [InlineData("moderator", CommandLevel.Admin)]
    [InlineData("viewer", CommandLevel.Admin)]
    [InlineData("admin", CommandLevel.Console)]
    [InlineData("helpers", CommandLevel.Console)]
    public void Command_levels_have_ceilings(string id, CommandLevel level) =>
        Assert.NotNull(RoleStore.Validate((RoleStore.Default(id) with { Name = "x" }) with { CommandLevel = level }));

    [Fact]
    public void Role_rules()
    {
        Assert.NotNull(RoleStore.Validate(new RoleDef { Id = "x", Name = "X", Grants = ["*"] }));                // no "*" grants
        Assert.NotNull(RoleStore.Validate(new RoleDef { Id = "x", Name = "X", Revokes = ["a.b"] }));             // custom: no revokes
        Assert.NotNull(RoleStore.Validate(new RoleDef { Id = "X", Name = "X" }));
        Assert.NotNull(RoleStore.Validate(new RoleDef { Id = "x", Name = "" }));
        Assert.NotNull(RoleStore.Validate(new RoleDef { Id = "x", Name = "X", Grants = ["Bad.Name"] }));
        Assert.NotNull(RoleStore.Validate(new RoleDef { Id = "x", Name = "X", CommandDeny = ["bad pattern"] }));
        Assert.NotNull(RoleStore.Validate(RoleStore.Default("owner") with { Grants = ["a.b"] }));
        Assert.NotNull(RoleStore.Validate(RoleStore.Default("owner") with { CommandLevel = CommandLevel.Admin }));
        Assert.Null(RoleStore.Validate(RoleStore.Default("owner") with { Require2fa = true }));
        Assert.Null(RoleStore.Validate(new RoleDef { Id = "helpers", Name = "Helpers", CommandLevel = CommandLevel.Admin, CommandOnly = ["kick", "claims.command.*"] }));
    }

    [Fact]
    public void Roles_persist_with_builtins_always_present()
    {
        using var tmp = new TempDir();
        var data = new DataStore(tmp.Path);
        var s = new RoleStore(data);
        Assert.Equal(["owner", "admin", "moderator", "viewer"], s.All.Select(r => r.Id));
        s.Put(new RoleDef { Id = "builder", Name = "Builder", Grants = ["build.*"], CommandLevel = CommandLevel.Everyone });
        s.Put(RoleStore.Default("moderator") with { Require2fa = true });
        var again = new RoleStore(data);
        Assert.Equal(5, again.All.Count);
        Assert.True(again.Find("moderator")!.Require2fa);
        Assert.Equal(CommandLevel.Everyone, again.Find("builder")!.CommandLevel);
        Assert.Throws<ArgumentException>(() => s.Remove("admin"));
        Assert.True(s.Remove("builder"));
        File.WriteAllText(tmp.File("roles.json"), "{\"format\":1,\"roles\":[{\"id\":\"moderator\",\"name\":\"M\",\"commandLevel\":\"console\"}]}");
        Assert.Throws<InvalidDataException>(() => new RoleStore(data));
    }

    [Fact]
    public void Command_patterns_match_names_aliases_and_nodes()
    {
        Assert.True(CommandPattern.Matches("kick", "Kick", [], "kiln.command.kick"));
        Assert.True(CommandPattern.Matches("k", "kick", ["k"], null));
        Assert.True(CommandPattern.Matches("claims.command.*", "claim", [], "claims.command.claim"));
        Assert.True(CommandPattern.Matches("claims.command.claim", "claim", [], "claims.command.claim"));
        Assert.False(CommandPattern.Matches("claims.*", "claim", [], null));
        Assert.False(CommandPattern.Matches("kick", "kickall", [], "kiln.command.kickall"));
    }
}

public class SessionStoreTests
{
    private static Account A(string id = "u-0000000000000001") => new() { Id = id, Name = "alice" };

    [Fact]
    public void Tokens_find_sessions_and_only_their_hash_is_kept()
    {
        using var tmp = new TempDir();
        var data = new DataStore(tmp.Path);
        var settings = new AuthSettings();
        using (var s = new SessionStore(data, () => settings, new TestLog()))
        {
            var (token, rec) = s.Create(A(), "password", false, "1.2.3.4", "Mozilla/5.0\r\nX: y");
            Assert.Same(rec, s.Find(token));
            Assert.Null(s.Find(token[..42] + (token[42] == 'A' ? 'B' : 'A')));
            Assert.Null(s.Find("garbage"));
            Assert.Equal("Mozilla/5.0X: y", rec.UserAgent);
            s.Flush();
            var file = File.ReadAllText(tmp.File("sessions.json"));
            Assert.DoesNotContain(token, file);
            Assert.Contains(rec.Hash, file);
        }
        using var again = new SessionStore(data, () => settings, new TestLog());
        Assert.Equal(1, again.Count);
    }

    [Fact]
    public void Idle_and_absolute_expiry()
    {
        using var tmp = new TempDir();
        var time = new ManualTime(DateTimeOffset.Parse("2026-11-23T12:00:00Z"));
        var settings = new AuthSettings { SessionIdleMinutes = 30, SessionAbsoluteHours = 2 };
        using var s = new SessionStore(new DataStore(tmp.Path), () => settings, new TestLog(), time);
        var (token, rec) = s.Create(A(), "password", false, "", "");
        time.Advance(TimeSpan.FromMinutes(29));
        Assert.NotNull(s.Find(token));
        rec = s.Touch(s.Find(token)!);
        time.Advance(TimeSpan.FromMinutes(29));
        Assert.NotNull(s.Find(token));
        time.Advance(TimeSpan.FromMinutes(31));
        Assert.Null(s.Find(token));                // idle
        Assert.Equal(0, s.Count);
        var (t2, r2) = s.Create(A(), "password", false, "", "");
        for (var i = 0; i < 4; i++)
        {
            time.Advance(TimeSpan.FromMinutes(25));
            s.Touch(s.Find(t2)!);                  // in use: never idle
        }
        time.Advance(TimeSpan.FromMinutes(25));
        Assert.Null(s.Find(t2));                   // absolute (2 h)
    }

    [Fact]
    public void Rotation_kills_the_old_token_and_revokes_work_by_handle()
    {
        using var tmp = new TempDir();
        var settings = new AuthSettings { MaxSessionsPerAccount = 2 };
        using var s = new SessionStore(new DataStore(tmp.Path), () => settings, new TestLog());
        var a = A();
        var (t1, r1) = s.Create(a, "password", false, "", "");
        var (t1b, r1b) = s.Rotate(r1, a with { PrivilegeStamp = 5 });
        Assert.Null(s.Find(t1));
        Assert.Equal(r1.Handle, s.Find(t1b)!.Handle);
        Assert.Equal(5, s.Find(t1b)!.PrivilegeStamp);
        var (t2, _) = s.Create(a, "password", false, "", "");
        var (t3, _) = s.Create(a, "password", false, "", "");   // over the cap of 2: the oldest goes
        Assert.Null(s.Find(t1b));
        Assert.NotNull(s.Find(t2));
        Assert.True(s.Revoke(s.Find(t2)!.Handle));
        Assert.Null(s.Find(t2));
        Assert.Equal(1, s.RevokeAccount(a.Id));
        Assert.Null(s.Find(t3));
        Assert.Null(s.FindByHandle(r1b.Handle));
    }
}

public class SignInLimiterTests
{
    private static readonly IPAddress Ip = IPAddress.Parse("203.0.113.9");

    [Fact]
    public void Attempts_per_address_are_a_bucket()
    {
        var time = new ManualTime(DateTimeOffset.UnixEpoch);
        var l = new SignInLimiter(() => new SignInLimitSettings { AttemptsPerMinutePerIp = 3 }, time);
        for (var i = 0; i < 3; i++) Assert.Null(l.Attempt(Ip, null));
        Assert.NotNull(l.Attempt(Ip, null));
        Assert.Null(l.Attempt(IPAddress.Parse("203.0.113.10"), null));
        time.Advance(TimeSpan.FromSeconds(20));
        Assert.Null(l.Attempt(Ip, null));
    }

    [Fact]
    public void Failures_lock_names_with_doubling_backoff_known_or_not()
    {
        var time = new ManualTime(DateTimeOffset.UnixEpoch);
        var l = new SignInLimiter(() => new SignInLimitSettings { AttemptsPerMinutePerIp = 600, AccountFreeFailures = 3, IpFreeFailures = 100, LockoutBaseSeconds = 10, LockoutMaxSeconds = 40 }, time);
        for (var i = 0; i < 2; i++) l.Fail(Ip, "name:nobody");
        Assert.Null(l.Attempt(Ip, "name:nobody"));
        l.Fail(Ip, "name:nobody");                                             // the 3rd failure locks
        Assert.Equal(TimeSpan.FromSeconds(10), l.Attempt(Ip, "name:nobody"));
        Assert.Null(l.Attempt(IPAddress.Parse("198.51.100.1"), "name:other"));
        time.Advance(TimeSpan.FromSeconds(11));
        l.Fail(Ip, "name:nobody");
        Assert.Equal(TimeSpan.FromSeconds(20), l.Attempt(Ip, "name:nobody"));
        time.Advance(TimeSpan.FromSeconds(21));
        l.Fail(Ip, "name:nobody");
        l.Fail(Ip, "name:nobody");
        Assert.Equal(TimeSpan.FromSeconds(40), l.Attempt(Ip, "name:nobody")); // capped
        l.Succeed("name:nobody");
        Assert.Null(l.Attempt(Ip, "name:nobody"));
    }

    [Fact]
    public void Addresses_lock_too_and_ipv6_counts_per_64()
    {
        var l = new SignInLimiter(() => new SignInLimitSettings { AttemptsPerMinutePerIp = 600, IpFreeFailures = 2, AccountFreeFailures = 100 });
        var a = IPAddress.Parse("2001:db8:1:2::1");
        var b = IPAddress.Parse("2001:db8:1:2:ffff::9");
        l.Fail(a, "name:x1");
        Assert.Null(l.Attempt(b, "name:fresh"));
        l.Fail(b, "name:x2");
        Assert.NotNull(l.Attempt(a, "name:fresh"));
        Assert.Null(l.Attempt(IPAddress.Parse("2001:db8:1:3::1"), "name:fresh"));
        Assert.Equal("203.0.113.9", SignInLimiter.Prefix(IPAddress.Parse("::ffff:203.0.113.9")));
    }
}

public sealed class ManualTime(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}

public class ExternalSignInTests
{
    private sealed class Steam : ISignInProvider
    {
        public string Id => "steam";
        public string Title => "Steam";
    }

    [Fact]
    public async Task Linked_identities_sign_in_and_nothing_else_does()
    {
        await using var h = await AuthHarness.StartAsync(kap: false);
        var auth = h.Auth;
        var c = new ClientInfo(IPAddress.Loopback, "t");
        var id = new ExternalIdentity("steam", "76561198000000001");
        Assert.Equal(400, Assert.IsType<SignInResult.Refused>(auth.SignInExternal(id, c, null)).Status);   // no such provider yet
        auth.Providers.Add(new Steam());
        Assert.Throws<InvalidOperationException>(() => auth.Providers.Add(new Steam()));
        var a = await h.AccountAsync("gamer", "moderator");
        var b = await h.AccountAsync("other", "viewer");
        Assert.Equal(401, Assert.IsType<SignInResult.Refused>(auth.SignInExternal(id, c, null)).Status);   // not linked
        var pa = auth.Resolve(auth.Sessions.Create(a, "password", false, "", "").Record)!;
        var pb = auth.Resolve(auth.Sessions.Create(b, "password", false, "", "").Record)!;
        Assert.Null(auth.Link(pa, id, c));
        Assert.Equal(409, auth.Link(pb, id, c)!.Status);                                                  // one account per identity
        var ok = Assert.IsType<SignInResult.Success>(auth.SignInExternal(id, c, null));
        Assert.Equal("gamer", ok.Account.Name);
        Assert.Equal("steam", ok.Session.Via);
        Assert.False(ok.Session.Mfa);
        auth.Accounts.Update(a.Id, x => x with { Disabled = true });
        Assert.Equal(401, Assert.IsType<SignInResult.Refused>(auth.SignInExternal(id, c, null)).Status);
        var status = await h.NewClient().JsonAsync(await h.NewClient().GetAsync("/api/auth/status"));
        Assert.Equal("steam", status!["providers"]![1]!["id"]!.GetValue<string>());
    }
}
