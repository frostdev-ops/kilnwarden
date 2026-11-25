// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;

namespace Kilnwarden.Ops;

/// <summary>updates/updates.json: where packages may come from and how big they may be (owner, sensitive to change).</summary>
public sealed class UpdateSettings
{
    /// <summary>"https://host" or "https://host:port": the only origins a URL (and every redirect) may point at.</summary>
    public List<string> AllowedOrigins { get; set; } =
        ["https://github.com", "https://objects.githubusercontent.com", "https://release-assets.githubusercontent.com"];

    public long MaxUploadBytes { get; set; } = 256L << 20;
    public long MaxDownloadBytes { get; set; } = 256L << 20;
    public long MaxUnpackedBytes { get; set; } = 512L << 20;
    public int MaxEntries { get; set; } = 10_000;

    public ArchiveLimits Limits => new(MaxEntries, MaxUnpackedBytes, Math.Min(MaxUnpackedBytes, 256L << 20));

    /// <summary>Publisher keys (minisign public keys) the owner trusts for signed packages.</summary>
    public List<TrustedKeySetting> TrustedKeys { get; set; } = [];

    /// <summary>Stage only packages a trusted key signed (off: unsigned packages can be staged after their checksum is
    /// compared by hand; a signature that does not match is always refused).</summary>
    public bool RequireSignature { get; set; }

    public IReadOnlyList<SigningKey> Keys() =>
        [.. (TrustedKeys ?? []).Select(k => (k, key: Minisign.ParsePublicKey(k.PublicKey))).Where(x => x.key is not null)
            .Select(x => new SigningKey(x.k.Name, x.key!.Value.KeyId, x.key.Value.PublicKey))];

    public static IReadOnlyList<string> Validate(UpdateSettings s)
    {
        var p = new List<string>();
        if (s.AllowedOrigins is null || s.AllowedOrigins.Count > 32) p.Add("allowedOrigins: at most 32");
        foreach (var o in s.AllowedOrigins ?? [])
            if (UrlPolicy.ParseOrigin(o) is null) p.Add($"allowedOrigins: '{o}' is not https://host or https://host:port");
        if (s.MaxUploadBytes is < 1 << 20 or > 1L << 30) p.Add("maxUploadBytes: 1 MiB to 1 GiB");
        if (s.MaxDownloadBytes is < 1 << 20 or > 1L << 30) p.Add("maxDownloadBytes: 1 MiB to 1 GiB");
        if (s.MaxUnpackedBytes is < 1 << 20 or > 2L << 30) p.Add("maxUnpackedBytes: 1 MiB to 2 GiB");
        if (s.MaxEntries is < 1 or > 100_000) p.Add("maxEntries: 1 to 100000");
        if (s.TrustedKeys is null || s.TrustedKeys.Count > 16) p.Add("trustedKeys: at most 16");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var k in s.TrustedKeys ?? [])
        {
            if (k is null || k.Name is not { Length: > 0 and <= 64 } || k.Name.Any(char.IsControl)) p.Add("trustedKeys: each needs a name (1 to 64 characters)");
            if (Minisign.ParsePublicKey(k?.PublicKey) is not { } parsed) p.Add($"trustedKeys: '{k?.Name}' is not a minisign public key (the RW... line of its .pub file)");
            else if (!ids.Add(Minisign.KeyIdText(parsed.KeyId))) p.Add($"trustedKeys: the key id {Minisign.KeyIdText(parsed.KeyId)} is listed twice");
        }
        if (s.RequireSignature && (s.TrustedKeys?.Count ?? 0) == 0) p.Add("requireSignature: list at least one trusted key first");
        return p;
    }
}

public sealed class TrustedKeySetting
{
    public string Name { get; set; } = "";

    /// <summary>The minisign public key: its base64 line ("RW...").</summary>
    public string PublicKey { get; set; } = "";
}

/// <summary>What a package URL may be: https, no user info, an allowlisted origin, and (when connecting) a public
/// address only.</summary>
public static class UrlPolicy
{
    /// <summary>"https://host[:port]" (lower-case host, no path) as (host, port); null when it is not one.</summary>
    public static (string Host, int Port)? ParseOrigin(string? origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps || u.UserInfo.Length > 0) return null;
        if (u.AbsolutePath != "/" || u.Query.Length > 0 || u.Fragment.Length > 0 || origin!.TrimEnd('/').Length != u.GetLeftPart(UriPartial.Authority).Length) return null;
        return (u.IdnHost.ToLowerInvariant(), u.Port);
    }

    /// <summary>Why <paramref name="url"/> may not be fetched, or null.</summary>
    public static string? Problem(Uri url, IEnumerable<string> allowed)
    {
        if (!url.IsAbsoluteUri || url.Scheme != Uri.UriSchemeHttps) return "only https URLs";
        if (url.UserInfo.Length > 0) return "no user name or password in the URL";
        var host = url.IdnHost.ToLowerInvariant();
        if (!allowed.Select(ParseOrigin).Any(o => o is { } x && x.Host == host && x.Port == url.Port))
            return $"{url.Scheme}://{url.Authority} is not an allowed source (updates.json allowedOrigins)";
        return null;
    }

    /// <summary>Whether a connection to <paramref name="a"/> is to the public internet (no loopback, private, link-local,
    /// CGNAT, multicast, unspecified, documentation or IPv4-mapped/translated private ranges).</summary>
    public static bool IsPublic(IPAddress a)
    {
        if (a.IsIPv4MappedToIPv6) a = a.MapToIPv4();
        if (IPAddress.IsLoopback(a) || a.Equals(IPAddress.Any) || a.Equals(IPAddress.IPv6Any) || a.Equals(IPAddress.Broadcast)) return false;
        if (a.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = a.GetAddressBytes();
            return !(b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224
                || b[0] == 100 && (b[1] & 0xC0) == 64 // 100.64/10 CGNAT
                || b[0] == 169 && b[1] == 254
                || b[0] == 172 && (b[1] & 0xF0) == 16
                || b[0] == 192 && b[1] == 168
                || b[0] == 192 && b[1] == 0 && (b[2] == 0 || b[2] == 2)
                || b[0] == 198 && (b[1] & 0xFE) == 18 // 198.18/15 benchmarking
                || b[0] == 198 && b[1] == 51 && b[2] == 100
                || b[0] == 203 && b[1] == 0 && b[2] == 113);
        }
        if (a.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (a.IsIPv6LinkLocal || a.IsIPv6SiteLocal || a.IsIPv6Multicast || a.IsIPv6UniqueLocal || a.IsIPv6Teredo) return false;
            var b = a.GetAddressBytes();
            if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8) return false; // documentation
            if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b) return false; // NAT64 (could reach private IPv4)
            if (b[0] == 0x20 && b[1] == 0x02) return false; // 6to4 (embeds an IPv4 address)
            return (b[0] & 0xE0) == 0x20; // global unicast 2000::/3 only
        }
        return false;
    }
}

