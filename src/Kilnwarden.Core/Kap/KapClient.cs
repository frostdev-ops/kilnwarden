// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Kilnwarden.Hosting;

namespace Kilnwarden.Kap;

public sealed class KapClientOptions
{
    /// <summary>kiln/data/kiln-admin (endpoint.json, admin-token).</summary>
    public required string AdminDirectory { get; init; }

    public required IWebAdminLog Log { get; init; }

    /// <summary>The auth frame's <c>client</c>: Kiln.Admin logs it and uses it as the default <c>by</c>.</summary>
    public string ClientName { get; init; } = $"{Product.ModId}/{Product.Version}";

    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan WriteTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Delays between connection attempts; the last one repeats (1, 2, 4 ... 30 s).</summary>
    public IReadOnlyList<TimeSpan> Backoff { get; init; } = [.. new[] { 1, 2, 4, 8, 16, 30 }.Select(s => TimeSpan.FromSeconds(s))];

    /// <summary>How long a request may take: the op's budget (admin-protocol.md §4) plus 5 s for the trip.</summary>
    public Func<string, TimeSpan> OpTimeout { get; init; } = op => TimeSpan.FromSeconds(KapOps.BudgetS(op) + 5);

    /// <summary>How often the keepalive looks at the connection (it pings after limits.pingS without sending and drops a
    /// server silent for limits.idleS).</summary>
    public TimeSpan KeepaliveTick { get; init; } = TimeSpan.FromSeconds(1);
}

/// <summary>The panel's one KAP/1 connection to Kiln.Admin (admin-protocol.md), shared by every web user: the
/// handshake with the admin token (HMAC both ways), requests by id with per-op timeouts and the in-flight cap, topic
/// subscriptions fanned out to any number of local subscribers, pings, and reconnects with backoff. Every request names
/// its <see cref="KapCaller"/>; <see cref="KapOps.Stamp"/> sets source/level/by from it, so no code path can send a
/// command op without them. Thread-safe; all of it runs off the tick thread.</summary>
public sealed class KapClient : IAsyncDisposable
{
    /// <summary>Topics whose newest push a new subscriber gets at once (Kiln.Admin does this for state).</summary>
    private static readonly IReadOnlySet<string> Replayed = new HashSet<string>(StringComparer.Ordinal) { "state" };

    private readonly KapClientOptions _o;
    private readonly CancellationTokenSource _life = new();
    private readonly object _topicsLock = new();
    private readonly Dictionary<string, List<TopicSub>> _topics = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, KapPush> _last = new(StringComparer.Ordinal);
    private Conn? _conn;
    private Task? _loop;
    private long _generation;
    private int _stopping;
    private TaskCompletionSource<KapSession> _ready = NewReady();
    private string? _lastLogged;
    private long _lastLoggedAt;
    private IReadOnlyList<string>? _missing;

    public KapClient(KapClientOptions options)
    {
        _o = options;
    }

    /// <summary>The live session, or null while not connected.</summary>
    public KapSession? Session => Volatile.Read(ref _conn) is { Closed: false } c ? c.Session : null;

    public bool IsReady => Session is not null;

    /// <summary>A connection became ready (any thread). Handlers must not block.</summary>
    public event Action<KapSession>? Connected;

    /// <summary>The ready connection was lost; the reason (any thread). Handlers must not block.</summary>
    public event Action<string>? Disconnected;

    /// <summary>Kiln.Admin lacks what the mod needs (an older Kiln): what is missing. The client then stays
    /// disconnected for good (no retry); the panel refuses to serve (AuthComponent).</summary>
    public event Action<IReadOnlyList<string>>? Incompatible;

    /// <summary>What Kiln.Admin lacked at the last check, or null (not checked yet, or everything is there).</summary>
    public IReadOnlyList<string>? MissingCapabilities => Volatile.Read(ref _missing);

    /// <summary>The ops the mod needs from Kiln.Admin 0.2.0-dev.30.9 (admin-protocol.md §11), checked in ready.ops at
    /// every connect, in this order.</summary>
    public static readonly IReadOnlyList<string> RequiredOps =
    [
        "login_mint", "login_redeem", "server_restart", "server_restart_cancel", "update_list", "update_stage", "update_cancel", "update_rollback",
        "command_run", "command_complete", "commands_describe",
    ];

    /// <summary>The topics the mod needs (ready.topics).</summary>
    public static readonly IReadOnlyList<string> RequiredTopics = ["metrics"];

