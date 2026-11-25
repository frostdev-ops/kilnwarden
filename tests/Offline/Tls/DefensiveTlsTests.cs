// SPDX-License-Identifier: MIT
using System.Reflection;
using Kilnwarden.Offline.Support;
using Kilnwarden.Storage;
using Kilnwarden.Tls;

namespace Kilnwarden.Offline.Tls;

public class DefensiveTlsTests
{
    private static CertificateMaterial Material(KwtlsLibrary lib, string name)
    {
        var (cert, key) = lib.SelfSigned(["localhost"], name, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10));
        return new CertificateMaterial(cert, key, CertificateInspector.Inspect(cert, key, "test"));
    }

    [Fact]
    public async Task Late_certificate_callback_cannot_revive_disposed_engine()
    {
        var lib = Native.Kwtls();
        using var material = Material(lib, "first");
        using var next = Material(lib, "second");
        var source = new Source(material);
        using var engine = new KwtlsServer(lib, source, ["http/1.1"], new TestLog());
        var pending = source.Snapshot();
        engine.Dispose();
        pending(next); // a timer may have taken its callback snapshot just before disposal
        await Assert.ThrowsAsync<ObjectDisposedException>(() => engine.AcceptAsync(new MemoryStream(), default).AsTask());
    }

    [Fact]
    public void Stream_disposal_does_not_attempt_unbounded_synchronous_transport_write()
    {
        var lib = Native.Kwtls();
        using var material = Material(lib, "dispose");
        using var engine = new KwtlsServer(lib, new Source(material), ["http/1.1"], new TestLog());
        var ptr = (IntPtr)typeof(KwtlsServer).GetMethod("NewConnection", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(engine, null)!;
        using var transport = new RecordingTransport();
        var stream = new KwtlsStream(transport, lib, ptr);
        stream.Dispose();
        Assert.Equal(0, transport.SyncWrites);
    }

    [Theory]
    [InlineData(0)]
    [InlineData((1 << 20) + 1)]
    public void Unreadable_kept_self_signed_files_are_made_again(int size)
    {
        // The bounded PEM read refuses an empty or oversized file; the self-signed source must still heal itself (as it
        // does for any other unreadable pair) instead of failing the listener's start.
        using var dir = new TempDir();
        var data = new DataStore(dir.Path);
        data.WriteBytes("tls/self-signed-rsa.crt.pem", new byte[size]);
        data.WriteBytes("tls/self-signed-rsa.key.pem", new byte[size], secret: true);
        using var source = new SelfSignedCertificateSource(data, "rsa", ["localhost"], new TestLog(), TimeProvider.System,
            (n, cn, f, t) => Pfx.SelfSignedRsa(cn, n, f, t));
        Assert.True(source.Generated);
        Assert.StartsWith("it is unreadable", source.Reason);
        Assert.Equal("rsa", Pem.KeyKind(source.Current.KeyPem));
    }

    private sealed class RecordingTransport : MemoryStream
    {
        public int SyncWrites;
        public override void Write(byte[] buffer, int offset, int count) { SyncWrites++; }
        public override void Write(ReadOnlySpan<byte> buffer) { SyncWrites++; }
    }

    private sealed class Source(CertificateMaterial material) : ICertificateSource
    {
        public string Mode => "test";
        public CertificateMaterial Current => material;
        public event Action<CertificateMaterial>? Changed;
        public Action<CertificateMaterial> Snapshot() => Changed!;
        public void Dispose() { }
    }
}
