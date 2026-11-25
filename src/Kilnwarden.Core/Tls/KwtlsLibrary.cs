// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;
using System.Text;

namespace Kilnwarden.Tls;

/// <summary>The native kwtls library (native/kwtls: rustls + ring behind a C ABI). Loaded once per file for the life of
/// the process: a native DLL cannot be unloaded on hot reload, which is why each build has its own file name
/// (kwtls-&lt;version&gt;.dll). The version it reports must equal <see cref="Product.KwtlsVersion"/>.</summary>
public sealed unsafe class KwtlsLibrary
{
    private static readonly Dictionary<string, KwtlsLibrary> Loaded = new(StringComparer.Ordinal);
    private static readonly object Gate = new();

    public string Path { get; }
    public string Version { get; }

    internal readonly delegate* unmanaged<byte*, nuint, nint> VersionFn;
    internal readonly delegate* unmanaged<byte*, nuint, byte*, nuint, byte*, nuint, IntPtr> ConfigNew;
    internal readonly delegate* unmanaged<IntPtr, void> ConfigFree;
    internal readonly delegate* unmanaged<IntPtr, IntPtr> ConnNew;
    internal readonly delegate* unmanaged<IntPtr, void> ConnFree;
    internal readonly delegate* unmanaged<IntPtr, byte*, nuint, nint> ReadTls;
    internal readonly delegate* unmanaged<IntPtr, byte*, nuint, nint> WriteTls;
    internal readonly delegate* unmanaged<IntPtr, int> IsHandshaking;
    internal readonly delegate* unmanaged<IntPtr, byte*, nuint, nint> Read;
    internal readonly delegate* unmanaged<IntPtr, byte*, nuint, nint> Write;
    internal readonly delegate* unmanaged<IntPtr, void> SendClose;
    internal readonly delegate* unmanaged<IntPtr, byte*, nuint, nint> Info;
    internal readonly delegate* unmanaged<byte*, nuint, byte*, nuint, byte*, nuint, int, byte, byte, int, byte, byte, byte*, nuint, nuint*, byte*, nuint, nuint*, nint> SelfSignedFn;
    internal readonly delegate* unmanaged<byte*, nuint, nint> LastErrorFn;

    private KwtlsLibrary(string path)
    {
        Path = path;
        var h = NativeLibrary.Load(path);
        VersionFn = (delegate* unmanaged<byte*, nuint, nint>)NativeLibrary.GetExport(h, "kwtls_version");
        ConfigNew = (delegate* unmanaged<byte*, nuint, byte*, nuint, byte*, nuint, IntPtr>)NativeLibrary.GetExport(h, "kwtls_config_new");
        ConfigFree = (delegate* unmanaged<IntPtr, void>)NativeLibrary.GetExport(h, "kwtls_config_free");
        ConnNew = (delegate* unmanaged<IntPtr, IntPtr>)NativeLibrary.GetExport(h, "kwtls_conn_new");
        ConnFree = (delegate* unmanaged<IntPtr, void>)NativeLibrary.GetExport(h, "kwtls_conn_free");
        ReadTls = (delegate* unmanaged<IntPtr, byte*, nuint, nint>)NativeLibrary.GetExport(h, "kwtls_read_tls");
        WriteTls = (delegate* unmanaged<IntPtr, byte*, nuint, nint>)NativeLibrary.GetExport(h, "kwtls_write_tls");
        IsHandshaking = (delegate* unmanaged<IntPtr, int>)NativeLibrary.GetExport(h, "kwtls_is_handshaking");
        Read = (delegate* unmanaged<IntPtr, byte*, nuint, nint>)NativeLibrary.GetExport(h, "kwtls_read");
        Write = (delegate* unmanaged<IntPtr, byte*, nuint, nint>)NativeLibrary.GetExport(h, "kwtls_write");
        SendClose = (delegate* unmanaged<IntPtr, void>)NativeLibrary.GetExport(h, "kwtls_send_close");
        Info = (delegate* unmanaged<IntPtr, byte*, nuint, nint>)NativeLibrary.GetExport(h, "kwtls_info");
        SelfSignedFn = (delegate* unmanaged<byte*, nuint, byte*, nuint, byte*, nuint, int, byte, byte, int, byte, byte, byte*, nuint, nuint*, byte*, nuint, nuint*, nint>)
            NativeLibrary.GetExport(h, "kwtls_self_signed");
        LastErrorFn = (delegate* unmanaged<byte*, nuint, nint>)NativeLibrary.GetExport(h, "kwtls_last_error");
        var b = stackalloc byte[128];
        var n = VersionFn(b, 128);
        Version = n > 0 ? Encoding.UTF8.GetString(b, (int)n) : "?";
    }

