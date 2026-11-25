// SPDX-License-Identifier: MIT
namespace Kilnwarden.Config;

/// <summary>settings.json in the data folder: the server's own settings (other components keep theirs in their own
/// files through <see cref="SettingsStore{T}"/>). Hand-editable (comments allowed), hot-reloaded: limits apply to the
/// next request, bind/port/TLS changes restart the listener. Defaults are the safe ones (DESIGN §2).</summary>
public sealed class ServerSettings
{
    public int Format { get; set; } = 1;

    /// <summary>The address to listen on. "127.0.0.1" (default) is this machine only. "0.0.0.0" or "::" (all addresses)
    /// or one address makes the panel reachable from elsewhere; it only takes effect once the owner account exists, and
    /// the log warns loudly.</summary>
    public string Bind { get; set; } = "127.0.0.1";

    /// <summary>0: the game's query port + 100 (15737 with the default 15637).</summary>
    public int Port { get; set; }

    public TlsSettings Tls { get; set; } = new();
    public ReverseProxySettings ReverseProxy { get; set; } = new();

    /// <summary>Host names the panel answers to (DNS-rebinding guard). Empty: localhost and IP literals, plus any name
    /// in tls.hostnames. Public binds also require explicit DNS names.</summary>
    public List<string> AllowedHosts { get; set; } = [];

    /// <summary>Optional client allowlist: addresses or CIDR ranges ("192.168.1.0/24"). Empty: everyone (subject to the
    /// limits). Loopback is always allowed.</summary>
    public List<string> IpAllowlist { get; set; } = [];

    public LimitSettings Limits { get; set; } = new();

    /// <summary>Strict-Transport-Security on HTTPS responses to a DNS name (never for localhost or IP literals: HSTS on
    /// localhost would pin every local web app to HTTPS).</summary>
    public bool Hsts { get; set; } = true;

    /// <summary>The first-run setup from another machine (a rented game server with FTP and a web console only, a
    /// container): while no owner account exists, the panel already listens on <see cref="Bind"/>, the relay connects
    /// and the ACME certificate is ordered, so the setup page opens from anywhere. It still needs the one-time setup
    /// link (kiln.log, or the console command <c>kilnwarden setup</c>). No effect once the owner exists. Off by
    /// default: until the owner exists the panel is this machine's only.</summary>
    public bool RemoteSetup { get; set; }
}

public enum TlsMode
{
    /// <summary>A certificate made on first start and kept in tls/ (ECDSA P-256 with kwtls, RSA-2048 with SslStream).</summary>
    SelfSigned,

    /// <summary>Your own PEM files (tls.certFile = fullchain, tls.keyFile), reloaded when they change.</summary>
    File,

    /// <summary>Plain HTTP: loopback only, or behind a reverse proxy (reverseProxy.enabled).</summary>
    Off,

    /// <summary>Let's Encrypt (step 2).</summary>
    Acme,
}

public enum TlsEngine
{
    /// <summary>kwtls when its DLL loads, else SslStream.</summary>
    Auto,
    Kwtls,
    SslStream,
}

public sealed class TlsSettings
{
    public TlsMode Mode { get; set; } = TlsMode.SelfSigned;
    public TlsEngine Engine { get; set; } = TlsEngine.Auto;

    /// <summary>For mode file: PEM paths, absolute or relative to the data folder.</summary>
    public string CertFile { get; set; } = "";

    public string KeyFile { get; set; } = "";

    /// <summary>Names in the self-signed certificate besides localhost, 127.0.0.1 and ::1.</summary>
    public List<string> Hostnames { get; set; } = [];
}

public sealed class ReverseProxySettings
{
    /// <summary>"I have a reverse proxy": allows tls.mode off on a non-loopback bind and trusts X-Forwarded-For/-Proto/-Host
    /// from <see cref="TrustedProxies"/>.</summary>
    public bool Enabled { get; set; }

    public List<string> TrustedProxies { get; set; } = ["127.0.0.1", "::1"];
}

public sealed class LimitSettings
{
    public int MaxConnections { get; set; } = 256;
    public int MaxConnectionsPerIp { get; set; } = 32;

    /// <summary>New connections per second per IP (a TLS handshake costs ~1 ms of CPU).</summary>
    public double ConnectionsPerSecondPerIp { get; set; } = 20;

    public double RequestsPerSecondPerIp { get; set; } = 50;
    public int RequestBurstPerIp { get; set; } = 200;
    public int MaxHeaderBytes { get; set; } = 16 * 1024;
    public int MaxHeaderCount { get; set; } = 100;
    public int MaxTargetBytes { get; set; } = 8 * 1024;
    public long MaxBodyBytes { get; set; } = 1 << 20;
    public int MaxWebSocketMessageBytes { get; set; } = 1 << 20;
    public int HandshakeTimeoutMs { get; set; } = 10_000;

    /// <summary>From a request's first byte to the end of its headers (slowloris).</summary>
    public int HeaderTimeoutMs { get; set; } = 10_000;

    /// <summary>An idle keep-alive connection closes after this.</summary>
    public int KeepAliveTimeoutMs { get; set; } = 30_000;

    /// <summary>The body must arrive within this plus 1 s per 64 KiB.</summary>
    public int BodyTimeoutMs { get; set; } = 10_000;

    public int WriteTimeoutMs { get; set; } = 30_000;
    public int MaxRequestsPerConnection { get; set; } = 10_000;
}
