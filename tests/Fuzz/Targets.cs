// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kilnwarden.Config;
using Kilnwarden.Http;
using Kilnwarden.Routing;
using Kilnwarden.Server;
using Kilnwarden.Static;
using Kilnwarden.Storage;
using Kilnwarden.Tls;
using Kilnwarden.WebSockets;

namespace Kilnwarden.Fuzz;

/// <summary>A fuzz target: runs one input; throws <see cref="FuzzFinding"/> (or any exception not in
/// <see cref="Allowed"/>) when the code under test misbehaves.</summary>
public sealed record Target(string Name, Action<byte[]> Run, Type[] Allowed, string[] Seeds, string[] Dictionary);

public sealed class FuzzFinding(string message) : Exception(message);

public static class Targets
{
    private static readonly HttpLimits Limits = new(4096, 32, 1024);
    private static string Latin1(byte[] b) => Encoding.Latin1.GetString(b);

    private static readonly string[] HttpDict =
    [
        "\r\n", "\r\n\r\n", " ", ":", "GET ", "POST ", " HTTP/1.1", " HTTP/1.0", "Host: ", "Content-Length: ", "Transfer-Encoding: chunked",
        "Connection: ", "keep-alive", "close", "Upgrade: websocket", "Connection: Upgrade", "Expect: 100-continue", "%", "%2e", "..", "/", "?", "#",
        "\t", "\0", "\x7f", "\xff", "[::1]", ":15737", "Sec-WebSocket-Key: ", "Sec-WebSocket-Version: 13", "Origin: ",
    ];

    private const string Get = "GET /api/core/health?x=1 HTTP/1.1\r\nHost: localhost:15737\r\nUser-Agent: f\r\nCookie: a=b\r\n\r\n";
    private const string Post = "POST /api/x/actions/y HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Type: application/json\r\nContent-Length: 2\r\n\r\n{}";
    private const string Ws = "GET /api/core/ws HTTP/1.1\r\nHost: localhost:15737\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" +
                              "Sec-WebSocket-Version: 13\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nOrigin: https://localhost:15737\r\n\r\n";

    public static readonly Target[] All =
    [
        new("http-head", HttpHead, [typeof(HttpError)], [Get, Post, Ws, "\r\n\r\nGET / HTTP/1.0\r\n\r\n"], HttpDict),
        new("ws-upgrade", WsUpgrade, [typeof(HttpError)], [Ws, Get], HttpDict),
        new("cookies", b => Check(Cookies.Parse(Latin1(b)).Values.All(v => Cookies.IsCookieValue(v)), "cookie value with bad bytes"), [],
            ["a=1; b=\"2\"; __Host-kwa=abc", "x=y=z;;; =;"], ["=", ";", "\"", " ", "\t", "__Host-", ",", "\\"]),
        new("query", b => QueryString.Parse(Latin1(b)), [], ["a=1&b=%C3%A9&c=+x+", "k&&=&%"], ["%", "&", "=", "+", "%C3", "%A9", "%ZZ", "%0"]),
        new("path", PathTarget, [], ["/api/players/7", "/a%20b/", "/%2e%2e/"], ["/", "%", "%2e", "%2f", "%5c", "..", ".", "%00", "%C3%A9"]),
        new("json-body", JsonTarget, [typeof(HttpError)],
            ["{\"user\":\"ada\",\"enabled\":true,\"n\":[1,2,{\"x\":null}]}", "{\"bind\":\"0.0.0.0\",\"tls\":{\"mode\":\"off\"}}"],
            ["{", "}", "[", "]", "\"", ":", ",", "null", "true", "1e999", "\\u0000", "\\ud800", "/*", "\"tls\":"]),
        new("settings-file", SettingsTarget, [typeof(JsonException)],
            ["{\"bind\":\"0.0.0.0\",\"port\":1,\"tls\":{\"mode\":\"file\",\"certFile\":\"a\"},\"limits\":{\"maxConnections\":5}}", "// c\n{\"ipAllowlist\":[\"10.0.0.0/8\"],}"],
            ["null", "\"tls\":null", "\"limits\":null", "\"ipAllowlist\":null", "[null]", "\"reverseProxy\":{\"trustedProxies\":null}", ",", "}", "{", "\"self-signed\"", "-1", "1e9"]),
        new("pem", PemTarget, [typeof(FormatException)],
            ["-----BEGIN CERTIFICATE-----\nAQID\n-----END CERTIFICATE-----\n-----BEGIN PRIVATE KEY-----\nMAMCAQA=\n-----END PRIVATE KEY-----\n"],
            ["-----BEGIN ", "-----END ", "-----", "CERTIFICATE", "PRIVATE KEY", "EC PRIVATE KEY", "\n", "=", "A", "/+"]),
        new("accept-encoding", b => AcceptEncoding.Accepts(Latin1(b), "br"), [], ["gzip, deflate, br;q=0.5, *;q=0"], [",", ";", "q=", "0.", "1.000", "*", "br"]),
        new("ip-range", IpTarget, [], ["10.0.0.0/8", "fd00::/8", "1.2.3.4, ::ffff:10.0.0.1"], ["/", ":", ".", "%", ",", "::ffff:", "/33", "/0"]),
        new("host", HostTarget, [], ["localhost:15737", "[::1]:1", "panel.example.com"], ["[", "]", ":", "@", "%", ".", "localhost"]),
        new("forwarded", ForwardedTarget, [typeof(HttpError)],
            ["GET / HTTP/1.1\r\nHost: h\r\nX-Forwarded-For: 203.0.113.7, 10.0.0.1\r\nX-Forwarded-Proto: https\r\nX-Forwarded-Host: p.example\r\n\r\n"],
            [.. HttpDict, "X-Forwarded-For: ", "X-Forwarded-Proto: ", "X-Forwarded-Host: ", ", ", "::1"]),
    ];

