// SPDX-License-Identifier: MIT
using Kilnwarden.Server;

namespace Kilnwarden.Http;

/// <summary>Headers every response gets unless its handler set them (DESIGN §2 hardening). The CSP is strict: only the
/// panel's own scripts, styles, fonts and connections; no inline script or style, no framing, no plugins.</summary>
public static class SecurityHeaders
{
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data: blob:; font-src 'self'; connect-src 'self'; " +
        "worker-src 'self'; manifest-src 'self'; frame-src 'self'; object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'";

    public const string PermissionsPolicy = "camera=(), microphone=(), geolocation=(), payment=(), usb=(), interest-cohort=()";

    public static void Apply(HttpResponse r, bool https, string host, bool hsts)
    {
        var h = r.Headers;
        h.TryAdd("X-Content-Type-Options", "nosniff");
        h.TryAdd("X-Frame-Options", "DENY");
        h.TryAdd("Referrer-Policy", "no-referrer");
        h.TryAdd("Cross-Origin-Opener-Policy", "same-origin");
        h.TryAdd("Cross-Origin-Resource-Policy", "same-origin");
        h.TryAdd("Permissions-Policy", PermissionsPolicy);
        h.TryAdd("Content-Security-Policy", ContentSecurityPolicy);
        h.TryAdd("Cache-Control", "no-store");
        // HSTS on localhost or an IP would pin every local web app (any port) to HTTPS in that browser
        if (https && hsts && !ServerPolicy.IsLocalOrLiteral(host)) h.TryAdd("Strict-Transport-Security", "max-age=31536000");
    }
}
