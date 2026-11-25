// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Config;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Kap;
using Kilnwarden.Modules;
using Kilnwarden.Offline.Support;
using Kilnwarden.Routing;
using Kilnwarden.Server;
using Kilnwarden.Storage;

namespace Kilnwarden.Offline.Auth;

/// <summary>Pure service/pipeline checks: no listener, upstream connection or game.</summary>
public class DefensivePolicyTests
{
    [Fact]
    public void Unknown_ops_and_topics_fail_closed_even_for_owner()
    {
        using var f = new Fixture();
        var p = f.Principal();
        Assert.False(KapGate.CheckOp(p, "future_op", null, f.Registry).Allowed);
        Assert.False(KapGate.CheckTopic(p, "future_topic", f.Registry).Allowed);
    }

    [Theory]
    [InlineData("config_set")]
    [InlineData("command_run")]
    [InlineData("future_op")]
    public void Audit_never_keeps_unstructured_game_arguments(string op)
    {
        var args = new JsonObject { ["line"] = "private fixture value", ["nested"] = new JsonObject { ["Password"] = "private fixture value" } };
        Assert.DoesNotContain("private fixture value", KapGateway.Redact(op, args).ToJsonString());
    }

    /// <summary>Audit follow-up: command_run keeps the parsed command name and safe shape metadata (argument count,
    /// run-as-player flag, the level it ran at), never the line, its arguments or the player id.</summary>
    [Fact]
    public void Command_run_audit_keeps_the_command_name_and_safe_metadata_only()
    {
        var args = new JsonObject { ["line"] = "  /Kick bob private-fixture-secret", ["steamId"] = "76561198000000042", ["source"] = "dashboard" };
        var o = KapGateway.Redact("command_run", args, KapCaller.ForUser("ann", CommandLevel.Admin));
        var json = o.ToJsonString();
        Assert.Equal("kick", o["command"]!.GetValue<string>());
        Assert.Equal(2, o["argCount"]!.GetValue<int>());
        Assert.True(o["asPlayer"]!.GetValue<bool>());
        Assert.Equal("admin", o["level"]!.GetValue<string>());
        Assert.True(o["redacted"]!.GetValue<bool>());
        foreach (var leak in new[] { "bob", "private-fixture-secret", "76561198000000042", "dashboard", "Kick" })
            Assert.DoesNotContain(leak, json);
    }

    [Theory]
    [InlineData("hunter2=private-fixture-secret")]
    [InlineData("/p@ss:private-fixture-secret rest")]
    [InlineData("private-fixture-secret-that-is-much-longer-than-any-command-name")]
    [InlineData("/ünïcode-private-fixture-secret")]
    public void Command_run_audit_drops_first_words_that_are_not_command_names(string line)
    {
        var o = KapGateway.Redact("command_run", new JsonObject { ["line"] = line }, null);
        Assert.DoesNotContain("private-fixture-secret", o.ToJsonString());
        Assert.Null(o["command"]);
        Assert.False(o["commandValid"]!.GetValue<bool>());
        Assert.Null(o["level"]);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData("/help", 0)]
    [InlineData("help   a\tb  c ", 3)]
    public void Command_run_audit_counts_arguments_without_keeping_them(string? line, int count)
    {
        var args = new JsonObject();
        if (line is not null) args["line"] = line;
        var o = KapGateway.Redact("command_run", args, null);
        Assert.Equal(count, o["argCount"]!.GetValue<int>());
        Assert.False(o["asPlayer"]!.GetValue<bool>());
    }

    /// <summary>Audit follow-up 2: command_run entries add the command's own outcome from Kiln.Admin's result (ran ok,
    /// not found, denied, parse error, canceled) as booleans, never its name, error or output text.</summary>
    [Theory]
    [InlineData(true, true, false, false, false)]   // ran ok
    [InlineData(false, false, false, false, false)] // no such command
    [InlineData(true, false, true, false, false)]   // denied
    [InlineData(true, false, false, false, true)]   // parse error
    [InlineData(true, false, false, true, false)]   // canceled by a hook
    public void Command_run_audit_adds_the_commands_own_outcome_as_booleans(bool found, bool ok, bool denied, bool canceled, bool parseError)
    {
        var res = KapResult.FromFrame(new JsonObject
        {
            ["t"] = "res", ["ok"] = true, ["op"] = "command_run", ["ms"] = 1,
            ["data"] = new JsonObject
            {
                ["name"] = "private-fixture-name", ["found"] = found, ["ok"] = ok, ["denied"] = denied, ["canceled"] = canceled,
                ["parseError"] = parseError, ["error"] = "private-fixture-error", ["output"] = new JsonArray("private-fixture-output"),
            },
        });
        var o = KapGateway.Redact("command_run", new JsonObject { ["line"] = "/time private-fixture-arg" }, null);
        KapGateway.AddCommandOutcome(o, res);
        var outcome = o["outcome"]!.AsObject();
        Assert.Equal(KapGateway.CommandOutcomeFields, outcome.Select(kv => kv.Key));
        Assert.Equal(found, outcome["found"]!.GetValue<bool>());
        Assert.Equal(ok, outcome["ok"]!.GetValue<bool>());
        Assert.Equal(denied, outcome["denied"]!.GetValue<bool>());
        Assert.Equal(canceled, outcome["canceled"]!.GetValue<bool>());
        Assert.Equal(parseError, outcome["parseError"]!.GetValue<bool>());
        Assert.DoesNotContain("private-fixture", o.ToJsonString());
    }

