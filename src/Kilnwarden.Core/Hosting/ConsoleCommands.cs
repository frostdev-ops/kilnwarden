// SPDX-License-Identifier: MIT
using System.Text;
using Kilnwarden.Config;

namespace Kilnwarden.Hosting;

/// <summary>The server console's <c>kilnwarden</c> command (the mod declares it for the console only; <c>webadmin</c> is its old name). On a rented game
/// server it works with file access alone: a text file <c>kiln/control/console/&lt;name&gt;.cmd</c> holding
/// <c>kilnwarden setup</c>, uploaded over FTP, and its answer in <c>&lt;name&gt;.result.json</c> next to it (Kiln's
/// console folder); also <c>kilnctl cmd</c>. These build the answer's text.</summary>
public static class ConsoleCommands
{
    /// <summary><c>kilnwarden setup</c>: a new one-time setup link while no owner account exists (good for an hour; the one
    /// in kiln.log keeps working), or how to sign in once one does.</summary>
    public static string Setup(WebAdminHost? host)
    {
        try { return SetupText(host); }
        catch (InvalidOperationException) { return $"{Product.Name} is still starting; try again in a few seconds."; }
    }

    private static string SetupText(WebAdminHost? host)
    {
        if (host?.Services.TryGet<Auth.FirstRunSetup>() is not { } setup) return $"{Product.Name} is not running (see kiln.log)";
        if (setup.OwnerExists)
            return $"The owner account exists: sign in at {Addresses(host)}. Lost the owner's password? The Kiln Launcher's Open web panel signs you in as the owner (a server on your PC or over SSH; it needs a Kiln Launcher that knows {Product.Name}).";
        if (host.Endpoints.Count == 0) return $"{Product.Name} is not listening yet; try again in a few seconds.";
        var link = setup.NewLink(out var where);
        if (link is null) return $"{Product.Name} cannot make a setup link now (see kiln.log).";
        var s = new StringBuilder($"No owner account yet. Open this link {where} to create it (it works once, for an hour): {link}");
        if (!host.Settings.Current.RemoteSetup)
            s.Append(" -- No access to this machine's own browser (a rented game server)? Set \"remoteSetup\": true in kiln/data/kilnwarden/settings.json (and the relay, or a public bind), then run kilnwarden setup again.");
        return s.ToString();
    }

    /// <summary><c>kilnwarden claim</c>: a claim link for relay.json's host, for the relay's Discord claim pages (a rented
    /// game server's owner, who can't reach the panel before the name is registered, gets it from the answer file over
    /// FTP). Never written to kiln.log: whoever opens the link first may claim the name for this server.</summary>
    public static string Claim(WebAdminHost? host)
    {
        try { return ClaimText(host); }
        catch (InvalidOperationException) { return $"{Product.Name} is still starting; try again in a few seconds."; }
    }

    private static string ClaimText(WebAdminHost? host)
    {
        if (host?.Services.TryGet<Relay.RelayClaims>() is not { } claims) return $"{Product.Name} is not running (see kiln.log)";
        var link = claims.Make("console");
        if (link.Error is { } error) return $"No claim link: {error} (kiln/data/kilnwarden/relay.json host).";
        host.Services.TryGet<Security.IAuditSink>()?.Record(new Security.AuditEntry(DateTimeOffset.UtcNow, "", "console", "core.relay.claim_link",
            new System.Text.Json.Nodes.JsonObject { ["host"] = link.Host, ["fingerprint"] = link.Fingerprint, ["source"] = "console" }, true, null, 0, null));
        var s = new StringBuilder($"Claim {link.Host} for this server: open this link yourself and sign in with the Discord account that should own " +
                                  $"the name (a member of the Kiln Discord; one name per account). It works once, for 15 minutes: {link.Url}");
        s.Append($" -- This server's relay key: SHA-256 {link.Fingerprint}.");
        foreach (var w in link.Warnings) s.Append($" -- Note: {w}.");
        return s.ToString();
    }

    /// <summary><c>kilnwarden status</c>: where the panel listens, its certificate, the owner, the relay.</summary>
    public static string Status(WebAdminHost? host)
    {
        try { return StatusText(host); }
        catch (InvalidOperationException) { return $"{Product.Name} is still starting; try again in a few seconds."; }
    }

    private static string StatusText(WebAdminHost? host)
    {
        if (host is null) return $"{Product.Name} is not running (see kiln.log)";
        var s = new StringBuilder($"{Product.Name} {Product.Version}: ");
        s.Append(host.Endpoints.Count == 0 ? "not listening" : $"listening at {Addresses(host)}");
        if (host.Certificate is { } c) s.Append($"; TLS {host.TlsEngine}, certificate {c.Source} SHA-256 {c.Sha256}");
        var owner = host.Services.TryGet<Auth.FirstRunSetup>()?.OwnerExists == true;
        var settings = host.Settings.Current;
        s.Append(owner ? "; owner account: yes" : $"; owner account: not yet (kilnwarden setup makes a link{(settings.RemoteSetup ? "; remoteSetup is on" : "")})");
        if (host.Services.TryGet<Relay.RelayStatus>() is { } relay)
        {
            var r = relay.Describe();
            s.Append(r.Enabled ? $"; relay {r.Host}: {r.State}{(r.LastError is { } e && r.State != "connected" ? $" ({e})" : "")}" : "; relay: off");
            if (r.Enabled && host.Services.TryGet<Relay.RelayMoves>()?.Describe() is { } moves) s.Append($"; {moves}");
        }
        return s.ToString();
    }

    private static string Addresses(WebAdminHost host)
    {
        var all = host.Endpoints.Select(e => e.ToString()).ToList();
        if (host.Services.TryGet<Relay.RelayStatus>()?.Describe() is { Enabled: true, Host.Length: > 0 } r) all.Insert(0, $"https://{r.Host}/");
        return string.Join(" ", all.Distinct());
    }
}
