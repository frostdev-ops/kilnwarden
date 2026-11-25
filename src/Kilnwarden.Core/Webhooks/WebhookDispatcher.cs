// SPDX-License-Identifier: MIT
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Kilnwarden.Events;
using Kilnwarden.Hosting;

namespace Kilnwarden.Webhooks;

/// <summary>Sends webhooks off the caller's thread: one bounded queue (256, the oldest dropped), one sender, at most
/// &lt;see cref="PerMinute"/&gt; sends a minute per hook (more are dropped and logged as rate_limited), every outcome in a
/// ring of the last 100 (no URL, no body).</summary>
public sealed class WebhookDispatcher : IAsyncDisposable
{
    public const int QueueSize = 256, PerMinute = 20, Kept = 100;

    private readonly IWebhookTransport _transport;
    private readonly TimeProvider _time;
    private readonly IWebAdminLog _log;
    private readonly Func<string> _serverName;
    private readonly Func<bool> _allowPrivate;
    private readonly Channel<Item> _queue = Channel.CreateBounded<Item>(new BoundedChannelOptions(QueueSize)
    {
        FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true,
    });
    private readonly Dictionary<string, Queue<long>> _sent = new(StringComparer.Ordinal);
    private readonly LinkedList<JsonObject> _deliveries = new();
    private readonly CancellationTokenSource _stop = new();
    private Task? _pump;
    private long _sentCount;

    public WebhookDispatcher(IWebhookTransport transport, TimeProvider time, IWebAdminLog log, Func<string> serverName, Func<bool>? allowPrivate = null)
    {
        _allowPrivate = allowPrivate ?? (() => false);
        _transport = transport;
        _time = time;
        _log = log;
        _serverName = serverName;
    }

    private sealed record Item(WebhookHook Hook, PanelEvent Event, EventKind Kind);

    public long SentCount => Interlocked.Read(ref _sentCount);

    public void Start() => _pump ??= Task.Run(PumpAsync);

    public void Enqueue(WebhookHook hook, PanelEvent e, EventKind kind) => _queue.Writer.TryWrite(new Item(hook, e, kind));

    public JsonArray Deliveries()
    {
        lock (_deliveries) return new JsonArray([.. _deliveries.Reverse().Select(d => (JsonNode)d.DeepClone())]);
    }

    private async Task PumpAsync()
    {
        try
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(_stop.Token).ConfigureAwait(false)) await SendOneAsync(item).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private bool Allow(string hookId, long now)
    {
        if (!_sent.TryGetValue(hookId, out var q)) _sent[hookId] = q = new Queue<long>();
        while (q.Count > 0 && now - q.Peek() >= 60_000) q.Dequeue();
        if (q.Count >= PerMinute) return false;
        q.Enqueue(now);
        return true;
    }

    internal async Task SendOneAsync(WebhookHook hook, PanelEvent e, EventKind kind) => await SendOneAsync(new Item(hook, e, kind)).ConfigureAwait(false);

    private async Task SendOneAsync(Item item)
    {
        var (hook, e, kind) = item;
        var now = _time.GetUtcNow().ToUnixTimeMilliseconds();
        if (!Allow(hook.Id, now))
        {
            Record(hook, e, false, null, "rate_limited", 0);
            return;
        }
        var sw = Stopwatch.StartNew();
        try
        {
            if (WebhookGuard.CheckUrl(hook.Kind, hook.Url, out var uri, _allowPrivate()) is { } bad) throw new WebhookRefusedException(bad);
            var body = WebhookPayloads.Build(hook.Kind, hook.Template, e, kind, _serverName());
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            cts.CancelAfter(TimeSpan.FromSeconds(12));
            var status = await _transport.SendAsync(new WebhookRequest(uri!, body), cts.Token).ConfigureAwait(false);
            var ok = status is >= 200 and < 300;
            if (ok) Interlocked.Increment(ref _sentCount);
            Record(hook, e, ok, status, ok ? null : status is >= 300 and < 400 ? "redirect_refused" : "http_" + status, sw.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            // never the URL: exception texts can carry the host (fine) but not the token, which only the path holds
            var why = ex switch
            {
                WebhookRefusedException r => "refused: " + r.Message,
                OperationCanceledException => "timeout",
                HttpRequestException h when h.InnerException is WebhookRefusedException r => "refused: " + r.Message,
                HttpRequestException h => "network: " + (h.HttpRequestError.ToString()),
                _ => "failed: " + ex.GetType().Name,
            };
            Record(hook, e, false, null, why, sw.Elapsed.TotalMilliseconds);
        }
    }

    private void Record(WebhookHook hook, PanelEvent e, bool ok, int? status, string? error, double ms)
    {
        var d = new JsonObject
        {
            ["at"] = _time.GetUtcNow().ToString("O"), ["hook"] = hook.Id, ["name"] = hook.Name, ["event"] = e.Topic, ["ok"] = ok, ["status"] = status,
            ["error"] = error, ["ms"] = Math.Round(ms, 1),
        };
        lock (_deliveries)
        {
            _deliveries.AddLast(d);
            while (_deliveries.Count > Kept) _deliveries.RemoveFirst();
        }
        if (!ok) _log.Warn($"webhook {hook.Id} ({hook.Name}) {e.Topic}: {error}");
    }

    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        _stop.Cancel();
        if (_pump is { } p)
        {
            try
            {
                await p.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
            }
        }
        _stop.Dispose();
    }
}
