// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Kilnwarden.Hosting;
using Kilnwarden.Kap;
using Kilnwarden.Modules;
using Kilnwarden.WebSockets;

namespace Kilnwarden.Auth;

/// <summary>The admin pages' WebSocket transport (admin-protocol.md §12; launcher/src/api/adminWs.ts): KAP/1 frames as
/// JSON, one per message, over a signed-in socket. The browser never sees the admin token: this host holds the one KAP
/// connection (<see cref="KapClient"/>) and forwards each request after the gate (<see cref="KapGate"/>) with the
/// user's source and level stamped in. The session is checked again at every message and every few seconds; a socket
/// whose session ended closes with 4401, one whose user may not use it with 4403 (final for the page). When Kiln.Admin's
/// connection drops, every socket gets bye and close 1012 so the pages reconnect to the new one.</summary>
public sealed class KapGateway : IDisposable
{
    public const WebSocketCloseStatus NotSignedIn = (WebSocketCloseStatus)4401;
    public const WebSocketCloseStatus NotAllowed = (WebSocketCloseStatus)4403;
    public const WebSocketCloseStatus ServiceRestart = (WebSocketCloseStatus)1012, TryAgainLater = (WebSocketCloseStatus)1013;

    /// <summary>Requests one socket may have in flight, and its request rate (per second, burst).</summary>
    public const int MaxInflight = 64, RatePerSecond = 20, RateBurst = 60;

    private readonly AuthService _auth;
    private readonly KapClient _kap;
    private readonly ModuleRegistry _registry;
    private readonly IWebAdminLog _log;
    private readonly CommandRules _rules;
    private readonly ConcurrentDictionary<Socket, byte> _sockets = new();
    private readonly ConcurrentDictionary<string, int> _perAccount = new(StringComparer.Ordinal);

    public KapGateway(AuthService auth, KapClient kap, ModuleRegistry registry, IWebAdminLog log)
    {
        _auth = auth;
        _kap = kap;
        _registry = registry;
        _log = log;
        _rules = new CommandRules(kap);
        _kap.Disconnected += OnUpstreamLost;
    }