    [Fact]
    public void Command_run_outcome_is_left_out_when_the_request_failed_and_non_booleans_become_null()
    {
        var o = KapGateway.Redact("command_run", new JsonObject { ["line"] = "/time" }, null);
        KapGateway.AddCommandOutcome(o, KapResult.Fail("command_run", "op_failed", "line required"));
        KapGateway.AddCommandOutcome(o, KapResult.FromFrame(new JsonObject { ["t"] = "res", ["ok"] = true, ["op"] = "command_run", ["data"] = new JsonArray() }));
        Assert.Null(o["outcome"]);
        KapGateway.AddCommandOutcome(o, KapResult.FromFrame(new JsonObject
        {
            ["t"] = "res", ["ok"] = true, ["op"] = "command_run",
            ["data"] = new JsonObject { ["found"] = "private-fixture-secret", ["ok"] = 1, ["denied"] = new JsonObject { ["x"] = true } },
        }));
        var outcome = o["outcome"]!.AsObject();
        Assert.All(KapGateway.CommandOutcomeFields, f => Assert.True(outcome.ContainsKey(f) && outcome[f] is null));
        Assert.DoesNotContain("private-fixture-secret", o.ToJsonString());
    }

    [Theory]
    [InlineData("config_set")]
    [InlineData("command_complete")]
    [InlineData("future_op")]
    public void Other_ops_keep_only_the_field_count(string op)
    {
        var o = KapGateway.Redact(op, new JsonObject { ["line"] = "/kick bob", ["text"] = "/kick" }, KapCaller.ForUser("ann", CommandLevel.Admin));
        Assert.Equal(["redacted", "fieldCount"], o.Select(kv => kv.Key));
    }

    [Fact]
    public async Task Sensitive_actions_require_explicit_recent_proof()
    {
        using var f = new Fixture();
        var p = f.Principal();
        var r = await f.Request("POST", "/api/users", p, new { name = "helper", password = "another fixture password", roles = new[] { "viewer" } });
        Assert.Equal(403, r.Status);
        Assert.Contains("reauth_required", Encoding.UTF8.GetString(r.Body.Span));
        Assert.False(KapGate.CheckOp(p, "config_set", null, f.Registry).Allowed);
    }

    [Fact]
    public async Task Reauth_contract_and_session_scope()
    {
        using var f = new Fixture();
        var p = f.Principal();
        Assert.Equal(400, (await f.Request("POST", "/api/auth/reauth", p, new { })).Status);
        Assert.Equal(400, (await f.Request("POST", "/api/auth/reauth", p, new { password = "fixture password", code = "123456" })).Status);
        Assert.Equal(401, (await f.Request("POST", "/api/auth/reauth", p, new { password = "wrong" })).Status);
        var good = await f.Request("POST", "/api/auth/reauth", p, new { password = "fixture password" });
        Assert.Equal(200, good.Status);
        Assert.Contains("reauthenticatedUntil", Encoding.UTF8.GetString(good.Body.Span));
        var fresh = f.Auth.ResolveHandle(p.Session.Handle)!;
        Assert.True(KapGate.CheckOp(fresh, "config_set", null, f.Registry).Allowed);
        var other = f.Auth.Resolve(f.Sessions.Create(f.Account, "launcher", true, "127.0.0.1", "").Record)!;
        Assert.False(KapGate.CheckOp(other, "config_set", null, f.Registry).Allowed);
        f.Time.Advance(TimeSpan.FromMinutes(10));
        var expired = f.Auth.ResolveHandle(p.Session.Handle)!;
        Assert.False(KapGate.CheckOp(expired, "config_set", null, f.Registry, f.Time.GetUtcNow()).Allowed);

    }

