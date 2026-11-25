// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kilnwarden.Auth;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Kap;
using Kilnwarden.Modules;
using Kilnwarden.Storage;

namespace Kilnwarden.Fuzz;

/// <summary>The auth lane's parsers: KAP frames (both directions), endpoint.json and the token file, the session
/// cookie, the JSON bodies of the auth and users routes, the stored files (accounts, roles, sessions), TOTP and
/// recovery codes, base32, stored password hashes, command lines and patterns, and the gate's invariant on a page's
/// frames (a command op it lets through always leaves with the user's source and level).</summary>
public static class AuthTargets
{
    private static string Utf8(byte[] b) => Encoding.UTF8.GetString(b);

    private static readonly string[] JsonDict =
        ["{", "}", "[", "]", "\"", ":", ",", "null", "true", "false", "1e999", "-0", "\\u0000", "\\ud800", "\"t\"", "\"id\"", "\"op\"", "\"args\"", "\"source\"", "\"level\""];

    private static readonly string[] KapOpsDict =
        ["\"req\"", "\"sub\"", "\"command_run\"", "\"command\"", "\"login_mint\"", "\"login_redeem\"", "\"commands_describe\"", "\"command_complete\"", "\"steamId\"", "\"console\"", "\"dashboard\""];

    public static readonly Target[] All =
    [
        new("kap-frame", KapFrame, [typeof(KapProtocolException)],
            ["{\"t\":\"res\",\"id\":7,\"ok\":true,\"op\":\"players\",\"ms\":1,\"data\":[]}", "{\"t\":\"push\",\"topic\":\"state\",\"seq\":1,\"data\":{}}"], [.. JsonDict, .. KapOpsDict]),
        new("kap-endpoint", Endpoint, [typeof(KapUnavailableException)],
            ["{\"format\":1,\"protocol\":\"kap/1\",\"host\":\"127.0.0.1\",\"port\":15637,\"pid\":1,\"token\":\"admin-token\"}"], [.. JsonDict, "\"host\"", "\"::1\"", "\"10.0.0.1\"", "65535", "0"]),
        new("kap-token", b => Check(KapEndpoint.ParseToken(b).Length == 32, "token not 32 bytes"), [typeof(KapUnavailableException)],
            ["000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f\n"], ["\n", "\r\n", "0", "f", "F", "g"]),
        new("session-cookie", SessionCookie, [],
            ["wa-session=" + new string('A', 43) + "; __Host-wa-session=" + new string('_', 43), "wa-session=\"abc\"; x=1"], ["=", ";", "\"", " ", "__Host-", "wa-session", "-", "_"]),
        new("auth-json", AuthJson, [typeof(HttpError)],
            ["{\"name\":\"alice\",\"password\":\"x\"}", "{\"code\":\"123 456\",\"recoveryCode\":null}", "{\"roles\":[\"admin\"],\"disabled\":true}",
             "{\"id\":\"helpers\",\"grants\":[\"spawn.*\"],\"commandLevel\":\"admin\",\"commandOnly\":[\"kick\"]}"], [.. JsonDict, "\"roles\"", "\"grants\""]),
        new("accounts-file", AccountsFile, [typeof(InvalidDataException)],
            ["{\"format\":1,\"accounts\":[{\"id\":\"u-0123456789abcdef\",\"name\":\"alice\",\"passwordHash\":\"\",\"roles\":[\"owner\"],\"recoveryCodes\":[],\"links\":{}}]}"],
            [.. JsonDict, "\"roles\"", "\"owner\"", "\"totpSecret\"", "\"links\"", "\"u-0123456789abcdef\""]),
        new("roles-file", RolesFile, [typeof(InvalidDataException)],
            ["{\"format\":1,\"roles\":[{\"id\":\"moderator\",\"name\":\"M\",\"grants\":[\"a.*\"],\"commandLevel\":\"everyone\",\"commandOnly\":null}]}"],
            [.. JsonDict, "\"commandLevel\"", "\"console\"", "\"owner\"", "\"*\""]),
        new("sessions-file", SessionsFile, [],
            ["{\"format\":1,\"sessions\":[{\"hash\":\"" + new string('a', 64) + "\",\"handle\":\"s-1\",\"accountId\":\"u-1\",\"expires\":\"2999-01-01T00:00:00Z\",\"lastSeen\":\"2999-01-01T00:00:00Z\"}]}"],
            JsonDict),
        new("totp-code", Totp_, [], ["123 456", "12-34-56", "000000"], [" ", "-", "0", "9", "\0", "١"]),
        new("recovery-code", b => RecoveryCodes.Match([RecoveryCodes.Hash("ABCDEFGHJK")], Utf8(b)), [], ["ABCDE-FGHJK", "abcde fghjk"], ["-", " ", "A", "I", "0"]),
        new("base32", b => Base32.Decode(Utf8(b)), [], ["MZXW6YTBOI", "mzxw6ytboi======"], ["=", " ", "A", "7", "8", "1"]),
        new("password-hash", b => PasswordHasher.TryParse(Utf8(b), out _, out _, out _), [],
            ["pbkdf2-sha256$600000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="], ["$", "=", "+", "/", "-1", "99999999999"]),
        new("command-line", CommandLine, [], ["/kick bob", "  k\ttroll", "claims.command.*|claim"], ["/", " ", "\t", ".", "*", "|", "　"]),
        new("page-frame-gate", PageFrameGate, [typeof(KapProtocolException)],
            ["{\"t\":\"req\",\"id\":1,\"op\":\"command_run\",\"args\":{\"line\":\"/stop\",\"source\":\"dashboard\",\"level\":\"console\"}}",
             "{\"t\":\"req\",\"id\":2,\"op\":\"login_mint\",\"args\":{\"consumer\":\"kilnwarden\"}}", "{\"t\":\"sub\",\"id\":3,\"topic\":\"log\"}"],
            [.. JsonDict, .. KapOpsDict, "\"kill\"", "\"players\"", "\"brand_new_op\""]),
    ];

