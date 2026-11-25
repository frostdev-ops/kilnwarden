// SPDX-License-Identifier: MIT
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Kilnwarden.Hosting;
using Kilnwarden.Security;
using Kilnwarden.Storage;

namespace Kilnwarden.Auth;

/// <summary>The audit log (DESIGN §5): every sign-in, failure, account and role change, and every write action and
/// refused call, as JSON Lines in audit/audit.jsonl (8 MiB per file, 10 files kept). <see cref="Record"/> never blocks
/// a request: entries go through a bounded queue to a writer task (a full queue drops and counts; the next written
/// entry says how many were lost).
/// <para>It is the panel's one audit sink: every component records here, directly (auth, users, the KAP socket, the ops
/// lane) or through the <see cref="IAuditSink"/> service (the gate, modules, extensions). Observers such as the webhooks'
/// admin.action feed attach with <see cref="Tap"/> instead of wrapping the sink, so nothing can bypass them, whatever the
/// component order. A tap sees the entry as recorded (arguments already redacted) and must not block or throw.</para></summary>
public sealed class AuditLog : IAuditSink, IAsyncDisposable
{
    public const string File = "audit/audit.jsonl";
    public const int MaxArgsBytes = 4096;

    private readonly JsonlWriter _writer;
    private readonly IWebAdminLog _log;
    private readonly Channel<AuditEntry> _queue = Channel.CreateBounded<AuditEntry>(new BoundedChannelOptions(8192)
    {
        FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true,
    });
    private Task? _pump;
    private long _dropped;
    private Action<AuditEntry>[] _taps = [];

    public AuditLog(DataStore data, IWebAdminLog log)
    {
        _writer = data.OpenJsonl(File, 8 << 20, 10);
        _log = log;
    }

    /// <summary>Starts the writer (the component's start); entries recorded before it wait in the queue.</summary>
    public void Start() => _pump ??= Task.Run(PumpAsync);

    public long Dropped => Interlocked.Read(ref _dropped);

    public void Record(AuditEntry entry)
    {
        if (!_queue.Writer.TryWrite(entry)) Interlocked.Increment(ref _dropped);
        foreach (var tap in Volatile.Read(ref _taps))
        {
            try
            {
                tap(entry);
            }
            catch (Exception ex)
            {
                _log.Error($"audit log: an observer failed on {entry.Op}", ex);
            }
        }
    }

    /// <summary>Attaches an observer of every recorded entry (synchronous, on the recording thread: it must only queue).
    /// Dispose the result to detach (a component's stop; the delegate pins its owner until then).</summary>
    public IDisposable Tap(Action<AuditEntry> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        Action<AuditEntry>[] cur, next;
        do
        {
            cur = Volatile.Read(ref _taps);
            next = [.. cur, observer];
        } while (Interlocked.CompareExchange(ref _taps, next, cur) != cur);
        return new Detach(this, observer);
    }

    /// <summary>Tests and diagnostics: how many observers are attached.</summary>
    public int TapCount => Volatile.Read(ref _taps).Length;

    private void Untap(Action<AuditEntry> observer)
    {
        Action<AuditEntry>[] cur, next;
        do
        {
            cur = Volatile.Read(ref _taps);
            var i = Array.IndexOf(cur, observer);
            if (i < 0) return;
            next = [.. cur[..i], .. cur[(i + 1)..]];
        } while (Interlocked.CompareExchange(ref _taps, next, cur) != cur);
    }

    private sealed class Detach(AuditLog log, Action<AuditEntry> observer) : IDisposable
    {
        private int _done;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0) log.Untap(observer);
        }
    }

    private async Task PumpAsync()
    {
        var failing = false;
        await foreach (var e in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                var line = Line(e);
                var lost = Interlocked.Exchange(ref _dropped, 0);
                if (lost > 0) line["lost"] = lost;
                _writer.Append(line);
                failing = false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
            {
                Interlocked.Increment(ref _dropped);
                if (!failing) _log.Error($"audit log: cannot write {_writer.FilePath}", ex);
                failing = true;
            }
        }
    }

    /// <summary>One entry as its JSON line: {at, user, userName, op, ok, error?, ms, ip, args?}.</summary>
    public static JsonObject Line(AuditEntry e)
    {
        var o = new JsonObject
        {
            ["at"] = e.At.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
            ["user"] = e.User,
            ["userName"] = e.UserName,
            ["op"] = e.Op,
            ["ok"] = e.Ok,
            ["ms"] = Math.Round(e.Ms, 1),
            ["ip"] = e.Client?.ToString(),
        };
        if (e.Error is not null) o["error"] = e.Error.Length > 500 ? e.Error[..500] : e.Error;
        if (e.Args is not null)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(e.Args);
            o["args"] = bytes.Length <= MaxArgsBytes ? JsonNode.Parse(bytes) : new JsonObject { ["truncated"] = true, ["bytes"] = bytes.Length };
        }
        return o;
    }

    /// <summary>Entries newest first, up to <paramref name="limit"/>, older than <paramref name="before"/>, optionally
    /// only one user's or ops starting with a prefix. Reads the files (the newest first) on the caller's thread.</summary>
    public IReadOnlyList<JsonElement> Read(int limit, DateTimeOffset? before = null, string? user = null, string? opPrefix = null)
    {
        var result = new List<JsonElement>(Math.Min(limit, 1000));
        foreach (var file in _writer.Files())
        {
            List<JsonElement> lines;
            try
            {
                lines = [.. JsonlWriter.ReadLines(file)];
            }
            catch (IOException)
            {
                continue;
            }
            for (var i = lines.Count - 1; i >= 0 && result.Count < limit; i--)
            {
                var l = lines[i];
                if (l.ValueKind != JsonValueKind.Object) continue;
                if (before is { } b && (!l.TryGetProperty("at", out var at) || !at.TryGetDateTimeOffset(out var t) || t >= b)) continue;
                if (user is not null && (!l.TryGetProperty("user", out var u) || u.ValueKind != JsonValueKind.String || u.GetString() != user)) continue;
                if (opPrefix is not null && (!l.TryGetProperty("op", out var op) || op.ValueKind != JsonValueKind.String || !op.GetString()!.StartsWith(opPrefix, StringComparison.Ordinal))) continue;
                result.Add(l);
            }
            if (result.Count >= limit) break;
        }
        return result;
    }

    /// <summary>Writes what is queued and closes the file (bounded by <paramref name="ct"/>).</summary>
    public async Task StopAsync(CancellationToken ct)
    {
        _queue.Writer.TryComplete();
        try
        {
            if (_pump is { } pump) await pump.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _log.Warn("audit log: entries still queued at stop were not written");
        }
        _writer.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        using var t = new CancellationTokenSource(1000);
        await StopAsync(t.Token).ConfigureAwait(false);
    }

    /// <summary>Shorthand for an entry about a user (null: anonymous).</summary>
    public static AuditEntry Entry(string op, WebPrincipal? user, IPAddress? ip, bool ok, string? error = null, JsonObject? args = null, double ms = 0,
        string? userName = null) =>
        new(DateTimeOffset.UtcNow, user?.Id ?? "", user?.Name ?? userName ?? "", op, args, ok, error, ms, ip);
}