    /// <summary>How often an open socket re-checks its session without traffic.</summary>
    public TimeSpan RecheckEvery { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>How long a new socket waits for Kiln.Admin's connection before refusing.</summary>
    public TimeSpan ReadyWait { get; init; } = TimeSpan.FromSeconds(3);

    public int OpenSockets => _sockets.Count;

    private void OnUpstreamLost(string reason)
    {
        foreach (var s in _sockets.Keys) _ = s.End("kiln-admin", ServiceRestart, "Kiln.Admin reconnecting");
    }

    /// <summary>The route's handler (the route is Public so that a refused socket can be accepted and closed with
    /// 4401/4403, which a page can read; an HTTP 401 on the upgrade it cannot).</summary>
    public async Task HandleAsync(WebSocketSession ws)
    {
        var ctx = ws.Request;
        if (ctx.User is not WebPrincipal p)
        {
            await ws.CloseAsync(NotSignedIn, "sign in first").ConfigureAwait(false);
            return;
        }
        if (p.Restriction != Restriction.None)
        {
            await ws.CloseAsync(NotAllowed, PermissionGate.RestrictedDecision(p.Restriction).Message).ConfigureAwait(false);
            return;
        }
        var max = _auth.Settings.MaxSocketsPerAccount;
        if (_perAccount.AddOrUpdate(p.Id, 1, (_, n) => n + 1) > max)
        {
            Release(p.Id);
            await ws.CloseAsync(TryAgainLater, $"at most {max} open panels per account").ConfigureAwait(false);
            return;
        }
        Socket? s = null;
        try
        {
            var session = await _kap.WaitReadyAsync(ReadyWait, ws.Stopping).ConfigureAwait(false);
            if (session is null)
            {
                await ws.SendAsync(KapFrames.Payload(new JsonObject { ["t"] = "error", ["code"] = "shutting_down", ["message"] = "Kiln.Admin is not connected" })).ConfigureAwait(false);
                await ws.CloseAsync(TryAgainLater, "Kiln.Admin is not connected").ConfigureAwait(false);
                return;
            }
            s = new Socket(this, ws, p, session);
            _sockets[s] = 0;
            // the connection may have dropped between the wait and the registration (OnUpstreamLost missed this socket)
            if (!_kap.IsReady || _kap.Session?.Generation != session.Generation)
            {
                await s.End("kiln-admin", ServiceRestart, "Kiln.Admin reconnecting").ConfigureAwait(false);
                return;
            }
            await s.RunAsync().ConfigureAwait(false);
        }
        finally
        {
            if (s is not null)
            {
                _sockets.TryRemove(s, out _);
                s.Dispose();
            }
            Release(p.Id);
        }
    }

    private void Release(string accountId)
    {
        while (true)
        {
            if (!_perAccount.TryGetValue(accountId, out var n)) return;
            if (n <= 1 ? _perAccount.TryRemove(new KeyValuePair<string, int>(accountId, n)) : _perAccount.TryUpdate(accountId, n - 1, n)) return;
        }
    }

    public void Dispose()
    {
        _kap.Disconnected -= OnUpstreamLost;
        foreach (var s in _sockets.Keys) _ = s.End("unload", WebSocketCloseStatus.EndpointUnavailable, "server stopping");
    }

    /// <summary>One page's socket.</summary>
    private sealed class Socket : IDisposable
    {
        private readonly KapGateway _g;
        private readonly WebSocketSession _ws;
        private readonly KapSession _session;
        private readonly string _handle;
        private readonly CancellationTokenSource _cts;
        private readonly Channel<KapPush> _pushes = Channel.CreateBounded<KapPush>(new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
        private readonly Dictionary<string, IDisposable> _topics = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<ulong, byte> _inflight = new();
        private readonly SemaphoreSlim _slots = new(MaxInflight, MaxInflight);
        private WebPrincipal _user;
        private double _tokens = RateBurst;
        private long _tokensAt = Stopwatch.GetTimestamp();
        private long _lastSent = Environment.TickCount64, _lastRecv = Environment.TickCount64;
        private int _ended;
        private Task? _closing;

        public Socket(KapGateway g, WebSocketSession ws, WebPrincipal user, KapSession session)
        {
            _g = g;
            _ws = ws;
            _user = user;
            _session = session;
            _handle = user.Session.Handle;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ws.Stopping);
        }

        private CancellationToken Ct => _cts.Token;

        public async Task RunAsync()
        {
            var (ops, topics) = KapGate.Visible(_user, _session, _g._registry);
            var limits = _session.Limits;
            await SendAsync(new JsonObject
            {
                ["t"] = "hello", ["proto"] = "kap/1", ["kiln"] = _session.Kiln, ["admin"] = _session.Admin, ["server"] = _session.Server?.DeepClone(),
            }).ConfigureAwait(false);
            await SendAsync(new JsonObject
            {
                ["t"] = "ready", ["session"] = "ws-" + Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(6)),
                ["ops"] = new JsonArray([.. ops.Select(o => (JsonNode)o)]), ["topics"] = new JsonArray([.. topics.Select(t => (JsonNode)t)]),
                ["limits"] = new JsonObject
                {
                    ["maxFrame"] = limits.MaxFrame, ["maxClientFrame"] = Math.Min(limits.MaxClientFrame, _ws.MaxMessageBytes),
                    ["inflight"] = MaxInflight, ["pingS"] = 15, ["idleS"] = 45,
                },
                ["restart"] = _session.Restart?.DeepClone(),
            }).ConfigureAwait(false);
            var pump = Task.Run(PumpAsync);
            var watch = Task.Run(WatchAsync);
            try
            {
                while (!Ct.IsCancellationRequested)
                {
                    var msg = await _ws.ReceiveAsync(Ct).ConfigureAwait(false);
                    if (msg is null) break;
                    Volatile.Write(ref _lastRecv, Environment.TickCount64);
                    JsonObject f;
                    try
                    {
                        f = KapFrames.Decode(msg.Value.Data.Span);
                    }
                    catch (KapProtocolException e)
                    {
                        await SendAsync(new JsonObject { ["t"] = "error", ["code"] = "bad_frame", ["message"] = e.Message }).ConfigureAwait(false);
                        await CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "bad frame").ConfigureAwait(false);
                        break;
                    }
                    if (!await HandleAsync(f).ConfigureAwait(false)) break;
                }
            }
            catch (Exception e) when (e is OperationCanceledException or WebSocketException or IOException or ObjectDisposedException)
            {
                // closed under us, or ended by End()
            }
            finally
            {
                Cancel();
                _pushes.Writer.TryComplete();
                await Task.WhenAll(pump, watch).ConfigureAwait(false);
                // a close started by End() goes out before the handler returns (else the core would close with 1000 first)
                if (Volatile.Read(ref _closing) is { } closing) await closing.ConfigureAwait(false);
            }
        }

