// SPDX-License-Identifier: MIT
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Text;

namespace Kilnwarden.Tls;

/// <summary>PEM blocks (RFC 7468), read strictly enough for certificates and keys: "-----BEGIN X-----", base64 lines,
/// "-----END X-----" with the same label. Text outside blocks is ignored (comments in fullchain files). A block with
/// bad base64 or a mismatched END is an error (fuzz target).</summary>
public static class Pem
{
    public const int MaxBlocks = 16;

    /// <summary>Bound the allocation before reading owner-provided certificate/key files, including reloads.</summary>
    public static byte[] ReadFile(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (file.Length is <= 0 or > 1 << 20) throw new FormatException("PEM file must be 1 byte..1 MiB");
        var bytes = new byte[(int)file.Length];
        file.ReadExactly(bytes);
        if (file.ReadByte() != -1) throw new FormatException("PEM file changed while being read");
        return bytes;
    }

    public readonly record struct Block(string Label, byte[] Der);

    public static List<Block> Decode(ReadOnlySpan<byte> text)
    {
        if (text.Length > 1 << 20) throw new FormatException("PEM file over 1 MiB");
        var s = Encoding.ASCII.GetString(text);
        var blocks = new List<Block>();
        var pos = 0;
        while (true)
        {
            var begin = s.IndexOf("-----BEGIN ", pos, StringComparison.Ordinal);
            if (begin < 0) break;
            var labelEnd = s.IndexOf("-----", begin + 11, StringComparison.Ordinal);
            if (labelEnd < 0) throw new FormatException("unterminated BEGIN line");
            var label = s[(begin + 11)..labelEnd];
            if (label.Length is 0 or > 64 || label.Any(c => c is < ' ' or > '~')) throw new FormatException("bad PEM label");
            var endMarker = "-----END " + label + "-----";
            var bodyStart = labelEnd + 5;
            var end = s.IndexOf("-----END ", bodyStart, StringComparison.Ordinal);
            if (end < 0 || string.CompareOrdinal(s, end, endMarker, 0, endMarker.Length) != 0) throw new FormatException($"no END for {label}");
            var b64 = new StringBuilder(end - bodyStart);
            foreach (var c in s.AsSpan(bodyStart, end - bodyStart))
                if (c is not (' ' or '\t' or '\r' or '\n')) b64.Append(c);
            byte[] der;
            try
            {
                der = Convert.FromBase64String(b64.ToString());
            }
            catch (FormatException)
            {
                throw new FormatException($"bad base64 in {label}");
            }
            if (der.Length == 0) throw new FormatException($"empty {label}");
            blocks.Add(new Block(label, der));
            if (blocks.Count > MaxBlocks) throw new FormatException("too many PEM blocks");
            pos = end + endMarker.Length;
        }
        return blocks;
    }

    public static string Encode(string label, ReadOnlySpan<byte> der) =>
        new string(PemEncoding.Write(label, der)) + "\n";

    /// <summary>"ecdsa" or "rsa" from a private key PEM (PKCS#8, SEC1 or PKCS#1), "" when unknown.</summary>
    public static string KeyKind(ReadOnlySpan<byte> keyPem)
    {
        foreach (var b in Decode(keyPem))
        {
            switch (b.Label)
            {
                case "EC PRIVATE KEY":
                    return "ecdsa";
                case "RSA PRIVATE KEY":
                    return "rsa";
                case "PRIVATE KEY":
                    try
                    {
                        // PrivateKeyInfo ::= SEQUENCE { version, AlgorithmIdentifier SEQUENCE { algorithm OID, ... }, ... }
                        var r = new AsnReader(b.Der, AsnEncodingRules.DER).ReadSequence();
                        r.ReadInteger();
                        var oid = r.ReadSequence().ReadObjectIdentifier();
                        return oid switch { "1.2.840.10045.2.1" => "ecdsa", "1.2.840.113549.1.1.1" => "rsa", "1.3.101.112" => "ed25519", _ => "" };
                    }
                    catch (AsnContentException)
                    {
                        return "";
                    }
            }
        }
        return "";
    }
}
