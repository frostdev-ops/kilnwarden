// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Kilnwarden.Tls;

namespace Kilnwarden.Server;

/// <summary>One listening socket (step 0's shape): a TcpListener with ExclusiveAddressUse, one blocking accept thread
/// (it unblocks on Stop under Wine), connections running async on the thread pool. The accept thread refuses at once
/// what is over a cap (allowlist, open connections overall and per IP, new connections per IP per second).</summary>
public sealed class HttpServer
{
    private readonly ServerShared _shared;
    private readonly ITlsServer? _tls;
    private readonly IPEndPoint _endpoint;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<long, HttpConnection> _conns = new();
    private readonly object _admissions = new();
    private TcpListener? _listener;
    private Thread? _accept;
    private long _nextId;
    private volatile bool _stopping;

    public HttpServer(string name, IPEndPoint endpoint, ITlsServer? tls, ServerShared shared)
    {
        Name = name;
        _endpoint = endpoint;
        _tls = tls;
        _shared = shared;
    }

    public string Name { get; }
    public bool IsTls => _tls is not null;
    public ITlsServer? Tls => _tls;
    public IPEndPoint LocalEndPoint { get; private set; } = new(IPAddress.None, 0);
    public int Connections => _conns.Count;

    /// <summary>Binds and starts accepting. Throws SocketException when the address is taken.</summary>
    public void Start()
    {
        var l = new TcpListener(_endpoint);
        l.ExclusiveAddressUse = true;
        if (_endpoint.AddressFamily == AddressFamily.InterNetworkV6 && _endpoint.Address.Equals(IPAddress.IPv6Any)) l.Server.DualMode = true;
        l.Start(128);
        _listener = l;
        LocalEndPoint = (IPEndPoint)l.LocalEndpoint;
        _accept = new Thread(AcceptLoop) { IsBackground = true, Name = $"webadmin-accept-{Name}" };
        _accept.Start();
    }

    private void AcceptLoop()
    {
        while (!_stopping)
        {
            Socket s;
            try
            {
                s = _listener!.AcceptSocket();
            }
            catch (Exception) when (_stopping)
            {
                break;
            }
            catch (SocketException e)
            {
                Interlocked.Increment(ref _shared.Counters.Errors);
                _shared.Log.Debug($"accept on {Name}: {e.SocketErrorCode}");
                Thread.Sleep(20);
                continue;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            Admit(s);
        }
    }

    private void Admit(Socket s)
    {
        IPEndPoint remote, local;
        try
        {
            local = (IPEndPoint)s.LocalEndPoint!;
            remote = Normalize((IPEndPoint)s.RemoteEndPoint!);
        }
        catch (Exception)
        {
            s.Dispose();
            return;
        }
        if (StartConnection(remote.Address, relayed: false, id => new HttpConnection(id, s, remote, local, _tls, _shared, _cts.Token)) is null)
            Refuse(s);
    }

    // The relay provides the real ingress address over its authenticated control link and supplies encrypted bytes only.
    internal Task AdmitRelayAsync(Stream transport, IPEndPoint remote)
    {
        remote = Normalize(remote);
        if (_tls is { } tls &&
            StartConnection(remote.Address, relayed: true, id => new HttpConnection(id, transport, remote, LocalEndPoint, tls, _shared, _cts.Token)) is { } task)
            return task;
        Interlocked.Increment(ref _shared.Counters.Refused);
        transport.Dispose();
        return Task.CompletedTask;
    }

    private static IPEndPoint Normalize(IPEndPoint address) =>
        address.Address.IsIPv4MappedToIPv6 ? new IPEndPoint(address.Address.MapToIPv4(), address.Port) : address;

    private Task? StartConnection(IPAddress peer, bool relayed, Func<long, HttpConnection> create)
    {
        lock (_admissions)
        {
            var policy = _shared.Policy();
            // relayed peers: public addresses only and no loopback exemption from the allowlist (review S3)
            if (_stopping || !policy.ClientAllowed(peer, relayed) || relayed && !Relay.RelayComponent.AcceptableAnnouncement(peer)) return null;
            if (Interlocked.Increment(ref _shared.OpenConnections) > policy.Limits.MaxConnections)
            {
                Interlocked.Decrement(ref _shared.OpenConnections);
                return null;
            }
            if (!_shared.Limiter.TryOpen(peer, policy.Limits, exempt: !relayed && policy.IsTrustedProxy(peer)))
            {
                Interlocked.Decrement(ref _shared.OpenConnections);
                return null;
            }
            Interlocked.Increment(ref _shared.Counters.Accepted);
            var c = create(++_nextId);
            _conns[c.Id] = c;
            return c.Task = RunConnectionAsync(c, peer);
        }
    }

    private Task RunConnectionAsync(HttpConnection c, IPAddress peer) => Task.Run(async () =>
        {
            try
            {
                await c.RunAsync().ConfigureAwait(false);
            }
            finally
            {
                _conns.TryRemove(c.Id, out _);
                _shared.Limiter.Close(peer);
                Interlocked.Decrement(ref _shared.OpenConnections);
            }
    });

    private void Refuse(Socket s)
    {
        Interlocked.Increment(ref _shared.Counters.Refused);
        try
        {
            s.LingerState = new LingerOption(true, 0);
            s.Close();
        }
        catch (Exception)
        {
            s.Dispose();
        }
    }

    /// <summary>The unload order: stop accepting, close every connection gracefully (idle HTTP at once, busy HTTP after its
    /// response, WebSockets with 1001 "server stopping"), wait up to <paramref name="graceMs"/>, then cancel and abort
    /// what is left. Synchronous: OnUnload calls it.</summary>
    public StopReport Stop(int graceMs)
    {
        lock (_admissions)
        {
            if (_stopping) return new StopReport(0, 0, 0, true);
            _stopping = true;
        }
        try
        {
            _listener?.Stop();
        }
        catch (Exception)
        {
            // already stopped
        }
        var acceptEnded = _accept?.Join(graceMs) ?? true;
        var all = _conns.Values.ToArray();
        foreach (var c in all) c.BeginClose();
        var tasks = all.Select(c => c.Task ?? Task.CompletedTask).ToArray();
        var ended = Task.WaitAll(tasks, graceMs);
        _cts.Cancel();
        var aborted = 0;
        if (!ended)
        {
            foreach (var c in all)
            {
                if (c.Task is { IsCompleted: false })
                {
                    aborted++;
                    c.Abort();
                }
            }
            Task.WaitAll(tasks, 250);
        }
        _cts.Dispose();
        return new StopReport(all.Length, all.Length - aborted, aborted, acceptEnded);
    }
}
