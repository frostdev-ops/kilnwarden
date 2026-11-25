// SPDX-License-Identifier: MIT
using System.Text;
using Kilnwarden.Identity;

namespace Kilnwarden.Ops;

/// <summary>A publisher key the owner trusts (updates.json trustedKeys): a minisign public key.</summary>
public sealed record SigningKey(string Name, byte[] KeyId, byte[] PublicKey)
{
    /// <summary>The key id as minisign prints it (the 8 bytes as a little-endian number, upper-case hex).</summary>
    public string KeyIdText => Minisign.KeyIdText(KeyId);
}

/// <summary>What a package's signature says.</summary>
/// <param name="State">"verified" (a trusted key signed these bytes), "unsigned" (no signature was given),
/// "unknown_key" (signed, by a key updates.json does not list), "invalid" (it does not match: altered bytes, a wrong
/// key, a broken file).</param>
public sealed record SignatureCheck(string State, string? Key = null, string? KeyId = null, string? TrustedComment = null, string? Problem = null)
{
    public static readonly SignatureCheck Unsigned = new("unsigned");
}

/// <summary>minisign signatures (https://jedisct1.github.io/minisign/), the format the Kiln launcher's updates are signed
/// in and that rsign/minisign make for any file: Ed25519 over the file ("Ed") or over its BLAKE2b-512 ("ED", the
/// default), plus a global signature over the signature and its trusted comment. Verification uses the panel's managed
/// Ed25519 (Identity/Ed25519.cs).</summary>
public static class Minisign
{
    public const int MaxSignatureText = 4096;

    /// <summary>Legacy "Ed" signatures sign the whole file, which must then be read into memory: only for files up to
    /// this size (minisign itself prehashes by default).</summary>
    public const long MaxLegacyBytes = 16 << 20;

    public static string KeyIdText(ReadOnlySpan<byte> id)
    {
        var b = id.ToArray();
        Array.Reverse(b);
        return Convert.ToHexString(b);
    }

    /// <summary>A public key: the base64 line ("RW...") alone, or a whole .pub file (an untrusted comment line, then
    /// the key). Null when it is not one.</summary>
    public static (byte[] KeyId, byte[] PublicKey)? ParsePublicKey(string? text)
    {
        if (text is null || text.Length > 1024) return null;
        var lines = text.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var line = lines.Length switch
        {
            1 => lines[0],
            2 when lines[0].StartsWith("untrusted comment:", StringComparison.Ordinal) => lines[1],
            _ => null,
        };
        if (line is null || !TryBase64(line, out var raw) || raw.Length != 42 || raw[0] != (byte)'E' || raw[1] != (byte)'d') return null;
        return (raw[2..10], raw[10..42]);
    }

    private static bool TryBase64(string s, out byte[] bytes)
    {
        bytes = [];
        if (s.Length is 0 or > 200) return false;
        try
        {
            bytes = Convert.FromBase64String(s);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Checks <paramref name="signatureText"/> (a .minisig file) for the file at <paramref name="path"/>.</summary>
    public static SignatureCheck VerifyFile(string? signatureText, string path, IReadOnlyList<SigningKey> keys) =>
        Verify(signatureText, () => new FileInfo(path).Length > MaxLegacyBytes ? null : File.ReadAllBytes(path), () => Blake2b.HashFile(path), keys);

    public static SignatureCheck Verify(string? signatureText, ReadOnlySpan<byte> data, IReadOnlyList<SigningKey> keys)
    {
        var copy = data.ToArray();
        return Verify(signatureText, () => copy, () => Blake2b.Hash(copy), keys);
    }

    private static SignatureCheck Verify(string? text, Func<byte[]?> raw, Func<byte[]> prehash, IReadOnlyList<SigningKey> keys)
    {
        if (string.IsNullOrWhiteSpace(text)) return SignatureCheck.Unsigned;
        if (text.Length > MaxSignatureText) return new("invalid", Problem: "the signature file is too long");
        var lines = text.Replace("\r", "").Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0).ToArray();
        if (lines.Length != 4 || !lines[0].StartsWith("untrusted comment:", StringComparison.Ordinal) ||
            !lines[2].StartsWith("trusted comment: ", StringComparison.Ordinal))
            return new("invalid", Problem: "not a minisign signature (four lines: untrusted comment, signature, trusted comment, global signature)");
        if (!TryBase64(lines[1].Trim(), out var sig) || sig.Length != 74 || !TryBase64(lines[3].Trim(), out var global) || global.Length != 64)
            return new("invalid", Problem: "the signature lines are not minisign's");
        var alg = Encoding.ASCII.GetString(sig, 0, 2);
        if (alg is not ("Ed" or "ED")) return new("invalid", Problem: $"unknown signature algorithm '{alg}'");
        var keyId = sig[2..10];
        var signature = sig[10..74];
        var trusted = lines[2]["trusted comment: ".Length..];
        var key = keys.FirstOrDefault(k => k.KeyId.AsSpan().SequenceEqual(keyId));
        if (key is null) return new("unknown_key", KeyId: KeyIdText(keyId), TrustedComment: Cut(trusted));
        var message = alg == "ED" ? prehash() : raw();
        if (message is null)
            return new("invalid", key.Name, key.KeyIdText, Cut(trusted), $"a legacy (unhashed) signature is only accepted for files up to {MaxLegacyBytes >> 20} MiB: sign it with minisign's default (prehashed)");
        if (!Ed25519.Verify(key.PublicKey, message, signature))
            return new("invalid", key.Name, key.KeyIdText, Cut(trusted), "the signature does not match these bytes (altered, or another key with the same id)");
        if (!Ed25519.Verify(key.PublicKey, [.. signature, .. Encoding.UTF8.GetBytes(trusted)], global))
            return new("invalid", key.Name, key.KeyIdText, Cut(trusted), "the trusted comment was altered");
        return new("verified", key.Name, key.KeyIdText, Cut(trusted));
    }

    private static string Cut(string s) => s.Length <= 200 ? s : s[..200];
}
