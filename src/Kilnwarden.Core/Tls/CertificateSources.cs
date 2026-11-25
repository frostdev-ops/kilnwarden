// SPDX-License-Identifier: MIT
using Kilnwarden.Hosting;
using Kilnwarden.Storage;

namespace Kilnwarden.Tls;

/// <summary>tls.mode self-signed: one certificate per key kind, made on the first start (off the tick thread) and kept
/// in tls/ (the key 0600), so its fingerprint stays the same across restarts and the launcher can pin it. It is made
/// again when it has under 30 days left or lacks a wanted name. 825 days at most (Apple refuses longer ones even when
/// trusted by hand).</summary>
public sealed class SelfSignedCertificateSource : ICertificateSource
{
    public const int ValidDays = 825;
    public const int RenewDays = 30;

    public SelfSignedCertificateSource(DataStore data, string keyKind, IReadOnlyList<string> names, IWebAdminLog log, TimeProvider time,
        Func<IReadOnlyList<string>, string, DateTimeOffset, DateTimeOffset, (byte[] Cert, byte[] Key)> generate)
    {
        var certFile = $"tls/self-signed-{keyKind}.crt.pem";
        var keyFile = $"tls/self-signed-{keyKind}.key.pem";
        var now = time.GetUtcNow();
        byte[]? cert = null, key = null;
        CertificateInfo? info = null;
        try
        {
            // the bounded read refuses an empty or oversized file with a FormatException: made again like any bad pair
            cert = data.Exists(certFile) ? Pem.ReadFile(data.PathOf(certFile)) : null;
            key = data.Exists(keyFile) ? Pem.ReadFile(data.PathOf(keyFile)) : null;
            if (cert is not null && key is not null)
            {
                info = CertificateInspector.Inspect(cert, key, Mode);
                var missing = names.Where(n => !info.Names.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();
                if (info.NotAfter - now < TimeSpan.FromDays(RenewDays)) Reason = $"it expires {info.NotAfter:yyyy-MM-dd}";
                else if (missing.Count > 0) Reason = "it lacks " + string.Join(", ", missing);
                else if (Pem.KeyKind(key) != keyKind) Reason = "its key is not " + keyKind;
            }
            else Reason = "there is none yet";
        }
        catch (Exception e) when (e is FormatException or System.Security.Cryptography.CryptographicException)
        {
            Reason = "it is unreadable: " + e.Message;
        }
        if (Reason is not null)
        {
            var cn = $"{Product.Name} self-signed {now:yyyy-MM-dd}";
            (cert, key) = generate(names, cn, now.AddDays(-1), now.AddDays(ValidDays));
            data.WriteBytes(keyFile, key, secret: true);
            data.WriteBytes(certFile, cert);
            info = CertificateInspector.Inspect(cert, key, Mode);
            Generated = true;
            log.Info($"TLS: made a self-signed {keyKind} certificate ({Reason}): SHA-256 {info.Sha256}, until {info.NotAfter:yyyy-MM-dd}, " +
                     $"names {string.Join(", ", info.Names)}");
        }
        Current = new CertificateMaterial(cert!, key!, info!);
    }

    public string Mode => "self-signed";
    public CertificateMaterial Current { get; }

    /// <summary>Why a new certificate was made this start (null: the kept one was fine).</summary>
    public string? Reason { get; private set; }

    public bool Generated { get; }

    public event Action<CertificateMaterial>? Changed { add { } remove { } }

    public void Dispose() => Current.Dispose();
}

/// <summary>tls.mode file: the owner's PEM files (full chain + key), polled every 5 s and reloaded when either changes.
/// A pair that does not read (half-written during a renewal) is retried on the next change; the engine keeps the
/// previous certificate until a good pair arrives.</summary>
public sealed class FileCertificateSource : ICertificateSource
{
    private readonly string _cert;
    private readonly string _key;
    private readonly IWebAdminLog _log;
    private readonly ITimer _timer;
    private readonly object _gate = new();
    private (long, DateTime, long, DateTime) _seen;
    private CertificateMaterial _current;

    public FileCertificateSource(string certPath, string keyPath, IWebAdminLog log, TimeProvider time, TimeSpan? poll = null)
    {
        _cert = certPath;
        _key = keyPath;
        _log = log;
        _seen = Stamp();
        _current = Load();
        var every = poll ?? TimeSpan.FromSeconds(5);
        _timer = time.CreateTimer(_ => Poll(), null, every, every);
    }

    public string Mode => "file";
    public CertificateMaterial Current => Volatile.Read(ref _current);
    public event Action<CertificateMaterial>? Changed;

    private CertificateMaterial Load()
    {
        var cert = Pem.ReadFile(_cert);
        var key = Pem.ReadFile(_key);
        return new CertificateMaterial(cert, key, CertificateInspector.Inspect(cert, key, Mode));
    }

    private (long, DateTime, long, DateTime) Stamp()
    {
        var c = new FileInfo(_cert);
        var k = new FileInfo(_key);
        return (c.Exists ? c.Length : -1, c.Exists ? c.LastWriteTimeUtc : default, k.Exists ? k.Length : -1, k.Exists ? k.LastWriteTimeUtc : default);
    }

    /// <summary>The timer's body (tests call it): true when a new pair was read and announced.</summary>
    public bool Poll()
    {
        CertificateMaterial next;
        lock (_gate)
        {
            try
            {
                var stamp = Stamp();
                if (stamp == _seen) return false;
                _seen = stamp;
                next = Load();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or System.Security.Cryptography.CryptographicException)
            {
                _log.Warn($"TLS: {_cert} / {_key} changed but do not read yet ({e.Message}); keeping the current certificate");
                return false;
            }
            Volatile.Write(ref _current, next);
        }
        Changed?.Invoke(next);
        return true;
    }

    public void Dispose() => _timer.Dispose();
}
