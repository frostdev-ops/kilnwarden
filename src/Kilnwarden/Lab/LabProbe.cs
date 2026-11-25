// SPDX-License-Identifier: MIT
// Lab builds only (dotnet build -p:WebAdminLab=true): what the lab checks need and a release must never have.
//  - public test routes under /api/lab (ping, blob, echo, a WebSocket echo that also pushes the metrics line), so
//    tools/load/load.py can drive the panel at a panel's pace without the auth lane;
//  - per-second step timing and process metrics in <data>/lab-metrics.jsonl (tools/load/analyze.py reads it);
//  - optional "online" pacing (a 1 ms server wait, as with players online) when <data>/lab-pacing-online exists.
#if LAB
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Kilnwarden.Hosting;
using Kilnwarden.Http;
using Kilnwarden.Modules;
using Kilnwarden.Routing;
using Kilnwarden.Server;

namespace Kilnwarden.Lab;

internal sealed class LabProbe : IWebAdminComponent, IDisposable
{
    private readonly string _dataDir;
    private readonly ConcurrentQueue<(double[] Intervals, double[] Durations)> _windows = new();
    private List<double> _intervals = new(128), _durations = new(128);
    private long _windowStart = Stopwatch.GetTimestamp(), _preTs;
    private ulong _lastStep;
    private Timer? _timer;
    private StreamWriter? _out;
    private readonly object _gate = new();
    private string _latest = "{}";
    private TimeSpan _lastCpu;
    private long _lastTs;
    private Func<ServerCounters?>? _counters;

    public LabProbe(string dataDir) => _dataDir = dataDir;

    public string Name => "lab";
    public bool OnlinePacing => File.Exists(Path.Combine(_dataDir, "lab-pacing-online"));

    public void Configure(WebAdminBuilder b)
    {
        b.Log.Warn("LAB BUILD: public test routes under /api/lab; never ship this build");
        var m = b.Modules.Register(new ModuleSpec { Id = "lab", Title = "Lab", CanDisable = false }, Product.ModId);
        m.MapGet("/ping", RouteAccess.Public, ctx => ValueTask.FromResult(HttpResponse.Json(new { ok = true, tls = ctx.Connection.Tls })));
        m.MapGet("/blob", RouteAccess.Public, ctx =>
        {
            var n = Math.Clamp(int.TryParse(ctx.Request.QueryValues.GetValueOrDefault("n"), out var v) ? v : 16384, 0, 4 << 20);
            var body = new byte[n];
            body.AsSpan().Fill((byte)'x');
            return ValueTask.FromResult(HttpResponse.Bytes(200, "application/octet-stream", body));
        });
        m.MapPost("/echo", RouteAccess.Public, ctx => ValueTask.FromResult(HttpResponse.Bytes(200, "application/octet-stream", ctx.Request.Body)));
        m.MapGet("/metrics", RouteAccess.Public, _ => ValueTask.FromResult(HttpResponse.Bytes(200, "application/json", Encoding.UTF8.GetBytes(Volatile.Read(ref _latest)))));
        m.MapWebSocket("/ws", RouteAccess.Public, async ws =>
        {
            var push = int.TryParse(ws.Request.Request.QueryValues.GetValueOrDefault("push"), out var p) && p > 0 ? p : 0;
            var pusher = push > 0 ? Task.Run(async () =>
            {
                try
                {
                    while (ws.Socket.State == WebSocketState.Open)
                    {
                        await Task.Delay(push, ws.Stopping);
                        await ws.SendAsync(Encoding.UTF8.GetBytes(Volatile.Read(ref _latest)), WebSocketMessageType.Text, ws.Stopping);
                    }
                }
                catch (Exception e) when (e is OperationCanceledException or WebSocketException or IOException or ObjectDisposedException)
                {
                }
            }) : Task.CompletedTask;
            while (await ws.ReceiveAsync() is { } msg) await ws.SendAsync(msg.Data, msg.Type);
            await Task.WhenAny(pusher, Task.Delay(1000));
        });
        var services = b.Services;
        _counters = () => services.TryGet<ServerCounters>();
    }