    [Fact]
    public async Task Enrolled_totp_steps_up_once_but_recovery_codes_do_not()
    {
        using var f = new Fixture();
        var secret = Totp.NewSecret();
        f.Auth.Accounts.Update(f.Account.Id, x => x with { TotpSecret = Base32.Encode(secret), RecoveryCodes = [RecoveryCodes.Hash("ABCDE-FGHJK")] });
        var p = f.Principal();
        Assert.Equal(401, (await f.Request("POST", "/api/auth/reauth", p, new { code = "ABCDE-FGHJK" })).Status);
        var code = Totp.Code(secret, Totp.StepAt(f.Time.GetUtcNow()));
        Assert.Equal(200, (await f.Request("POST", "/api/auth/reauth", p, new { code })).Status);
        Assert.Equal(401, (await f.Request("POST", "/api/auth/reauth", p, new { code })).Status);
        Assert.Single(f.Auth.Accounts.Find(p.Id)!.RecoveryCodes);
    }

    [Fact]
    public async Task Touch_and_privilege_rotation_do_not_preserve_stale_step_up_state()
    {
        using var f = new Fixture();
        var p = f.Principal();
        Assert.Equal(200, (await f.Request("POST", "/api/auth/reauth", p, new { password = "fixture password" })).Status);
        f.Time.Advance(TimeSpan.FromMinutes(1));
        f.Sessions.Touch(p.Session); // a request that began before reauth must not overwrite the proof
        Assert.True(f.Auth.RecentlyAuthenticated(p));
        var updated = f.Auth.Accounts.Update(p.Id, x => x with { PrivilegeStamp = x.PrivilegeStamp + 1 });
        Assert.False(f.Auth.RecentlyAuthenticated(p));
        f.Sessions.Rotate(f.Sessions.FindByHandle(p.Session.Handle)!, updated);
        Assert.False(f.Auth.ResolveHandle(p.Session.Handle)!.Session.HasRecentAuthentication(f.Time.GetUtcNow()));
    }

