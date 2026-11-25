// SPDX-License-Identifier: MIT
using System.Text;
using Kilnwarden.Tls;

namespace Kilnwarden.Offline.Tls;

public class PemTests
{
    private static byte[] B(string s) => Encoding.ASCII.GetBytes(s);

    [Fact]
    public void Decodes_blocks_and_skips_text_between()
    {
        var pem = "comment\n-----BEGIN CERTIFICATE-----\nAQID\nBA==\n-----END CERTIFICATE-----\nmore\r\n-----BEGIN PRIVATE KEY-----\r\nBQY=\r\n-----END PRIVATE KEY-----\r\n";
        var blocks = Pem.Decode(B(pem));
        Assert.Equal(2, blocks.Count);
        Assert.Equal("CERTIFICATE", blocks[0].Label);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, blocks[0].Der);
        Assert.Equal(new byte[] { 5, 6 }, blocks[1].Der);
        Assert.Empty(Pem.Decode(B("no pem here")));
    }

    [Theory]
    [InlineData("-----BEGIN CERTIFICATE-----\nAQID\n-----END PRIVATE KEY-----\n")]
    [InlineData("-----BEGIN CERTIFICATE-----\nAQID\n")]
    [InlineData("-----BEGIN CERTIFICATE-----\n!!!!\n-----END CERTIFICATE-----\n")]
    [InlineData("-----BEGIN CERTIFICATE-----\n\n-----END CERTIFICATE-----\n")]
    [InlineData("-----BEGIN -----\nAQID\n-----END -----\n")]
    [InlineData("-----BEGIN CERT")]
    public void Refuses_broken_blocks(string pem) => Assert.Throws<FormatException>(() => Pem.Decode(B(pem)));

    [Fact]
    public void Round_trip_and_key_kinds()
    {
        var pem = Pem.Encode("CERTIFICATE", [1, 2, 3]);
        Assert.Equal(new byte[] { 1, 2, 3 }, Pem.Decode(B(pem))[0].Der);
        using var ec = System.Security.Cryptography.ECDsa.Create();
        Assert.Equal("ecdsa", Pem.KeyKind(B(ec.ExportPkcs8PrivateKeyPem())));
        Assert.Equal("ecdsa", Pem.KeyKind(B(ec.ExportECPrivateKeyPem())));
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        Assert.Equal("rsa", Pem.KeyKind(B(rsa.ExportPkcs8PrivateKeyPem())));
        Assert.Equal("rsa", Pem.KeyKind(B(rsa.ExportRSAPrivateKeyPem())));
        Assert.Equal("", Pem.KeyKind(B(Pem.Encode("PRIVATE KEY", [0x30, 0x01, 0x00]))));
    }
}
