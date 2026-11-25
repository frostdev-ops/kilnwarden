// SPDX-License-Identifier: MIT
using System.Text;
using Kilnwarden.Hosting;

namespace Kilnwarden.Tls;

/// <summary>The kwtls engine: TLS 1.3 and 1.2 (forward-secret AEAD only), ECDSA and RSA keys from PEM, full chains,
/// ALPN. A certificate change builds a new native config; connections already made keep the old one (each holds its own
/// reference), so the old handle is freed at once.</summary>
public sealed class KwtlsServer : ITlsServer
{
    private readonly KwtlsLibrary _lib;
    private readonly ICertificateSource _source;
    private readonly byte[] _alpn;
    private readonly IWebAdminLog _log;
    private readonly object _gate = new();
    private IntPtr _config;
    private bool _disposed;
    private CertificateInfo _info;

    public KwtlsServer(KwtlsLibrary lib, ICertificateSource source, IReadOnlyList<string> alpn, IWebAdminLog log)
    {
        _lib = lib;
        _source = source;
        _alpn = Encoding.ASCII.GetBytes(string.Join(',', alpn));
        _log = log;
        var m = source.Current;
        _config = Build(m);
        _info = m.Info;
        source.Changed += OnChanged;
    }

    public string Engine => $"kwtls {_lib.Version}";
    public CertificateInfo Certificate => Volatile.Read(ref _info);

    private unsafe IntPtr Build(CertificateMaterial m)
    {
        IntPtr h;
        fixed (byte* c = m.ChainPem)
        fixed (byte* k = m.KeyPem)
        fixed (byte* a = _alpn)
            h = _lib.ConfigNew(c, (nuint)m.ChainPem.Length, k, (nuint)m.KeyPem.Length, a, (nuint)_alpn.Length);
        if (h == IntPtr.Zero) throw new InvalidOperationException("kwtls: " + _lib.LastError());
        return h;
    }

    private unsafe void OnChanged(CertificateMaterial m)
    {
        try
        {
            var h = Build(m);
            IntPtr old;
            lock (_gate)
            {
                if (_disposed) { _lib.ConfigFree(h); return; }
                old = _config;
                _config = h;
                Volatile.Write(ref _info, m.Info);
            }
            if (old != IntPtr.Zero) _lib.ConfigFree(old);
            _log.Info($"TLS: now serving {m.Info.Source} certificate {m.Info.Sha256} ({m.Info.KeyType}, until {m.Info.NotAfter:yyyy-MM-dd})");
        }
        catch (Exception e)
        {
            _log.Error($"TLS: the new {m.Info.Source} certificate was refused, keeping the previous one: {e.Message}");
        }
    }

    public async ValueTask<TlsStream> AcceptAsync(Stream transport, CancellationToken ct)
    {
        var conn = NewConnection();
        if (conn == IntPtr.Zero) throw new IOException("kwtls: " + _lib.LastError());
        var s = new KwtlsStream(transport, _lib, conn);
        try
        {
            await s.HandshakeAsync(ct).ConfigureAwait(false);
            return s;
        }
        catch
        {
            await s.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private unsafe IntPtr NewConnection()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_config == IntPtr.Zero, this);
            return _lib.ConnNew(_config);
        }
    }

    public unsafe void Dispose()
    {
        _source.Changed -= OnChanged;
        IntPtr old;
        lock (_gate)
        {
            _disposed = true;
            old = _config;
            _config = IntPtr.Zero;
        }
        if (old != IntPtr.Zero) _lib.ConfigFree(old);
    }
}