    /// <summary>The name of the command-source capability in <see cref="MissingCapabilities"/>.</summary>
    public const string CommandLevelsCapability = "command_run source and level";

    /// <summary>Starts the connect loop on a pool thread and returns at once.</summary>
    public void Start()
    {
        if (_loop is not null) throw new InvalidOperationException("already started");
        _loop = Task.Run(() => RunAsync(_life.Token));
    }

    /// <summary>Waits until a connection is ready; null after <paramref name="timeout"/>.</summary>
    public async Task<KapSession?> WaitReadyAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        if (Session is { } s) return s;
        try
        {
            return await Volatile.Read(ref _ready).Task.WaitAsync(timeout, ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    /// <summary>Runs <paramref name="op"/> for <paramref name="caller"/>. Never throws for the op's own failures: a refused
    /// call (<see cref="KapOps.Stamp"/>), no connection, busy, timeout and Kiln.Admin's errors all come back as a failed
    /// <see cref="KapResult"/>. Throws OperationCanceledException only when <paramref name="ct"/> is cancelled.</summary>
    public async Task<KapResult> CallAsync(KapCaller caller, string op, JsonObject? args = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        JsonObject stamped;
        try
        {
            stamped = KapOps.Stamp(caller, op, args);
        }
        catch (KapRefusedException e)
        {
            return KapResult.Fail(op, "refused", e.Message);
        }
        var c = Volatile.Read(ref _conn);
        if (c is null || c.Closed) return KapResult.Unavailable(op);
        if (!c.Session.Ops.Contains(op)) return KapResult.Fail(op, "unknown_op", $"unknown op {op}");
        if (!c.Inflight.Wait(0)) return KapResult.Fail(op, "busy", "too many KAP requests in flight");
        var id = c.NextId();
        var pending = new Pending(op);
        c.Pending[id] = pending;
        try
        {
            try
            {
                await SendAsync(c, new JsonObject { ["t"] = "req", ["id"] = id, ["op"] = op, ["args"] = stamped }).ConfigureAwait(false);
            }
            catch (KapProtocolException e) when (e.Code == "too_large")
            {
                return KapResult.Fail(op, "too_large", e.Message);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                return KapResult.Unavailable(op);
            }
            var limit = _o.OpTimeout(op);
            try
            {
                return await pending.Result.Task.WaitAsync(limit, ct).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return KapResult.Fail(op, "timeout", $"{op} did not answer within {limit.TotalSeconds:F0} s");
            }
        }
        finally
        {
            c.Pending.TryRemove(id, out _);
            c.Inflight.Release();
        }
    }

    /// <summary>Subscribes <paramref name="onPush"/> to <paramref name="topic"/> (Kiln.Admin is asked once, whatever the
    /// number of local subscribers, and again after a reconnect). <paramref name="onPush"/> runs on the reader's thread:
    /// it must not block (queue the push). Dispose to unsubscribe.</summary>
    public IDisposable Subscribe(string topic, Action<KapPush> onPush)
    {
        var sub = new TopicSub(this, topic, onPush);
        lock (_topicsLock)
        {
            if (!_topics.TryGetValue(topic, out var list)) _topics[topic] = list = [];
            list.Add(sub);
        }
        if (Volatile.Read(ref _conn) is { Closed: false } c) _ = EnsureSubscribedAsync(c, topic);
        return sub;
    }

    /// <summary>The newest push of a replayed topic (state) on the current connection, for a new subscriber.</summary>
    public KapPush? LastPush(string topic) => Replayed.Contains(topic) && _last.TryGetValue(topic, out var p) ? p : null;

    /// <summary>Local subscribers per topic (tests, the status route).</summary>
    public IReadOnlyDictionary<string, int> SubscriberCounts()
    {
        lock (_topicsLock) return _topics.ToDictionary(kv => kv.Key, kv => kv.Value.Count);
    }

    /// <summary>Says bye, closes the connection and ends the connect loop; bounded by <paramref name="ct"/>.</summary>
    public async Task StopAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _stopping, 1) != 0) return;
        // bye first: cancelling the life token closes the connection at once
        if (Volatile.Read(ref _conn) is { Closed: false } c)
        {
            try
            {
                using var t = CancellationTokenSource.CreateLinkedTokenSource(ct);
                t.CancelAfter(300);
                await SendAsync(c, new JsonObject { ["t"] = "bye", ["reason"] = "unload" }, t.Token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // closing anyway
            }
        }
        _life.Cancel();
        if (Volatile.Read(ref _conn) is { } open) Close(open, "stopping");
        if (_loop is { } loop)
        {
            try
            {
                await loop.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // cancelled or already logged
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        using var t = new CancellationTokenSource(1000);
        await StopAsync(t.Token).ConfigureAwait(false);
        _life.Dispose();
    }

    // ---------------------------------------------------------------------------------------------- connection loop

    private async Task RunAsync(CancellationToken ct)
    {
        var attempt = 0;
        while (!ct.IsCancellationRequested)
        {
            Conn? c = null;
            var since = Stopwatch.GetTimestamp();
            try
            {
                c = await ConnectAsync(ct).ConfigureAwait(false);
                // a stop closes the live connection; the registration ends with it (no leak per reconnect)
                using (ct.Register(static o => Close((Conn)o!, "stopping"), c))
                {
                    var missing = await MissingAsync(c).ConfigureAwait(false);
                    if (missing.Count > 0)
                    {
                        Refuse(c, missing);
                        return;
                    }
                    Publish(c);
                    await c.Reader.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                LogThrottled(e is KapUnavailableException or KapProtocolException ? e.Message : $"{e.GetType().Name}: {e.Message}");
            }
            finally
            {
                if (c is not null) Unpublish(c);
            }
            if (ct.IsCancellationRequested) break;
            if (c is not null && Stopwatch.GetElapsedTime(since) > TimeSpan.FromSeconds(30)) attempt = 0;
            var delay = _o.Backoff[Math.Min(attempt++, _o.Backoff.Count - 1)];
            try
            {
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>What this Kiln.Admin lacks: the required ops and topics, and the command source/level support. That
    /// one is probed: commands_describe with a source and a level no Kiln.Admin knows; a 30.9 one refuses the level
    /// ("level: everyone, admin or console"), an older one ignores both and answers. A read-only op, no line runs.
    /// Throws (KapUnavailableException) when the probe gets no answer: the connection is retried as usual.</summary>
    private async Task<List<string>> MissingAsync(Conn c)
    {
        var s = c.Session;
        var missing = RequiredOps.Where(op => !s.Ops.Contains(op)).ToList();
        missing.AddRange(RequiredTopics.Where(t => !s.Topics.Contains(t)).Select(t => "topic " + t));
        if (s.Ops.Contains("commands_describe"))
        {
            var r = await RawOpAsync(c, "commands_describe", new JsonObject { ["source"] = "web:" + Product.ModId, ["level"] = "level-probe" }).ConfigureAwait(false);
            if (r.Code is "shutting_down" or "timeout" or "busy") throw new KapUnavailableException($"the capability probe got no answer ({r.Code})");
            var refusedLevel = !r.Ok && r.Error is { } e && e.Contains("level", StringComparison.OrdinalIgnoreCase);
            if (!refusedLevel) missing.Add(CommandLevelsCapability);
        }
        else if (!missing.Contains("commands_describe")) missing.Add(CommandLevelsCapability);
        if (missing.Remove("command_run") | missing.Remove("command_complete") | missing.Remove("commands_describe") && !missing.Contains(CommandLevelsCapability))
            missing.Add(CommandLevelsCapability);
        return missing;
    }

    private void Refuse(Conn c, List<string> missing)
    {
        Volatile.Write(ref _missing, missing);
        _o.Log.Error($"{Product.Name} needs Kiln {Product.KilnFloor} or newer (Kiln.Admin is missing: {string.Join(", ", missing)})");
        _ = SendQuietlyAsync(c, new JsonObject { ["t"] = "bye", ["reason"] = "incompatible" });
        // give the bye a moment, then close; the loop ends here: no retry until the mod reloads (a Kiln update restarts
        // the server anyway)
        _ = Task.Delay(200).ContinueWith(_ => Close(c, "Kiln.Admin is too old"), TaskScheduler.Default);
        Raise(() => Incompatible?.Invoke(missing));
    }

    /// <summary>One request straight on a connection that is not published yet (the capability probe).</summary>
    private async Task<KapResult> RawOpAsync(Conn c, string op, JsonObject args)
    {
        var id = c.NextId();
        var pending = new Pending(op);
        c.Pending[id] = pending;
        try
        {
            await SendAsync(c, new JsonObject { ["t"] = "req", ["id"] = id, ["op"] = op, ["args"] = args }).ConfigureAwait(false);
            return await pending.Result.Task.WaitAsync(_o.OpTimeout(op)).ConfigureAwait(false);
        }
        catch (Exception e) when (e is TimeoutException or IOException or SocketException or ObjectDisposedException or OperationCanceledException)
        {
            return KapResult.Fail(op, "timeout", e.Message);
        }
        finally
        {
            c.Pending.TryRemove(id, out _);
        }
    }

    private void LogThrottled(string message)
    {
        var now = Environment.TickCount64;
        if (message == _lastLogged && now - _lastLoggedAt < 60_000) return;
        _lastLogged = message;
        _lastLoggedAt = now;
        _o.Log.Warn($"KAP: cannot reach Kiln.Admin: {message} (retrying)");
    }

    private void Publish(Conn c)
    {
        _last.Clear();
        Volatile.Write(ref _conn, c);
        _lastLogged = null;
        var s = c.Session;
        _o.Log.Info($"KAP: connected to Kiln.Admin {s.Admin ?? "?"} (kiln {s.Kiln ?? "?"}) on {c.Endpoint}: {s.Ops.Count} ops, topics {string.Join(", ", s.Topics)}");
        Interlocked.Exchange(ref _ready, NewReady()).TrySetResult(s);
        List<string> topics;
        lock (_topicsLock) topics = [.. _topics.Keys];
        foreach (var t in topics) _ = EnsureSubscribedAsync(c, t);
        c.StartKeepalive(this);
        Raise(() => Connected?.Invoke(s));
    }

    private void Unpublish(Conn c)
    {
        var wasReady = c.Published;
        Close(c, c.CloseReason ?? "closed");
        Interlocked.CompareExchange(ref _conn, null, c);
        if (wasReady)
        {
            _last.Clear();
            if (!_life.IsCancellationRequested) _o.Log.Warn($"KAP: connection to Kiln.Admin lost ({c.CloseReason}); reconnecting");
            Raise(() => Disconnected?.Invoke(c.CloseReason ?? "closed"));
        }
    }

    private void Raise(Action a)
    {
        try
        {
            a();
        }
        catch (Exception e)
        {
            _o.Log.Error("KAP: a listener failed", e);
        }
    }

    private static TaskCompletionSource<KapSession> NewReady() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    // ---------------------------------------------------------------------------------------------- handshake

    private async Task<Conn> ConnectAsync(CancellationToken ct)
    {
        var ep = KapEndpoint.Read(_o.AdminDirectory);
        var tcp = new TcpClient(ep.AddressFamily) { NoDelay = true };
        try
        {
            using (var t = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                t.CancelAfter(_o.ConnectTimeout);
                try
                {
                    await tcp.ConnectAsync(ep, t.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    throw new KapUnavailableException($"connecting to {ep} timed out");
                }
                catch (SocketException e)
                {
                    throw new KapUnavailableException($"{ep}: {e.SocketErrorCode}");
                }
            }
            var stream = tcp.GetStream();
            JsonObject hello, ready;
            using (var t = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                t.CancelAfter(_o.HandshakeTimeout);
                try
                {
                    (hello, ready) = await HandshakeAsync(stream, t.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    throw new KapUnavailableException("the KAP handshake timed out");
                }
                catch (Exception e) when (e is IOException or EndOfStreamException or SocketException)
                {
                    throw new KapUnavailableException($"the KAP handshake failed: {e.Message}");
                }
            }
            var ops = ready["ops"] is JsonArray oa ? oa.Select(n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).Where(KapOps.IsOpName).Select(s => s!) : [];
            var topics = ready["topics"] is JsonArray ta ? ta.Select(n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).Where(KapOps.IsOpName).Select(s => s!) : [];
            var session = new KapSession
            {
                Hello = hello, Ready = ready, Ops = new HashSet<string>(ops, StringComparer.Ordinal), Topics = [.. topics.Distinct()],
                Limits = KapLimits.From(ready["limits"] as JsonObject), Generation = Interlocked.Increment(ref _generation),
            };
            var c = new Conn(tcp, stream, session, ep, _o);
            c.Reader = Task.Run(() => ReadLoopAsync(c));
            return c;
        }
        catch
        {
            tcp.Dispose();
            throw;
        }
    }

    /// <summary>hello -> auth -> ready, checking the server's proof of the token (admin-protocol.md §3).</summary>
    private async Task<(JsonObject Hello, JsonObject Ready)> HandshakeAsync(NetworkStream stream, CancellationToken ct)
    {
        var hello = await ReadFrameAsync(stream, 64 << 10, ct).ConfigureAwait(false);
        if (KapFrames.Str(hello, "t") == "error") throw new KapUnavailableException($"Kiln.Admin refused: {KapFrames.Str(hello, "code")} {KapFrames.Str(hello, "message")}");
        if (KapFrames.Str(hello, "t") != "hello") throw new KapProtocolException("bad_frame", "the server did not say hello");
        if (KapFrames.Str(hello, "proto") != "kap/1") throw new KapProtocolException("version", $"the server speaks {KapFrames.Str(hello, "proto")}, this client kap/1");
        var nonce = KapFrames.Str(hello, "nonce");
        if (nonce is null || !IsHex64(nonce)) throw new KapProtocolException("bad_frame", "hello has no valid nonce");
        if (hello["auth"] is JsonArray auth && !auth.Any(a => a is JsonValue v && v.TryGetValue<string>(out var s) && s == "hmac-sha256"))
            throw new KapProtocolException("version", "the server offers no hmac-sha256 authentication");
        var token = KapEndpoint.ReadToken(_o.AdminDirectory);
        byte[] expected;
        try
        {
            var cnonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            var mac = Mac(token, "kap1-auth", nonce, cnonce);
            expected = Mac(token, "kap1-ready", nonce, cnonce);
            await WriteRawAsync(stream, KapFrames.Encode(new JsonObject
            {
                ["t"] = "auth", ["proto"] = "kap/1", ["cnonce"] = cnonce, ["mac"] = Convert.ToHexStringLower(mac), ["client"] = _o.ClientName,
            }), ct).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(token);
        }
        var ready = await ReadFrameAsync(stream, KapFrames.MaxClientFrame, ct).ConfigureAwait(false);
        if (KapFrames.Str(ready, "t") == "error")
            throw new KapUnavailableException($"Kiln.Admin refused the handshake: {KapFrames.Str(ready, "code")} {KapFrames.Str(ready, "message")}");
        if (KapFrames.Str(ready, "t") != "ready") throw new KapProtocolException("bad_frame", "the server did not say ready");
        var proof = KapFrames.Str(ready, "mac");
        if (proof is null || !IsHex64(proof) || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(proof), expected))
            throw new KapProtocolException("auth_failed", "the server's ready mac is wrong (not the Kiln.Admin that wrote the token)");
        if (ready["ops"] is not JsonArray || ready["topics"] is not JsonArray) throw new KapProtocolException("bad_frame", "ready has no ops or topics");
        return (hello, ready);
    }

    internal static byte[] Mac(byte[] token, string label, string nonce, string cnonce) =>
        HMACSHA256.HashData(token, Encoding.ASCII.GetBytes(label + "\n" + nonce + "\n" + cnonce));

    private static bool IsHex64(string s) => s.Length == 64 && s.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static async Task<JsonObject> ReadFrameAsync(Stream s, int max, CancellationToken ct)
    {
        var head = new byte[4];
        await s.ReadExactlyAsync(head, ct).ConfigureAwait(false);
        var payload = new byte[KapFrames.Length(head, max)];
        await s.ReadExactlyAsync(payload, ct).ConfigureAwait(false);
        return KapFrames.Decode(payload);
    }

    private static async Task WriteRawAsync(Stream s, byte[] frame, CancellationToken ct)
    {
        await s.WriteAsync(frame, ct).ConfigureAwait(false);
        await s.FlushAsync(ct).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------------------------------------- reading

    private async Task ReadLoopAsync(Conn c)
    {
        var head = new byte[4];
        try
        {
            while (!c.Closed)
            {
                await c.Stream.ReadExactlyAsync(head, c.Token).ConfigureAwait(false);
                var payload = new byte[KapFrames.Length(head, c.Session.Limits.MaxFrame)];
                await c.Stream.ReadExactlyAsync(payload, c.Token).ConfigureAwait(false);
                c.Received();
                Dispatch(c, KapFrames.Decode(payload), payload);
            }
        }
        catch (KapProtocolException e)
        {
            Close(c, $"protocol error from Kiln.Admin: {e.Code} {e.Message}");
        }
        catch (Exception e) when (e is IOException or EndOfStreamException or SocketException or ObjectDisposedException or OperationCanceledException)
        {
            Close(c, c.CloseReason ?? "the connection closed");
        }
        catch (Exception e)
        {
            _o.Log.Error("KAP: reader failed", e);
            Close(c, "reader failed");
        }
    }

    private void Dispatch(Conn c, JsonObject f, byte[] payload)
    {
        switch (KapFrames.Str(f, "t"))
        {
            case "res":
                if (KapFrames.Id(f) is { } id && c.Pending.TryRemove(id, out var p)) p.Result.TrySetResult(KapResult.FromFrame(f));
                break;
            case "push":
                if (KapFrames.Str(f, "topic") is { } topic && KapFrames.Id(f, "seq") is { } seq) Fan(new KapPush(topic, seq, payload));
                break;
            case "ping":
                var pong = new JsonObject { ["t"] = "pong", ["id"] = f["id"]?.DeepClone() ?? JsonValue.Create(0) };
                if (f["ts"] is { } ts) pong["ts"] = ts.DeepClone();
                _ = SendQuietlyAsync(c, pong);
                break;
            case "bye":
                Close(c, $"bye from Kiln.Admin ({KapFrames.Str(f, "reason") ?? "-"})");
                break;
            case "error":
                var code = KapFrames.Str(f, "code") ?? "?";
                if (KapFrames.Id(f) is { } eid && c.Pending.TryRemove(eid, out var ep))
                    ep.Result.TrySetResult(KapResult.Fail(ep.Op, code, KapFrames.Str(f, "message") ?? code));
                else _o.Log.Warn($"KAP: error from Kiln.Admin: {code} {KapFrames.Str(f, "message")}");
                break;
            default:
                break; // pong, and frame types of a newer kap/1
        }
    }

    private void Fan(KapPush push)
    {
        if (Replayed.Contains(push.Topic)) _last[push.Topic] = push;
        TopicSub[] subs;
        lock (_topicsLock) subs = _topics.TryGetValue(push.Topic, out var l) ? [.. l] : [];
        foreach (var s in subs)
        {
            try
            {
                s.OnPush(push);
            }
            catch (Exception e)
            {
                _o.Log.Error($"KAP: a {push.Topic} subscriber failed", e);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------- topics

    private async Task EnsureSubscribedAsync(Conn c, string topic)
    {
        if (!c.Session.Topics.Contains(topic)) return;
        lock (c.Subscribed)
            if (!c.Subscribed.Add(topic)) return;
        var r = await RawRequestAsync(c, "sub", new JsonObject { ["topic"] = topic }).ConfigureAwait(false);
        if (!r.Ok)
        {
            lock (c.Subscribed) c.Subscribed.Remove(topic);
            if (!c.Closed) _o.Log.Warn($"KAP: subscribing to {topic} failed: {r.Code} {r.Error}");
        }
    }

    private void Unsubscribe(TopicSub sub)
    {
        bool last;
        lock (_topicsLock)
        {
            if (!_topics.TryGetValue(sub.Topic, out var list) || !list.Remove(sub)) return;
            last = list.Count == 0;
            if (last) _topics.Remove(sub.Topic);
        }
        if (!last || Volatile.Read(ref _conn) is not { Closed: false } c) return;
        lock (c.Subscribed)
            if (!c.Subscribed.Remove(sub.Topic)) return;
        _ = RawRequestAsync(c, "unsub", new JsonObject { ["topic"] = sub.Topic });
    }

    /// <summary>sub/unsub: a request frame of its own type (not an op), answered by res.</summary>
    private async Task<KapResult> RawRequestAsync(Conn c, string type, JsonObject fields)
    {
        var id = c.NextId();
        var pending = new Pending(type);
        c.Pending[id] = pending;
        try
        {
            fields["t"] = type;
            fields["id"] = id;
            await SendAsync(c, fields).ConfigureAwait(false);
            return await pending.Result.Task.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return KapResult.Unavailable(type);
        }
        finally
        {
            c.Pending.TryRemove(id, out _);
        }
    }

    // ---------------------------------------------------------------------------------------------- writing, closing

    private async Task SendAsync(Conn c, JsonObject frame, CancellationToken ct = default)
    {
        var bytes = KapFrames.Encode(frame, c.Session.Limits.MaxClientFrame);
        using var t = CancellationTokenSource.CreateLinkedTokenSource(ct, c.Token);
        t.CancelAfter(_o.WriteTimeout);
        await c.WriteLock.WaitAsync(t.Token).ConfigureAwait(false);
        try
        {
            await WriteRawAsync(c.Stream, bytes, t.Token).ConfigureAwait(false);
            c.Sent();
        }
        catch (Exception e) when (e is IOException or SocketException or ObjectDisposedException or OperationCanceledException)
        {
            if (!ct.IsCancellationRequested) Close(c, $"writing to Kiln.Admin failed: {e.Message}");
            throw;
        }
        finally
        {
            c.WriteLock.Release();
        }
    }

    private async Task SendQuietlyAsync(Conn c, JsonObject frame)
    {
        try
        {
            await SendAsync(c, frame).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // the connection is closing
        }
    }

    private static void Close(Conn c, string reason)
    {
        if (!c.TryClose(reason)) return;
        foreach (var id in c.Pending.Keys.ToArray())
            if (c.Pending.TryRemove(id, out var p)) p.Result.TrySetResult(KapResult.Unavailable(p.Op));
    }

    private sealed class Pending(string op)
    {
        public readonly string Op = op;
        public readonly TaskCompletionSource<KapResult> Result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class TopicSub(KapClient owner, string topic, Action<KapPush> onPush) : IDisposable
    {
        public readonly string Topic = topic;
        public readonly Action<KapPush> OnPush = onPush;
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) owner.Unsubscribe(this);
        }
    }

    /// <summary>One TCP connection after a good handshake.</summary>
    private sealed class Conn
    {
        private readonly TcpClient _tcp;
        private readonly CancellationTokenSource _cts;
        private readonly KapClientOptions _o;
        private long _nextId;
        private long _lastSent = Environment.TickCount64, _lastRecv = Environment.TickCount64;
        private int _closed;

        public Conn(TcpClient tcp, NetworkStream stream, KapSession session, IPEndPoint endpoint, KapClientOptions o)
        {
            _tcp = tcp;
            Stream = stream;
            Session = session;
            Endpoint = endpoint;
            _o = o;
            _cts = new CancellationTokenSource();
            Inflight = new SemaphoreSlim(session.Limits.Inflight, session.Limits.Inflight);
        }

        public NetworkStream Stream { get; }
        public KapSession Session { get; }
        public IPEndPoint Endpoint { get; }
        public SemaphoreSlim WriteLock { get; } = new(1, 1);
        public SemaphoreSlim Inflight { get; }
        public ConcurrentDictionary<ulong, Pending> Pending { get; } = new();
        public HashSet<string> Subscribed { get; } = new(StringComparer.Ordinal);
        public Task Reader { get; set; } = Task.CompletedTask;
        public bool Closed => Volatile.Read(ref _closed) != 0;
        public bool Published { get; private set; }
        public string? CloseReason { get; private set; }
        public CancellationToken Token => _cts.Token;

        public ulong NextId() => (ulong)Interlocked.Increment(ref _nextId);
        public void Sent() => Volatile.Write(ref _lastSent, Environment.TickCount64);
        public void Received() => Volatile.Write(ref _lastRecv, Environment.TickCount64);

        public bool TryClose(string reason)
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0) return false;
            CloseReason = reason;
            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
            try
            {
                _tcp.Client.Shutdown(SocketShutdown.Both);
            }
            catch (Exception)
            {
                // closed already
            }
            _tcp.Dispose();
            return true;
        }

        /// <summary>Pings after limits.pingS without sending; drops a server silent for limits.idleS.</summary>
        public void StartKeepalive(KapClient client)
        {
            Published = true;
            _ = Task.Run(async () =>
            {
                using var timer = new PeriodicTimer(_o.KeepaliveTick);
                try
                {
                    while (!Closed && await timer.WaitForNextTickAsync(Token).ConfigureAwait(false))
                    {
                        var now = Environment.TickCount64;
                        if (now - Volatile.Read(ref _lastRecv) > Session.Limits.IdleS * 1000L)
                        {
                            KapClient.Close(this, $"Kiln.Admin was silent for {Session.Limits.IdleS} s");
                            break;
                        }
                        if (now - Volatile.Read(ref _lastSent) >= Session.Limits.PingS * 1000L)
                            await client.SendQuietlyAsync(this, new JsonObject { ["t"] = "ping", ["id"] = NextId(), ["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });
        }
    }
}
