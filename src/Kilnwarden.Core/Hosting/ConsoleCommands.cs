// SPDX-License-Identifier: MIT
using System.Text;
using Kilnwarden.Config;
using Kilnwarden.Setup;

namespace Kilnwarden.Hosting;

/// <summary>The server console's <c>kilnwarden</c> command (the mod declares it for the console only; <c>webadmin</c> is its old name). On a rented game
/// server it works with file access alone: a text file <c>kiln/control/console/&lt;name&gt;.cmd</c> holding
/// <c>kilnwarden setup</c>, uploaded over FTP, and its answer in <c>&lt;name&gt;.result.json</c> next to it (Kiln's
/// console folder); also the host's web console and <c>kilnctl cmd</c>. These build the answer's text.</summary>
public static class ConsoleCommands
{
    /// <summary>Why the panel is off when the mod did not start it (0.1.2: the old name's copy is still installed), so
    /// the console says exactly what to do instead of "not running".</summary>
    public static string? Off { get; set; }

    private static string NotRunning => Off ?? $"{Product.Name} is not running (see kiln.log)";

    /// <summary><c>kilnwarden setup</c>: what to do next. While no owner exists: the first-run block with a new link for
    /// this computer (an hour; the one in kiln.log keeps working), or during the hosted setup its checklist and, once
    /// the name is secure, the link to create the owner there. <c>kilnwarden setup hosted &lt;name&gt;</c>: hosted access
    /// for that name, with the claim link.</summary>
    public static string Setup(WebAdminHost? host, string what = "", string name = "")
    {
        try
        {
            return what.Trim().ToLowerInvariant() switch
            {
                "" => SetupText(host),
                "hosted" => host?.Services.TryGet<SetupComponent>() is { } setup ? setup.ConsoleHosted(name) : NotRunning,
                _ => SetupTexts.SetupUsage,
            };
        }
        catch (InvalidOperationException) { return $"{Product.Name} is still starting; try again in a few seconds."; }
        catch (ArgumentException e) { return $"Not saved: {e.Message}"; }
        catch (Http.HttpError e) { return e.Message; }
    }

    private static string SetupText(WebAdminHost? host)
    {
        if (host?.Services.TryGet<Auth.FirstRunSetup>() is not { } first) return NotRunning;
        var setup = host.Services.TryGet<SetupComponent>();
        if (first.OwnerExists)
            return setup?.Progress.Done == false
                ? $"Your owner account exists. Finish the setup in the panel: {Addresses(host)}"
                : $"Set up. Sign in at {Addresses(host)}. Lost the owner's password? The Kiln Launcher's Open web panel signs you in as the owner (a server on your PC or over SSH).";
        if (host.Endpoints.Count == 0) return $"{Product.Name} is not listening yet; try again in a few seconds.";
        if (setup?.ConsoleHostedProgress(h => first.NewHostedLink(h) ?? "") is { } hosted) return hosted;
        return first.NewBlock() is { } block ? string.Join("\n", block) : $"{Product.Name} cannot make a setup link now (see kiln.log).";
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
        if (host?.Services.TryGet<Relay.RelayClaims>() is not { } claims) return NotRunning;
        var link = claims.Make("console");
        if (link.Error is { } error) return $"No claim link: {error} (kilnwarden setup hosted <name> sets the name).";
        host.Services.TryGet<Security.IAuditSink>()?.Record(new Security.AuditEntry(DateTimeOffset.UtcNow, "", "console", "core.relay.claim_link",
            new System.Text.Json.Nodes.JsonObject { ["host"] = link.Host, ["fingerprint"] = link.Fingerprint, ["source"] = "console" }, true, null, 0, null));
        var s = new StringBuilder($"Claim {link.Host} for this server: open this link yourself and sign in with the Discord account that should own " +
                                  $"the name (a member of the Kiln Discord; one name per account). It works once, for 15 minutes: {link.Url}");
        s.Append($" -- This server's relay key: SHA-256 {link.Fingerprint}.");
        foreach (var w in link.Warnings) s.Append($" -- Note: {w}.");
        return s.ToString();
    }

    /// <summary><c>kilnwarden status</c>: a checklist of what is done and what is left, with the next step's exact action.</summary>
    public static string Status(WebAdminHost? host)
    {
        try { return StatusText(host); }
        catch (InvalidOperationException) { return $"{Product.Name} is still starting; try again in a few seconds."; }
    }

    private static string StatusText(WebAdminHost? host)
    {
        if (host is null) return NotRunning;
        // the addresses only: the certificate's fingerprint is in kiln.log's "panel at" line and in Panel settings
        var running = host.Endpoints.Count == 0 ? "not listening yet" : Addresses(host);
        var owner = host.Services.TryGet<Auth.FirstRunSetup>()?.OwnerExists == true;
        var setup = host.Services.TryGet<SetupComponent>();
        var steps = setup?.StatusSteps(out _, out _);
        var route = setup?.Progress.Route ?? "";
        var hostName = host.Services.TryGet<Relay.RelayStatus>()?.Describe() is { Enabled: true } r ? r.Host : setup?.Progress.OwnHost ?? "";
        var extra = host.Services.TryGet<Relay.RelayMoves>()?.Describe();
        if (host.Settings.Current.RemoteSetup && !owner) extra = (extra is null ? "" : extra + "; ") + "settings.json remoteSetup is on";
        return SetupTexts.Status(running, steps, route, hostName, owner, setup?.Progress.Done ?? owner, extra);
    }

    /// <summary>A console line for the dev host (tools/devhost reads kiln/control/console like Kiln does): the
    /// <c>kilnwarden</c> commands with their arguments; null for anything else.</summary>
    public static string? Run(WebAdminHost? host, string line)
    {
        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length < 2 || words[0] is not ("kilnwarden" or "webadmin")) return null;
        return words[1] switch
        {
            "setup" => Setup(host, words.ElementAtOrDefault(2) ?? "", words.ElementAtOrDefault(3) ?? ""),
            "status" => Status(host),
            "claim" => Claim(host),
            _ => null,
        };
    }

    private static string Addresses(WebAdminHost host)
    {
        var all = host.Endpoints.Select(e => e.ToString()).ToList();
        if (host.Services.TryGet<Relay.RelayStatus>()?.Describe() is { Enabled: true, Host.Length: > 0 } r) all.Insert(0, $"https://{r.Host}/");
        return string.Join(" ", all.Distinct());
    }
}
