// SPDX-License-Identifier: MIT
using System.Net;
using System.Text.RegularExpressions;

namespace Kilnwarden.Tls.Acme;

/// <summary>acme.json in the data folder: used while settings.json tls.mode is "acme" (DESIGN §2). Hot-reloaded.</summary>
public sealed partial class AcmeSettings
{
    public const string LetsEncrypt = "https://acme-v02.api.letsencrypt.org/directory";

    public int Format { get; set; } = 1;

    /// <summary>The ACME directory (https only).</summary>
    public string Directory { get; set; } = LetsEncrypt;

    /// <summary>Optional contact for expiry notices.</summary>
    public string Email { get; set; } = "";

    /// <summary>The owner accepts the CA's terms of service; nothing is ordered until this is true.</summary>
    public bool AcceptTerms { get; set; }

    /// <summary>The DNS names to certify (no wildcards, no IP addresses). Challenges are answered for these only.</summary>
    public List<string> Names { get; set; } = [];

    /// <summary>"http-01" (port 80), "tls-alpn-01" (port 443, answered by the panel's own TLS listener: the panel listens
    /// on 443 or 443 is forwarded to it) or "dns-01" (the registered relay name).</summary>
    public string Challenge { get; set; } = "http-01";

    public int Dns01PropagationSeconds { get; set; } = 60;

    /// <summary>Where the http-01 answerer listens while an order is validated ("" = all addresses).</summary>
    public string Http01Bind { get; set; } = "";

    public int Http01Port { get; set; } = 80;

    /// <summary>Renew when the certificate has fewer days left.</summary>
    public int RenewDaysBefore { get; set; } = 30;

    /// <summary>After a failure, wait at least this long, doubling up to <see cref="MaxRetryHours"/>.</summary>
    public int MinRetryMinutes { get; set; } = 60;

    public int MaxRetryHours { get; set; } = 24;

    /// <summary>At most this many orders in any 7 days (the CA has its own, stricter limits per name).</summary>
    public int MaxOrdersPerWeek { get; set; } = 5;

    public static IReadOnlyList<string> Validate(AcmeSettings s)
    {
        var p = new List<string>();
        if (!Uri.TryCreate(s.Directory, UriKind.Absolute, out var d) || d.Scheme != "https" || d.UserInfo.Length > 0) p.Add("directory must be an https URL");
        if (s.Names is null) return [.. p, "names must be a list"];
        if (s.Names.Count > 20) p.Add("at most 20 names");
        foreach (var n in s.Names)
            if (!IsDnsName(n)) p.Add($"'{n}' is not a DNS name (no wildcards, IP addresses or localhost)");
        if (s.Challenge is not ("http-01" or "tls-alpn-01" or "dns-01")) p.Add("challenge must be http-01, tls-alpn-01 or dns-01");
        if (s.Dns01PropagationSeconds is < 5 or > 300) p.Add("dns01PropagationSeconds must be 5-300");
        if (s.Http01Bind is { Length: > 0 } b && !IPAddress.TryParse(b, out _)) p.Add("http01Bind must be an IP address or empty");
        if (s.Http01Port is < 0 or > 65535) p.Add("http01Port must be 1-65535 (0: any free port, for tests)");
        if (s.RenewDaysBefore is < 1 or > 60) p.Add("renewDaysBefore must be 1-60");
        if (s.MinRetryMinutes is < 1 or > 1440) p.Add("minRetryMinutes must be 1-1440");
        if (s.MaxRetryHours is < 1 or > 168) p.Add("maxRetryHours must be 1-168");
        if (s.MaxOrdersPerWeek is < 1 or > 50) p.Add("maxOrdersPerWeek must be 1-50");
        if (s.Email is { Length: > 0 } e && (e.Length > 254 || !e.Contains('@') || e.Any(c => c <= ' ' || c is ',' or '<' or '>'))) p.Add("email is not an address");
        return p;
    }

    public static bool IsDnsName(string? n) =>
        n is { Length: > 0 and <= 253 } && !IPAddress.TryParse(n, out _) && DnsRe().IsMatch(n) && n.Contains('.') &&
        !n.Equals("localhost", StringComparison.OrdinalIgnoreCase) && !n.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex("^(?=.{1,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)(\\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)*$", RegexOptions.IgnoreCase)]
    private static partial Regex DnsRe();
}