    /// <summary>The library at <paramref name="path"/> (loaded on first use). Throws when it cannot load, lacks an export,
    /// or reports another version than <paramref name="expectedVersion"/> (null: <see cref="Product.KwtlsVersion"/>).</summary>
    public static KwtlsLibrary Load(string path, string? expectedVersion = null)
    {
        var full = System.IO.Path.GetFullPath(path);
        KwtlsLibrary lib;
        lock (Gate)
        {
            if (!Loaded.TryGetValue(full, out lib!))
            {
                lib = new KwtlsLibrary(full);
                Loaded[full] = lib;
            }
        }
        var want = expectedVersion ?? Product.KwtlsVersion;
        if (!lib.Version.StartsWith(want + " ", StringComparison.Ordinal))
            throw new InvalidOperationException($"{System.IO.Path.GetFileName(full)} is kwtls {lib.Version}, this build needs {want}");
        return lib;
    }

    /// <summary>The file name the mod ships for this platform: kwtls-&lt;version&gt;.dll on Windows (and Wine),
    /// libkwtls.so elsewhere (offline tests).</summary>
    public static string FileName(string version) => OperatingSystem.IsWindows() ? $"kwtls-{version}.dll" : "libkwtls.so";

    /// <summary>The calling thread's last error (call right after the failing call, on the same thread).</summary>
    public string LastError()
    {
        var b = stackalloc byte[512];
        var n = LastErrorFn(b, 512);
        return n > 0 ? Encoding.UTF8.GetString(b, (int)n) : "unknown error";
    }

    /// <summary>A self-signed ECDSA P-256 certificate (rcgen): (certificate PEM, PKCS#8 key PEM).</summary>
    public (byte[] CertPem, byte[] KeyPem) SelfSigned(IReadOnlyList<string> names, string commonName, DateTimeOffset from, DateTimeOffset to)
    {
        var n = Encoding.UTF8.GetBytes(string.Join(',', names));
        var cn = Encoding.UTF8.GetBytes(commonName);
        var serial = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        serial[0] &= 0x7F;
        var cert = new byte[16 * 1024];
        var key = new byte[8 * 1024];
        nuint cl = 0, kl = 0;
        nint r;
        fixed (byte* pn = n)
        fixed (byte* pc = cn)
        fixed (byte* ps = serial)
        fixed (byte* pcert = cert)
        fixed (byte* pkey = key)
            r = SelfSignedFn(pn, (nuint)n.Length, pc, (nuint)cn.Length, ps, (nuint)serial.Length,
                from.UtcDateTime.Year, (byte)from.UtcDateTime.Month, (byte)from.UtcDateTime.Day,
                to.UtcDateTime.Year, (byte)to.UtcDateTime.Month, (byte)to.UtcDateTime.Day,
                pcert, (nuint)cert.Length, &cl, pkey, (nuint)key.Length, &kl);
        if (r != 0) throw new InvalidOperationException("kwtls self-signed: " + LastError());
        var keyPem = key.AsSpan(0, (int)kl).ToArray();
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
        return (cert.AsSpan(0, (int)cl).ToArray(), keyPem);
    }
}