    private static void Check(bool ok, string what)
    {
        if (!ok) throw new FuzzFinding(what);
    }

    private static void KapFrame(byte[] b)
    {
        // both framings: as a WebSocket message and behind a length prefix
        if (b.Length >= 4)
            try
            {
                var len = KapFrames.Length(b.AsSpan(0, 4), 1 << 20);
                Check(len > 0, "zero length passed");
            }
            catch (KapProtocolException)
            {
            }
        var f = KapFrames.Decode(b);
        Check(KapFrames.Str(f, "t") is not null, "frame without t passed");
        _ = KapFrames.Id(f);
        _ = KapFrames.Id(f, "seq");
        _ = KapFrames.Bool(f, "ok");
        _ = KapFrames.Int(f, "ms");
        _ = KapLimits.From(f["limits"] as JsonObject);
        var round = KapFrames.Decode(KapFrames.Payload(f));
        Check(round.ToJsonString() == f.ToJsonString(), "frame does not round-trip");
    }

    private static void Endpoint(byte[] b)
    {
        var ep = KapEndpoint.Parse(b);
        Check(IPAddress.IsLoopback(ep.Address), "a non-loopback endpoint passed");
        Check(ep.Port is > 0 and < 65536, "bad port passed");
    }

    private static void SessionCookie(byte[] b)
    {
        var header = Encoding.Latin1.GetString(b);
        foreach (var (name, value) in Cookies.Parse(header))
        {
            if (!SecretToken.IsWellFormed(value)) continue;
            Check(value.Length == SecretToken.TextLength, "a malformed token passed");
            Check(Sessions.Value.Find(value) is null, "a random token found a session");
            _ = SecretToken.Hash(value);
        }
        Check(Sessions.Value.FindByHandle(header) is null, "a random handle found a session");
    }

