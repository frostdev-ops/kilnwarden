// SPDX-License-Identifier: MIT
using Kilnwarden.Hosting;
using Kilnwarden.Tls;

namespace Kilnwarden.Fuzz;

/// <summary>The kwtls glue (KwtlsStream + the C ABI) fed with mutated client bytes through a fake transport: the
/// handshake must end (IOException or a closed stream), never hang, crash or throw anything else. Needs the host
/// build of kwtls (native/kwtls/dist/libkwtls.so on Linux); without it the target is left out.</summary>
public static class GlueTargets
{
    private static KwtlsServer? _server;

    public static IEnumerable<Target> All()
    {
        var server = Server();
        if (server is null) yield break;
        // a real ClientHello from .NET's SslStream (captured once), plus junk
        yield return new Target("kwtls-glue", b => Handshake(server, b), [typeof(IOException), typeof(OperationCanceledException)],
            [System.Text.Encoding.Latin1.GetString(ClientHello()), "\x16\x03\x01\x00\x05hello", "GET / HTTP/1.1\r\n\r\n"],
            ["\x16\x03\x01", "\x16\x03\x03", "\x15\x03\x03\x00\x02\x02\x28", "\x17\x03\x03", "\x00\x00", "\xff\xff"]);
    }

    private static KwtlsServer? Server()
    {
        if (_server is not null) return _server;
        var dir = AppContext.BaseDirectory;
        string? path = null;
        for (var d = new DirectoryInfo(dir); d is not null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "native", "kwtls", "dist", KwtlsLibrary.FileName(Product.KwtlsVersion));
            if (File.Exists(p))
            {
                path = p;
                break;
            }
        }
        if (path is null) return null;
        var lib = KwtlsLibrary.Load(path);
        var (cert, key) = lib.SelfSigned(["localhost"], "fuzz", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        var info = CertificateInspector.Inspect(cert, key, "self-signed");
        return _server = new KwtlsServer(lib, new Fixed(new CertificateMaterial(cert, key, info)), ["http/1.1"], new NullLog());
    }

    private static void Handshake(KwtlsServer server, byte[] input)
    {
        using var transport = new FeedStream(input);
        using var cts = new CancellationTokenSource(2000);
        var t = server.AcceptAsync(transport, cts.Token).AsTask();
        if (!((IAsyncResult)t).AsyncWaitHandle.WaitOne(3000)) throw new FuzzFinding("handshake did not end");
        using var s = t.GetAwaiter().GetResult();   // rethrows the handshake's own exception
        // a completed handshake from fuzz bytes is impossible without the key; reading must still end
        var buf = new byte[256];
        var r = s.ReadAsync(buf, cts.Token).AsTask();
        if (!((IAsyncResult)r).AsyncWaitHandle.WaitOne(3000)) throw new FuzzFinding("read did not end");
        r.GetAwaiter().GetResult();
    }

    private static byte[] ClientHello()
    {
        var capture = new CaptureStream();
        using var ssl = new System.Net.Security.SslStream(capture, false, (_, _, _, _) => true);
        try
        {
            ssl.AuthenticateAsClientAsync("localhost").Wait(2000);
        }
        catch (Exception)
        {
            // the capture stream ends the handshake after the first flight
        }
        return capture.Written.ToArray();
    }

    /// <summary>Returns the input, then EOF; swallows writes.</summary>
    private sealed class FeedStream(byte[] input) : Stream
    {
        private int _pos;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => input.Length;
        public override long Position { get => _pos; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] b, int o, int c)
        {
            var n = Math.Min(c, input.Length - _pos);
            Array.Copy(input, _pos, b, o, n);
            _pos += n;
            return n;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> dst, CancellationToken ct = default)
        {
            var n = Math.Min(dst.Length, input.Length - _pos);
            input.AsMemory(_pos, n).CopyTo(dst);
            _pos += n;
            return ValueTask.FromResult(n);
        }
        public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
        public override void SetLength(long v) => throw new NotSupportedException();
        public override void Write(byte[] b, int o, int c) { }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> src, CancellationToken ct = default) => ValueTask.CompletedTask;
    }

    /// <summary>Records what a client writes; reads end the handshake.</summary>
    private sealed class CaptureStream : Stream
    {
        public readonly MemoryStream Written = new();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get => 0; set { } }
        public override void Flush() { }
        public override int Read(byte[] b, int o, int c) => 0;
        public override long Seek(long o, SeekOrigin s) => 0;
        public override void SetLength(long v) { }
        public override void Write(byte[] b, int o, int c) => Written.Write(b, o, c);
    }

    private sealed class Fixed(CertificateMaterial m) : ICertificateSource
    {
        public string Mode => "self-signed";
        public CertificateMaterial Current => m;
        public event Action<CertificateMaterial>? Changed { add { } remove { } }
        public void Dispose() { }
    }

    private sealed class NullLog : IWebAdminLog
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? error = null) { }
    }
}
