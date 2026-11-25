// SPDX-License-Identifier: MIT
using System.Net;
using Kilnwarden.Config;

namespace Kilnwarden.Server;

/// <summary>Per-client limits: open connections per IP, new connections per second per IP and requests per second per
/// IP (token buckets). Entries of idle clients are swept, so a scan from many addresses cannot grow it without bound.</summary>
public sealed class ClientLimiter(TimeProvider time)
{
    public const int MaxTracked = 50_000;
    private readonly Dictionary<IPAddress, Entry> _clients = [];
    private readonly object _gate = new();
    private long _lastSweep;

    /// <summary>Admits a new connection from <paramref name="ip"/>; false when it is over its caps. Exempt (trusted
    /// proxies) skip the per-IP caps but still count. Every admitted connection must <see cref="Close"/>.</summary>
    public bool TryOpen(IPAddress ip, LimitSettings limits, bool exempt)
    {
        lock (_gate)
        {
            var e = Get(ip, limits);
            if (!exempt)
            {
                if (e.Open >= limits.MaxConnectionsPerIp) return false;
                if (!Take(ref e.ConnTokens, ref e.ConnStamp, limits.ConnectionsPerSecondPerIp, Math.Max(1, limits.ConnectionsPerSecondPerIp * 2))) return false;
            }
            e.Open++;
            return true;
        }
    }

    public void Close(IPAddress ip)
    {
        lock (_gate)
            if (_clients.TryGetValue(ip, out var e) && e.Open > 0) e.Open--;
    }

    /// <summary>One request from <paramref name="ip"/> (the client address after X-Forwarded-For); false: answer 429.</summary>
    public bool TryRequest(IPAddress ip, LimitSettings limits)
    {
        lock (_gate)
        {
            var e = Get(ip, limits);
            return Take(ref e.ReqTokens, ref e.ReqStamp, limits.RequestsPerSecondPerIp, limits.RequestBurstPerIp);
        }
    }

    public int Tracked
    {
        get
        {
            lock (_gate) return _clients.Count;
        }
    }

    private Entry Get(IPAddress ip, LimitSettings limits)
    {
        var now = time.GetTimestamp();
        if (_clients.Count > 1024 && time.GetElapsedTime(_lastSweep, now) > TimeSpan.FromSeconds(30) || _clients.Count >= MaxTracked) Sweep(now);
        if (!_clients.TryGetValue(ip, out var e))
        {
            e = new Entry { ReqTokens = limits.RequestBurstPerIp, ReqStamp = now, ConnTokens = Math.Max(1, limits.ConnectionsPerSecondPerIp * 2), ConnStamp = now };
            _clients[ip] = e;
        }
        return e;
    }

    private bool Take(ref double tokens, ref long stamp, double rate, double burst)
    {
        var now = time.GetTimestamp();
        tokens = Math.Min(burst, tokens + time.GetElapsedTime(stamp, now).TotalSeconds * rate);
        stamp = now;
        if (tokens < 1) return false;
        tokens -= 1;
        return true;
    }

    private void Sweep(long now)
    {
        _lastSweep = now;
        foreach (var (ip, e) in _clients.ToList())
            if (e.Open == 0 && time.GetElapsedTime(Math.Max(e.ReqStamp, e.ConnStamp), now) > TimeSpan.FromSeconds(60)) _clients.Remove(ip);
        if (_clients.Count >= MaxTracked)
            foreach (var (ip, _) in _clients.Where(kv => kv.Value.Open == 0).Take(_clients.Count / 2).ToList()) _clients.Remove(ip);
    }

    private sealed class Entry
    {
        public int Open;
        public double ReqTokens, ConnTokens;
        public long ReqStamp, ConnStamp;
    }
}