/// <summary>Fetches a package to a file. The panel's one outbound fetcher: tests replace it.</summary>
public interface IPackageDownloader
{
    /// <summary>Downloads <paramref name="url"/> into <paramref name="file"/> (created new); returns the final URL. Throws
    /// <see cref="DownloadRefusedException"/> with a message for the owner.</summary>
    Task<Uri> DownloadAsync(Uri url, string file, long maxBytes, IReadOnlyList<string> allowedOrigins, CancellationToken ct);
}

public sealed class DownloadRefusedException(string message) : Exception(message);

/// <summary>The real downloader: no automatic redirects (each hop is checked here against the allowlist), no proxy, no
/// cookies, no decompression, public addresses only (checked on the addresses the connection actually uses), a byte cap
/// counted on the body, 120 s in total.</summary>
public sealed class HttpPackageDownloader : IPackageDownloader, IDisposable
{
    public const int MaxRedirects = 5;
    public static readonly TimeSpan Total = TimeSpan.FromSeconds(120);

    private readonly HttpMessageInvoker _http;

    public HttpPackageDownloader() : this(DefaultHandler())
    {
    }

    /// <summary>Tests pass a fake handler (no network).</summary>
    public HttpPackageDownloader(HttpMessageHandler handler)
    {
        _http = new HttpMessageInvoker(handler, disposeHandler: true);
    }

    public static SocketsHttpHandler DefaultHandler() => new()
    {
        AllowAutoRedirect = false, UseProxy = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.None,
        ConnectTimeout = TimeSpan.FromSeconds(15), PooledConnectionLifetime = TimeSpan.FromMinutes(1),
        ConnectCallback = async (ctx, ct) =>
        {
            var addrs = await Dns.GetHostAddressesAsync(ctx.DnsEndPoint.Host, ct).ConfigureAwait(false);
            // every address must be public: a name that resolves to both is refused (DNS rebinding)
            if (addrs.Length == 0 || addrs.Any(a => !UrlPolicy.IsPublic(a)))
                throw new HttpRequestException($"{ctx.DnsEndPoint.Host} resolves to an address that is not public");
            var s = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await s.ConnectAsync(addrs, ctx.DnsEndPoint.Port, ct).ConfigureAwait(false);
                return new NetworkStream(s, ownsSocket: true);
            }
            catch
            {
                s.Dispose();
                throw;
            }
        },
    };

    public async Task<Uri> DownloadAsync(Uri url, string file, long maxBytes, IReadOnlyList<string> allowedOrigins, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Total);
        var current = url;
        try
        {
            for (var hop = 0; ; hop++)
            {
                if (UrlPolicy.Problem(current, allowedOrigins) is { } why) throw new DownloadRefusedException($"{OpsHttp.SafeUrl(current)}: {why}");
                using var req = new HttpRequestMessage(HttpMethod.Get, current);
                req.Headers.UserAgent.Add(new ProductInfoHeaderValue(Product.ModId, Product.Version));
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
                using var res = await _http.SendAsync(req, cts.Token).ConfigureAwait(false);
                var code = (int)res.StatusCode;
                if (code is 301 or 302 or 303 or 307 or 308)
                {
                    if (hop >= MaxRedirects) throw new DownloadRefusedException($"more than {MaxRedirects} redirects");
                    var loc = res.Headers.Location ?? throw new DownloadRefusedException("a redirect without a Location");
                    current = loc.IsAbsoluteUri ? loc : new Uri(current, loc);
                    continue;
                }
                if (code != 200) throw new DownloadRefusedException($"{OpsHttp.SafeUrl(current)} answered {code}");
                if (res.Content.Headers.ContentLength is { } len && len > maxBytes) throw new DownloadRefusedException($"larger than {maxBytes} bytes");
                await using var body = await res.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
                await using var dst = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                var buf = new byte[81920];
                long total = 0;
                int n;
                while ((n = await body.ReadAsync(buf, cts.Token).ConfigureAwait(false)) > 0)
                {
                    total += n;
                    if (total > maxBytes) throw new DownloadRefusedException($"larger than {maxBytes} bytes");
                    await dst.WriteAsync(buf.AsMemory(0, n), cts.Token).ConfigureAwait(false);
                }
                return current;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new DownloadRefusedException($"the download took longer than {Total.TotalSeconds:F0} s");
        }
        catch (HttpRequestException e)
        {
            throw new DownloadRefusedException($"the download failed: {e.Message}");
        }
    }

    public void Dispose() => _http.Dispose();
}