        private void Cancel()
        {
            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>One frame from the page; false ends the socket.</summary>
        private async Task<bool> HandleAsync(JsonObject f)
        {
            var t = KapFrames.Str(f, "t");
            switch (t)
            {
                case "req":
                case "sub":
                case "unsub":
                {
                    if (KapFrames.Id(f) is not { } id)
                    {
                        await SendAsync(new JsonObject { ["t"] = "error", ["code"] = "bad_frame", ["message"] = $"{t} needs an unsigned integer id" }).ConfigureAwait(false);
                        return true;
                    }
                    if (Recheck() is not { } user) return false;
                    // the page's own requests count as activity for the idle timeout (pings and pushes do not: a tab
                    // left open must still time out)
                    _g._auth.Sessions.Touch(user.Session);
                    if (t == "req") return await RequestAsync(user, id, f).ConfigureAwait(false);
                    await TopicAsync(user, id, t == "sub", KapFrames.Str(f, "topic")).ConfigureAwait(false);
                    return true;
                }
                case "ping":
                    var pong = new JsonObject { ["t"] = "pong", ["id"] = f["id"]?.DeepClone() ?? JsonValue.Create(0) };
                    if (f["ts"] is { } ts) pong["ts"] = ts.DeepClone();
                    await SendAsync(pong).ConfigureAwait(false);
                    return true;
                case "pong":
                    return true;
                case "bye":
                    await CloseAsync(WebSocketCloseStatus.NormalClosure, "bye").ConfigureAwait(false);
                    return false;
                default:
                    await SendAsync(new JsonObject { ["t"] = "error", ["code"] = "bad_frame", ["message"] = $"unknown frame type '{t}'" }).ConfigureAwait(false);
                    return true;
            }
        }

        private async Task<bool> RequestAsync(WebPrincipal user, ulong id, JsonObject f)
        {
            var op = KapFrames.Str(f, "op") ?? "";
            if (f["args"] is not null and not JsonObject)
            {
                await SendAsync(Res(id, KapResult.Fail(op, "op_failed", "args must be an object"))).ConfigureAwait(false);
                return true;
            }
            var args = f["args"] is JsonObject a ? (JsonObject)a.DeepClone() : new JsonObject();
            if (!_inflight.TryAdd(id, 0))
            {
                await SendAsync(new JsonObject { ["t"] = "error", ["code"] = "bad_frame", ["message"] = $"request {id} is already in flight", ["id"] = id }).ConfigureAwait(false);
                return true;
            }
            var decision = KapGate.CheckOp(user, op, args, _g._registry, _g._auth.Now);
            if (!decision.Allowed)
            {
                _inflight.TryRemove(id, out _);
                _g._auth.Audit.Record(AuditLog.Entry("kap." + (KapOps.IsOpName(op) ? op : "?"), user, _ws.Request.ClientAddress, false, decision.Message, Redact(op, args)));
                await SendAsync(Res(id, KapResult.Fail(op, decision.Code, decision.Message))).ConfigureAwait(false);
                return true;
            }
            if (!TakeToken() || !_slots.Wait(0))
            {
                _inflight.TryRemove(id, out _);
                await SendAsync(Res(id, KapResult.Fail(op, "busy", "too many requests: slow down"))).ConfigureAwait(false);
                return true;
            }
            _ = Task.Run(async () =>
            {
                try
                {
                    await RunOpAsync(user, id, op, args, decision.Rule!).ConfigureAwait(false);
                }
                catch (Exception e) when (e is OperationCanceledException or WebSocketException or ObjectDisposedException or IOException)
                {
                    // the socket closed under it
                }
                catch (Exception e)
                {
                    _g._log.Error($"KAP gateway: {op} for {user.Name} failed", e);
                }
                finally
                {
                    _slots.Release();
                    _inflight.TryRemove(id, out _);
                }
            });
            return true;
        }

        private async Task RunOpAsync(WebPrincipal user, ulong id, string op, JsonObject args, KapOpRule rule)
        {
            var sw = Stopwatch.StartNew();
            KapResult result;
            KapCaller? caller = user.Caller();
            string? refusal = null;
            if (op is "command_run" or "command_complete")
                (caller, refusal) = await _g._rules.ForLineAsync(user, KapFrames.Str(args, op == "command_run" ? "line" : "text"), Ct).ConfigureAwait(false);
            if (caller is null) result = KapResult.Fail(op, "forbidden", refusal ?? "not allowed");
            else
            {
                // Command metadata may have awaited upstream work. Re-check immediately before dispatch, not only
                // when the message arrived: a queued op must not retain a revoked session or stale privilege/proof.
                var live = _g._auth.ResolveHandle(_handle);
                var gate = live is null ? KapDecision.Deny("unauthenticated", "the sign-in ended")
                    : KapGate.CheckOp(live, op, args, _g._registry, _g._auth.Now);
                if (!gate.Allowed) result = KapResult.Fail(op, gate.Code, gate.Message);
                else if (live!.Account.PrivilegeStamp != user.Account.PrivilegeStamp || live.Account.CredentialStamp != user.Account.CredentialStamp)
                    result = KapResult.Fail(op, "forbidden", "permissions changed: retry the request");
                else result = await _g._kap.CallAsync(caller, op, args, Ct).ConfigureAwait(false);
                if (result.Ok && op == "commands_describe") result = WithData(result, _g._rules.FilterDescribe(user, result.Data));
                if (result.Ok && op == "command_complete")
                    result = WithData(result, await _g._rules.FilterCompleteAsync(user, KapFrames.Str(args, "text"), result.Data, Ct).ConfigureAwait(false));
            }
            if (rule.Write || !result.Ok && result.Code is "forbidden" or "refused")
            {
                var audited = Redact(op, args, caller);
                if (op == "command_run") AddCommandOutcome(audited, result);
                _g._auth.Audit.Record(AuditLog.Entry("kap." + op, user, _ws.Request.ClientAddress, result.Ok, result.Ok ? null : result.Code,
                    audited, sw.Elapsed.TotalMilliseconds));
            }
            await SendAsync(Res(id, result)).ConfigureAwait(false);
        }

        private static KapResult WithData(KapResult r, JsonNode? data)
        {
            var f = (JsonObject)r.Frame.DeepClone();
            f["data"] = data?.DeepClone();
            return KapResult.FromFrame(f);
        }

        private async Task TopicAsync(WebPrincipal user, ulong id, bool on, string? topic)
        {
            var op = on ? "sub" : "unsub";
            if (topic is null)
            {
                await SendAsync(Res(id, KapResult.Fail(op, "bad_frame", "topic required"))).ConfigureAwait(false);
                return;
            }
            if (!on)
            {
                lock (_topics)
                    if (_topics.Remove(topic, out var sub)) sub.Dispose();
                await SendAsync(Res(id, KapResult.FromFrame(new JsonObject { ["t"] = "res", ["ok"] = true, ["op"] = op }))).ConfigureAwait(false);
                return;
            }
            if (!_session.Topics.Contains(topic))
            {
                await SendAsync(Res(id, KapResult.Fail(op, "unknown_topic", $"no topic '{topic}'"))).ConfigureAwait(false);
                return;
            }
            var d = KapGate.CheckTopic(user, topic, _g._registry);
            if (!d.Allowed)
            {
                _g._auth.Audit.Record(AuditLog.Entry("kap.sub", user, _ws.Request.ClientAddress, false, d.Message, new JsonObject { ["topic"] = topic }));
                await SendAsync(Res(id, KapResult.Fail(op, d.Code, d.Message))).ConfigureAwait(false);
                return;
            }
            bool added;
            lock (_topics)
            {
                added = !_topics.ContainsKey(topic);
                if (added) _topics[topic] = _g._kap.Subscribe(topic, push => _pushes.Writer.TryWrite(push));
            }
            await SendAsync(Res(id, KapResult.FromFrame(new JsonObject { ["t"] = "res", ["ok"] = true, ["op"] = op }))).ConfigureAwait(false);
            if (added && _g._kap.LastPush(topic) is { } last) _pushes.Writer.TryWrite(last);
        }

        /// <summary>The user as of now; ends the socket (4401/4403) when the session ended or got restricted.</summary>
        private WebPrincipal? Recheck()
        {
            var p = _g._auth.ResolveHandle(_handle);
            if (p is null)
            {
                _ = End(null, NotSignedIn, "the sign-in ended");
                return null;
            }
            if (p.Restriction != Restriction.None)
            {
                _ = End(null, NotAllowed, PermissionGate.RestrictedDecision(p.Restriction).Message);
                return null;
            }
            Volatile.Write(ref _user, p);
            return p;
        }

        /// <summary>Re-checks the session, drops topics the user lost, pings a quiet page and ends a silent one.</summary>
        private async Task WatchAsync()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            var lastCheck = Environment.TickCount64;
            try
            {
                while (await timer.WaitForNextTickAsync(Ct).ConfigureAwait(false))
                {
                    var now = Environment.TickCount64;
                    if (now - lastCheck >= _g.RecheckEvery.TotalMilliseconds)
                    {
                        lastCheck = now;
                        if (Recheck() is not { } user) return;
                        lock (_topics)
                            foreach (var topic in _topics.Keys.ToList())
                                if (!KapGate.CheckTopic(user, topic, _g._registry).Allowed && _topics.Remove(topic, out var sub)) sub.Dispose();
                    }
                    if (now - Volatile.Read(ref _lastRecv) > 45_000)
                    {
                        _ = End("idle", WebSocketCloseStatus.NormalClosure, "idle");
                        return;
                    }
                    if (now - Volatile.Read(ref _lastSent) >= 15_000)
                        await SendAsync(new JsonObject { ["t"] = "ping", ["id"] = 0, ["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }).ConfigureAwait(false);
                }
            }
            catch (Exception e) when (e is OperationCanceledException or WebSocketException or ObjectDisposedException or IOException)
            {
            }
        }

        private async Task PumpAsync()
        {
            try
            {
                await foreach (var push in _pushes.Reader.ReadAllAsync(Ct).ConfigureAwait(false))
                {
                    bool allowed;
                    lock (_topics) allowed = _topics.ContainsKey(push.Topic);
                    if (!allowed) continue;
                    await _ws.SendAsync(push.Payload, WebSocketMessageType.Text, Ct).ConfigureAwait(false);
                    Volatile.Write(ref _lastSent, Environment.TickCount64);
                }
            }
            catch (Exception e) when (e is OperationCanceledException or WebSocketException or ObjectDisposedException or IOException)
            {
            }
        }

        private bool TakeToken()
        {
            lock (_topics)
            {
                var now = Stopwatch.GetTimestamp();
                _tokens = Math.Min(RateBurst, _tokens + Stopwatch.GetElapsedTime(_tokensAt, now).TotalSeconds * RatePerSecond);
                _tokensAt = now;
                if (_tokens < 1) return false;
                _tokens--;
                return true;
            }
        }

        private static JsonObject Res(ulong id, KapResult r)
        {
            var f = (JsonObject)r.Frame.DeepClone();
            f["t"] = "res";
            f["id"] = id;
            return f;
        }

        private async Task SendAsync(JsonObject frame)
        {
            await _ws.SendAsync(KapFrames.Payload(frame), WebSocketMessageType.Text, Ct).ConfigureAwait(false);
            Volatile.Write(ref _lastSent, Environment.TickCount64);
        }

        /// <summary>Sends the close frame first, then stops the loops (the receive loop sees the cancel).</summary>
        private async Task CloseAsync(WebSocketCloseStatus status, string reason)
        {
            Interlocked.Exchange(ref _ended, 1);
            try
            {
                await _ws.CloseAsync(status, reason).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // the socket is gone already
            }
            finally
            {
                Cancel();
            }
        }

        /// <summary>Ends the socket from any thread: bye (when given a reason), then the close code. The socket's handler
        /// waits for this before it returns.</summary>
        public Task End(string? byeReason, WebSocketCloseStatus status, string reason)
        {
            if (Interlocked.Exchange(ref _ended, 1) != 0) return Volatile.Read(ref _closing) ?? Task.CompletedTask;
            var t = Task.Run(async () =>
            {
                try
                {
                    if (byeReason is not null)
                        await _ws.SendAsync(KapFrames.Payload(new JsonObject { ["t"] = "bye", ["reason"] = byeReason })).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // closing anyway
                }
                try
                {
                    await _ws.CloseAsync(status, reason).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // the socket is gone already
                }
                Cancel();
            });
            Volatile.Write(ref _closing, t);
            return t;
        }

        public void Dispose()
        {
            lock (_topics)
            {
                foreach (var s in _topics.Values) s.Dispose();
                _topics.Clear();
            }
            _cts.Dispose();
        }
    }

    /// <summary>KAP arguments are unstructured (commands and configs may contain arbitrary secrets). Keep only a
    /// count, never values or user-controlled field names. The operation, actor and outcome remain in the audit entry.
    /// command_run adds what can be told without its text (<see cref="AddCommandMetadata"/>).</summary>
    internal static JsonObject Redact(string op, JsonObject args, KapCaller? caller = null)
    {
        var o = new JsonObject { ["redacted"] = true, ["fieldCount"] = args.Count };
        if (op == "command_run") AddCommandMetadata(o, args, caller);
        return o;
    }

    /// <summary>The longest first word recorded as a command name; anything longer is not a command name.</summary>
    internal const int MaxAuditedCommandName = 32;

    /// <summary>command_run's audit metadata: <c>command</c>, the line's first word lower-cased without the slash,
    /// only when it looks like a command name ([a-z0-9][a-z0-9_.-]*, at most 32 characters; otherwise null with
    /// <c>commandValid</c> false, since a mistyped line can start with a secret); <c>argCount</c> (words after it, values
    /// never kept); <c>asPlayer</c> (a steamId was given, the id itself not kept); <c>level</c> (the command level the
    /// line ran at, when the gate got that far).</summary>
    private static void AddCommandMetadata(JsonObject o, JsonObject args, KapCaller? caller)
    {
        var line = (KapFrames.Str(args, "line") ?? "").Trim();
        if (line.StartsWith('/')) line = line[1..];
        var words = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var name = words.Length > 0 ? words[0].ToLowerInvariant() : "";
        var valid = name.Length is > 0 and <= MaxAuditedCommandName && IsCommandName(name);
        o["command"] = valid ? name : null;
        o["commandValid"] = valid;
        o["argCount"] = Math.Max(0, words.Length - 1);
        o["asPlayer"] = args["steamId"] is not null;
        if (caller is { IsPanel: false } c) o["level"] = CommandLevels.Name(c.Level);
    }

    /// <summary>The command result fields Kiln.Admin's command_run answers with that the audit keeps (booleans only).</summary>
    internal static readonly string[] CommandOutcomeFields = ["found", "ok", "denied", "canceled", "parseError"];

    /// <summary>command_run's own outcome, next to the entry's transport <c>ok</c> (which only says the request reached
    /// Kiln.Admin and it answered): <c>outcome</c> = {found, ok, denied, canceled, parseError} from the result's data, each
    /// true/false as Kiln.Admin sent it (null when it sent no boolean). Only when the request reached Kiln.Admin and its
    /// data is an object. Never the data's name, error or output: those can echo what was typed.</summary>
    internal static void AddCommandOutcome(JsonObject audited, KapResult result)
    {
        if (!result.Ok || result.Data is not JsonObject data) return;
        var outcome = new JsonObject();
        foreach (var f in CommandOutcomeFields)
            outcome[f] = KapFrames.Bool(data, f) is { } b ? JsonValue.Create(b) : null;
        audited["outcome"] = outcome;
    }

    private static bool IsCommandName(string s)
    {
        if (!char.IsAsciiLetterOrDigit(s[0])) return false;
        foreach (var ch in s)
            if (!(char.IsAsciiLetterLower(ch) || char.IsAsciiDigit(ch) || ch is '_' or '.' or '-')) return false;
        return true;
    }
}