    public Task StartAsync(WebAdminServices services, CancellationToken ct)
    {
        _out = new StreamWriter(new FileStream(Path.Combine(_dataDir, "lab-metrics.jsonl"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
        _lastCpu = Process.GetCurrentProcess().TotalProcessorTime;
        _lastTs = Stopwatch.GetTimestamp();
        _timer = new Timer(_ => Sample(), null, 1000, 1000);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Tick thread, Tick.Pre.</summary>
    public void Pre(ulong step, double wallDeltaMs)
    {
        var now = Stopwatch.GetTimestamp();
        if (Stopwatch.GetElapsedTime(_windowStart, now).TotalMilliseconds >= 1000)
        {
            _windows.Enqueue((_intervals.ToArray(), _durations.ToArray()));
            _intervals = new List<double>(128);
            _durations = new List<double>(128);
            _windowStart = now;
            while (_windows.Count > 120) _windows.TryDequeue(out _);
        }
        if (_lastStep != 0 && step == _lastStep + 1) _intervals.Add(wallDeltaMs);
        _lastStep = step;
        _preTs = now;
    }

    /// <summary>Tick thread, Tick.Post.</summary>
    public void Post()
    {
        if (_preTs != 0) _durations.Add(Stopwatch.GetElapsedTime(_preTs).TotalMilliseconds);
    }

    private void Sample()
    {
        try
        {
            var now = Stopwatch.GetTimestamp();
            using var p = Process.GetCurrentProcess();
            var cpu = p.TotalProcessorTime;
            var wallMs = Stopwatch.GetElapsedTime(_lastTs, now).TotalMilliseconds;
            var cpuMs = (cpu - _lastCpu).TotalMilliseconds;
            _lastCpu = cpu;
            _lastTs = now;
            var intervals = new List<double>();
            var durations = new List<double>();
            while (_windows.TryDequeue(out var w))
            {
                intervals.AddRange(w.Intervals);
                durations.AddRange(w.Durations);
            }
            var c = _counters?.Invoke();
            var d = new Dictionary<string, object?>
            {
                ["t"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
                ["cpuPct"] = Math.Round(cpuMs / wallMs * 100, 1),
                ["workingSetMB"] = Math.Round(p.WorkingSet64 / 1048576.0, 1),
                ["threads"] = p.Threads.Count,
                ["gcHeapMB"] = Math.Round(GC.GetTotalMemory(false) / 1048576.0, 2),
                ["gcCommittedMB"] = Math.Round(GC.GetGCMemoryInfo().TotalCommittedBytes / 1048576.0, 1),
                ["gcAllocatedMB"] = Math.Round(GC.GetTotalAllocatedBytes() / 1048576.0, 1),
                ["gc0"] = GC.CollectionCount(0),
                ["gc1"] = GC.CollectionCount(1),
                ["gc2"] = GC.CollectionCount(2),
                ["gcPauseMs"] = Math.Round(GC.GetTotalPauseDuration().TotalMilliseconds, 2),
                ["poolThreads"] = ThreadPool.ThreadCount,
                ["accepted"] = c?.Accepted, ["requests"] = c?.Requests, ["webSockets"] = c?.WebSockets, ["refused"] = c?.Refused,
                ["handshakesOk"] = c?.HandshakesOk, ["handshakesFailed"] = c?.HandshakesFailed, ["errors"] = c?.Errors, ["rateLimited"] = c?.RateLimited,
                ["steps"] = intervals.Count,
                ["interval"] = Summary(intervals),
                ["step"] = Summary(durations),
            };
            var line = JsonSerializer.Serialize(d);
            Volatile.Write(ref _latest, line);
            lock (_gate)
            {
                _out?.WriteLine(line);
                _out?.Flush();
            }
        }
        catch (Exception)
        {
            // a lab probe never takes the server down
        }
    }

    private static Dictionary<string, object?>? Summary(List<double> v)
    {
        if (v.Count == 0) return null;
        v.Sort();
        double P(double q) => v[Math.Min(v.Count - 1, (int)Math.Ceiling(q * v.Count) - 1)];
        return new() { ["n"] = v.Count, ["mean"] = Math.Round(v.Average(), 3), ["p50"] = Math.Round(P(.5), 3), ["p95"] = Math.Round(P(.95), 3),
            ["p99"] = Math.Round(P(.99), 3), ["max"] = Math.Round(v[^1], 3) };
    }

    public void Dispose()
    {
        if (_timer is not null)
        {
            using var done = new ManualResetEvent(false);
            _timer.Dispose(done);
            done.WaitOne(2000);
            _timer = null;
        }
        lock (_gate)
        {
            _out?.Dispose();
            _out = null;
        }
    }
}
#endif