    private static readonly Lazy<SessionStore> Sessions = new(() =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "kwa-fuzz-sessions-" + Environment.ProcessId);
        var s = new SessionStore(new DataStore(dir), () => new AuthSettings(), new NullLog());
        s.Create(new Account { Id = "u-0000000000000001", Name = "alice" }, "password", false, "", "");
        return s;
    });

    private static void AuthJson(byte[] b)
    {
        // every body the auth and users routes read (JsonBody: strict, HttpError for anything wrong)
        TryBody<AuthRoutes.LoginBody>(b);
        TryBody<AuthRoutes.CodeBody>(b);
        TryBody<AuthRoutes.SetupBody>(b);
        TryBody<AuthRoutes.SecretBody>(b);
        TryBody<AuthRoutes.PasswordBody>(b);
        TryBody<AuthRoutes.ConfirmBody>(b);
        TryBody<UserRoutes.CreateBody>(b);
        TryBody<UserRoutes.PatchBody>(b);
        if (TryBody<UserRoutes.RoleBody>(b) is { } r)
        {
            var role = new RoleDef
            {
                Id = r.Id ?? "x", Name = r.Name ?? "x", Grants = r.Grants?.Where(g => g is not null).ToList() ?? [], CommandDeny = r.CommandDeny?.Where(g => g is not null).ToList() ?? [],
                CommandOnly = r.CommandOnly?.Where(g => g is not null).ToList(), CommandLevel = CommandLevels.TryParse(r.CommandLevel, out var l) ? l : CommandLevel.None,
            };
            if (RoleStore.Validate(role) is null)
                Check(role.CommandLevel <= BuiltinRoles.MaxLevel(role.Id) && !role.Grants.Contains("*"), "an over-powered role validated");
        }
        if (TryBody<AuthRoutes.CodeBody>(b) is { } c)
        {
            Totp.TryNormalize(c.Code, out _);
            RecoveryCodes.Normalize(c.RecoveryCode);
        }
    }

    private static T? TryBody<T>(byte[] b) where T : class
    {
        try
        {
            return JsonBody.Parse<T>(b);
        }
        catch (HttpError)
        {
            return null;
        }
    }

    private static readonly string FileDir = Path.Combine(Path.GetTempPath(), "kwa-fuzz-files-" + Environment.ProcessId);

    private static void AccountsFile(byte[] b)
    {
        var data = new DataStore(FileDir);
        File.WriteAllBytes(data.PathOf(AccountStore.File), b);
        var s = new AccountStore(data);
        foreach (var a in s.All)
        {
            Check(AccountNames.IsValid(a.Name) && a.Roles.All(RoleNames.IsValid), "an invalid account loaded");
            _ = s.FindByName(a.Name);
        }
        _ = s.OwnerExists;
        _ = s.PrimaryOwner;
    }

    private static void RolesFile(byte[] b)
    {
        var data = new DataStore(FileDir);
        File.WriteAllBytes(data.PathOf(RoleStore.File), b);
        var s = new RoleStore(data);
        Check(BuiltinRoles.All.All(id => s.Find(id) is not null), "a built-in role is missing");
        foreach (var r in s.All)
        {
            Check(RoleStore.Validate(r) is null, "an invalid role loaded");
            Check(r.CommandLevel <= BuiltinRoles.MaxLevel(r.Id), "a role above its command ceiling loaded");
        }
    }

    private static void SessionsFile(byte[] b)
    {
        var data = new DataStore(FileDir);
        File.WriteAllBytes(data.PathOf(SessionStore.File), b);
        using var s = new SessionStore(data, () => new AuthSettings(), new NullLog());
        _ = s.Count;
        _ = s.FindByHandle("s-1");
    }

    private static void Totp_(byte[] b)
    {
        var code = Utf8(b);
        if (Totp.TryNormalize(code, out var digits)) Check(digits.Length == 6 && digits.All(char.IsAsciiDigit), "a non-code normalized");
        _ = Totp.Verify(new byte[20], code, DateTimeOffset.UnixEpoch.AddYears(56), 0);
    }

    private static void CommandLine(byte[] b)
    {
        var s = Utf8(b);
        var bar = s.IndexOf('|');
        var line = bar < 0 ? s : s[(bar + 1)..];
        var pattern = bar < 0 ? "kick" : s[..bar];
        var word = CommandRules.FirstWord(line);
        Check(word.Length <= 64 && !word.Any(char.IsWhiteSpace), "a first word with spaces");
        if (CommandPattern.IsValid(pattern)) _ = CommandPattern.Matches(pattern, word, [word], word.Contains('.') ? word : null);
        var role = new RoleDef { Id = "r", Name = "r", CommandLevel = CommandLevel.Admin, CommandOnly = CommandPattern.IsValid(pattern) ? [pattern] : null };
        _ = CommandRules.Permits(role, null, word);
    }

    private static readonly Lazy<(ModuleRegistry Reg, WebPrincipal Mod, WebPrincipal Owner)> Gate = new(() =>
    {
        var reg = new ModuleRegistry();
        foreach (var m in KapPolicy.Modules)
            reg.Register(new ModuleSpec { Id = m.Id, Title = m.Title, Permissions = KapPolicy.PermissionsOf(m.Id) }, "fuzz");
        var session = new SessionRecord { Handle = "s-1", AccountId = "u-0000000000000001" };
        var mod = new WebPrincipal(new Account { Id = "u-0000000000000001", Name = "mia", Roles = ["moderator"] }, [RoleStore.Default("moderator")], session, reg);
        var owner = new WebPrincipal(new Account { Id = "u-0000000000000002", Name = "own", Roles = ["owner"] }, [RoleStore.Default("owner")], session, reg);
        return (reg, mod, owner);
    });

    /// <summary>A page's frame through the gate and the stamp: blocked ops never pass, and a command op that passes
    /// leaves with source web:&lt;user&gt; and the user's level, whatever the frame said.</summary>
    private static void PageFrameGate(byte[] b)
    {
        var f = KapFrames.Decode(b);
        var (reg, mod, owner) = Gate.Value;
        var op = KapFrames.Str(f, "op") ?? "";
        var args = f["args"] as JsonObject;
        foreach (var p in new[] { mod, owner })
        {
            var d = KapGate.CheckOp(p, op, args, reg);
            if (KapOps.Never.Contains(op) || KapOps.PanelOnly.Contains(op)) Check(!d.Allowed, $"{op} passed the gate");
            if (!d.Allowed) continue;
            if (KapPolicy.Rule(op) is { Permission: { } perm }) Check(p.Has(perm), "the gate let through an op without its permission");
            JsonObject stamped;
            try
            {
                stamped = KapOps.Stamp(p.Caller(), op, args);
            }
            catch (KapRefusedException)
            {
                Check(!KapOps.CommandOps.Contains(op), "a gated command op was refused by the stamp");
                continue;
            }
            if (KapOps.CommandOps.Contains(op))
            {
                Check(KapFrames.Str(stamped, "source") == "web:" + p.Name, "a command op left without the user's source");
                Check(KapFrames.Str(stamped, "level") == CommandLevels.Wire(p.CommandLevel), "a command op left without the user's level");
            }
        }
        if (KapFrames.Str(f, "topic") is { } topic)
            _ = KapGate.CheckTopic(mod, topic, reg);
    }

    private sealed class NullLog : IWebAdminLog
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? error = null) { }
    }
}