    [Fact]
    public async Task Revocation_during_queued_password_work_cannot_authorize_user_creation()
    {
        using var f = new Fixture();
        var p = f.Principal();
        Assert.Equal(200, (await f.Request("POST", "/api/auth/reauth", p, new { password = "fixture password" })).Status);
        var slots = (SemaphoreSlim)typeof(PasswordHasher).GetField("_slots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(f.Auth.Hasher)!;
        Assert.True(slots.Wait(0));
        var request = f.Request("POST", "/api/users", p, new { name = "helper", password = "another fixture password", roles = new[] { "viewer" } });
        Assert.False(request.IsCompleted);
        f.Sessions.Revoke(p.Session.Handle);
        slots.Release();
        Assert.Equal(401, (await request).Status);
        Assert.Null(f.Auth.Accounts.FindByName("helper"));
    }

    [Fact]
    public async Task Credential_reset_during_second_factor_completion_invalidates_the_ticket()
    {
        using var f = new Fixture();
        var secret = Totp.NewSecret();
        f.Auth.Accounts.Update(f.Account.Id, x => x with { TotpSecret = Base32.Encode(secret) });
        var c = new ClientInfo(IPAddress.Loopback, "");
        var ticket = Assert.IsType<SignInResult.TwoFactorNeeded>(await f.Auth.SignInAsync(f.Account.Name, "fixture password", c, null, default));
        var reset = false;
        f.Auth.Accounts.AccountChanged += (_, next) =>
        {
            if (!reset && next is { TotpLastStep: > 0 })
            {
                reset = true;
                f.Auth.Accounts.Update(next.Id, x => x with { CredentialStamp = x.CredentialStamp + 1 });
            }
        };
        var result = f.Auth.CompleteTwoFactor(ticket.Ticket, Totp.Code(secret, Totp.StepAt(f.Time.GetUtcNow())), null, c, null);
        Assert.IsType<SignInResult.Refused>(result);
        Assert.Empty(f.Sessions.ForAccount(f.Account.Id));
    }

    [Fact]
    public void Account_attempt_budget_cannot_be_bypassed_with_other_addresses()
    {
        var s = new SignInLimitSettings { AttemptsPerMinutePerIp = 600 };
        var limiter = new SignInLimiter(() => s);
        for (var i = 1; i <= 10; i++) Assert.Null(limiter.Attempt(IPAddress.Parse($"192.0.2.{i}"), "name:alice"));
        Assert.NotNull(limiter.Attempt(IPAddress.Parse("192.0.2.11"), "name:alice"));
        limiter.Succeed("name:alice");
        Assert.NotNull(limiter.Attempt(IPAddress.Parse("192.0.2.12"), "name:alice"));
    }

    [Fact]
    public void Public_bind_does_not_disable_host_allowlist()
    {
        var policy = ServerPolicy.From(new ServerSettings(), true);
        Assert.False(policy.HostAllowed("unlisted.example"));
    }

    [Fact]
    public void Forwarded_ambiguity_fails_closed()
    {
        var s = new ServerSettings();
        s.ReverseProxy.Enabled = true;
        var h = new HttpHeaders();
        h.Add("X-Forwarded-For", "192.0.2.10");
        h.Add("X-Forwarded-For", "192.0.2.11");
        var req = new HttpRequest { Method = "GET", Target = "/", Path = "/", Headers = h, Host = "localhost" };
        Assert.Throws<HttpError>(() => ServerPolicy.From(s, false).Resolve(req, IPAddress.Loopback, false));
    }

    [Fact]
    public void Null_auth_settings_are_refused_not_thrown()
    {
        Assert.NotEmpty(AuthSettings.Validate(new AuthSettings { SignIn = null! }));
    }

    [Fact]
    public void Entire_head_including_blank_lines_is_bounded()
    {
        var bytes = Encoding.ASCII.GetBytes("\r\n\r\nGET / HTTP/1.1\r\nHost: h\r\n\r\n");
        Assert.Throws<HttpError>(() => HttpParser.TryParseHead(bytes, new HttpLimits(bytes.Length - 1, 10, 100), out _, out _));
    }

    private sealed class Fixture : IDisposable
    {
        public readonly TempDir Temp = new();
        public readonly ManualTime Time = new(DateTimeOffset.UtcNow);
        public readonly AuthSettings Settings = new() { SignIn = new() { AttemptsPerMinutePerIp = 600 } };
        public readonly ModuleRegistry Registry = new();
        public readonly SessionStore Sessions;
        public readonly AuthService Auth;
        public readonly Account Account;
        private readonly PasswordHasher _hasher;
        private readonly AuditLog _audit;
        private readonly RequestPipeline _pipeline;
        public Fixture()
        {
            var data = new DataStore(Temp.Path);
            var log = new TestLog();
            var accounts = new AccountStore(data, Time);
            var roles = new RoleStore(data);
            Sessions = new SessionStore(data, () => Settings, log, Time);
            _hasher = new PasswordHasher(1000, 1, null, null, allowWeak: true);
            _audit = new AuditLog(data, log);
            Auth = new AuthService(accounts, roles, Sessions, _hasher, new SignInLimiter(() => Settings.SignIn, Time), () => Settings, _audit, Registry, log, time: Time);
            Account = accounts.Add(new Account { Name = "owner1", PasswordHash = _hasher.HashAsync("fixture password", default).GetAwaiter().GetResult(), Roles = ["owner"] });
            AuthRoutes.Map(Registry.Register(new ModuleSpec { Id = "auth", Title = "Auth" }, "test"), Auth);
            UserRoutes.Map(Registry.Register(new ModuleSpec { Id = "users", Title = "Users", Permissions = [new PermissionSpec("users.view", "View"), new PermissionSpec("users.manage", "Manage") { Sensitive = true }, new PermissionSpec("users.roles", "Roles") { Sensitive = true }] }, "test"), Auth);
            foreach (var m in KapPolicy.Modules)
                Registry.Register(new ModuleSpec { Id = m.Id, Title = m.Title, Permissions = KapPolicy.PermissionsOf(m.Id) }, "test");
            _pipeline = new RequestPipeline(Registry, [], new PermissionGate(_audit, Auth), _ => ValueTask.FromResult(HttpResponse.NotFound()), log);
        }
        public WebPrincipal Principal() => Auth.Resolve(Sessions.Create(Account, "password", false, "127.0.0.1", "").Record)!;
        public async Task<HttpResponse> Request(string method, string path, WebPrincipal user, object body)
        {
            var headers = new HttpHeaders();
            headers.Set("Content-Type", "application/json");
            var ctx = new RequestContext
            {
                Request = new HttpRequest { Method = method, Target = path, Path = path, Headers = headers, Body = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(body) },
                Connection = new ConnectionInfo { Id = 1, PeerAddress = IPAddress.Loopback, LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 443) }, Services = new WebAdminServices(),
                ClientAddress = IPAddress.Loopback, Scheme = "https", Host = "localhost", User = user,
            };
            Assert.True(PathSegments.TryParse(path, out var segments));
            _pipeline.Match(ctx, segments);
            return await _pipeline.RunAsync(ctx);
        }
        public void Dispose() { Sessions.Dispose(); _hasher.Dispose(); _audit.DisposeAsync().AsTask().GetAwaiter().GetResult(); Temp.Dispose(); }
    }
}