    private static void Check(bool ok, string what)
    {
        if (!ok) throw new FuzzFinding(what);
    }

    private static void HttpHead(byte[] b)
    {
        if (HttpParser.TryParseHead(b, Limits, out var req, out var consumed) != ParseStatus.Complete) return;
        Check(consumed > 0 && consumed <= b.Length, "consumed out of range");
        Check(req!.Path.StartsWith('/') && req.Target.StartsWith(req.Path, StringComparison.Ordinal), "path not from the target");
        Check(req.ContentLength >= 0, "negative length");
        Check(HttpParser.TryParseHead(b.AsSpan(0, consumed), Limits, out _, out var again) == ParseStatus.Complete && again == consumed, "head not self-contained");
        foreach (var (k, v) in req.Headers) Check(!k.Contains('\r') && !v.Contains('\n') && !v.Contains('\r'), "CR/LF in a parsed header");
        _ = req.KeepAlive;
        _ = req.IsWebSocketUpgrade;
        _ = req.QueryValues.Count;
        _ = Cookies.Parse(req.Headers.Get("Cookie"));
    }

    private static void WsUpgrade(byte[] b)
    {
        if (HttpParser.TryParseHead(b, Limits, out var req, out _) != ParseStatus.Complete) return;
        var r = WebSocketUpgrade.Check(req!, "https://localhost:15737");
        if (r.Ok) Check(r.Accept.Length == 28 && req!.Headers.Get("Origin") is "https://localhost:15737" or not null, "accepted without key/origin");
    }

    private static void PathTarget(byte[] b)
    {
        if (!PathSegments.TryParse(Latin1(b), out var segs)) return;
        foreach (var s in segs) Check(s is not ("." or "..") && s.Length > 0 && s.IndexOfAny(['/', '\\', '\0']) < 0, "unsafe segment accepted");
    }

    private static void JsonTarget(byte[] b)
    {
        JsonBody.Parse<JsonObject>(b);
        JsonBody.Parse<ServerSettings>(b);
    }

    private static void SettingsTarget(byte[] b)
    {
        // what SettingsStore does with a hand-edited file: deserialize with the store's options, then validate
        ServerSettings? s;
        try
        {
            s = JsonSerializer.Deserialize<ServerSettings>(b, DataStore.Json);
        }
        catch (Exception e) when (e is NotSupportedException or ArgumentException or InvalidOperationException)
        {
            throw new JsonException(e.Message);
        }
        if (s is null) return;
        var problems = SettingsRules.Validate(s);
        if (problems.Count == 0) _ = ServerPolicy.From(s, publicBind: false);
    }

    private static void PemTarget(byte[] b)
    {
        Pem.Decode(b);
        Pem.KeyKind(b);
    }

    private static void IpTarget(byte[] b)
    {
        var s = Latin1(b);
        if (IpRange.TryParse(s, out var r)) _ = r.Contains(IPAddress.Loopback) | r.Contains(IPAddress.IPv6Loopback);
        _ = ServerPolicy.ForwardedFor(s);
    }

    private static void HostTarget(byte[] b)
    {
        var s = Latin1(b);
        _ = HttpParser.IsHost(s);
        _ = ServerPolicy.HostName(s);
        _ = ServerPolicy.IsLocalOrLiteral(s);
        _ = Policy.HostAllowed(s);
    }

    private static readonly ServerPolicy Policy = ServerPolicy.From(new ServerSettings { ReverseProxy = { Enabled = true } }, publicBind: false);

    private static void ForwardedTarget(byte[] b)
    {
        if (HttpParser.TryParseHead(b, Limits, out var req, out _) != ParseStatus.Complete) return;
        var (client, scheme, host) = Policy.Resolve(req!, IPAddress.Loopback, isTls: false);
        Check(scheme is "http" or "https", "bad scheme");
        Check(client is not null && host is not null, "null resolve");
    }
}
