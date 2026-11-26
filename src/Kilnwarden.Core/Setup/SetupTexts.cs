// SPDX-License-Identifier: MIT
using System.Text;

namespace Kilnwarden.Setup;

/// <summary>What kiln.log and the server console say about the setup (0.1.2): short, one thing to do at a time.</summary>
public static class SetupTexts
{
    public const string HostedUsage = "Type: kilnwarden setup hosted <name>, with a name of 3 to 40 letters, digits or dashes (your panel's address becomes https://<name>.kiln.nexus).";
    public const string SetupUsage = "Type kilnwarden setup, or kilnwarden setup hosted <name> for a server you can't open a browser on.";
    public const string Terms = "Going on accepts Let's Encrypt's terms: https://letsencrypt.org/repository/";

    /// <summary>The block kiln.log and <c>kilnwarden setup</c> show while there is no owner: three lines.</summary>
    /// <param name="link">The setup link (kiln.log's, or a new one from the console).</param>
    /// <param name="anyMachine">settings.json remoteSetup is on: the link works from other machines too.</param>
    /// <param name="hostedOn">Hosted access is already set up (relay.json): the console command is plain kilnwarden setup.</param>
    public static IReadOnlyList<string> FirstStart(string link, bool anyMachine, bool hostedOn) =>
    [
        $"{Product.Name}: set up your panel (about 2 minutes).",
        anyMachine ? $"  From any browser, open {link}" : $"  On this computer, open {link}",
        hostedOn
            ? "  No browser on this computer (a rented server)? In the server console, type: kilnwarden setup"
            : "  No browser on this computer (a rented server)? In the server console, type: kilnwarden setup hosted <a name for your panel>",
    ];

    public static string HostedStarted(string host, string? claimUrl, bool ownerExists)
    {
        var s = new StringBuilder();
        s.Append($"Hosted access: your panel's address will be https://{host}/").Append('\n');
        if (claimUrl is not null)
            s.Append($"1. Claim the name with Discord (open it yourself; it works once, for 15 minutes): {claimUrl}").Append('\n');
        else s.Append("1. The name is already claimed for this server.").Append('\n');
        s.Append(ownerExists
            ? $"2. About 2 minutes after the claim, open https://{host}/ and sign in."
            : "2. About 2 minutes after the claim, type kilnwarden setup again: it gives you the link to create your account.").Append('\n');
        s.Append(Terms);
        return s.ToString();
    }

    /// <summary><c>kilnwarden setup</c> during the console's hosted setup: where it is, and the account link once ready.</summary>
    public static string HostedProgress(string host, IReadOnlyList<SetupStep> steps, string? link)
    {
        var s = new StringBuilder();
        if (link is not null)
        {
            s.Append($"Ready. Open this link to create your owner account (it works once, for 30 minutes): {link}").Append('\n');
            s.Append($"Then bookmark https://{host}/");
            return s.ToString();
        }
        s.Append($"Setting up https://{host}/ :").Append('\n');
        foreach (var step in steps) s.Append("  " + Line(step, host)).Append('\n');
        s.Append("Next: " + Next(steps, host, ownerExists: false));
        return s.ToString();
    }

    /// <summary><c>kilnwarden status</c>: the checklist and the next step's exact action.</summary>
    public static string Status(string running, IReadOnlyList<SetupStep>? steps, string route, string host, bool ownerExists, bool done, string? extra)
    {
        var s = new StringBuilder($"{Product.Name} {Product.Version}");
        s.Append('\n');
        s.Append($"  [x] Running: {running}").Append('\n');
        if (steps is not null)
            foreach (var step in steps) s.Append("  " + Line(step, host)).Append('\n');
        else if (route == "launcher") s.Append("  [x] Opened with the Kiln Launcher (Servers > your server > Open web panel)").Append('\n');
        s.Append(ownerExists ? "  [x] Owner account" : "  [ ] Owner account").Append('\n');
        if (extra is not null) s.Append("  " + extra).Append('\n');
        string next;
        if (steps is not null && steps.Any(x => x.State != "done")) next = Next(steps, host, ownerExists);
        else if (!ownerExists) next = steps is null ? "type kilnwarden setup for your setup link." : "type kilnwarden setup for the link to create your owner account.";
        else if (!done) next = $"finish the setup in the panel{(host.Length > 0 ? $" at https://{host}/" : "")}.";
        else next = "nothing. Everything is set up.";
        s.Append("Next: " + next);
        return s.ToString();
    }

    private static string Label(SetupStep step, string host) => step.Id switch
    {
        "connected" => "Connected to hosted access",
        "claimed" => $"Name claimed ({host})",
        "secure" => "Secure",
        "ready" => $"Ready at https://{host}/",
        "found" => $"Address found ({host})",
        "reachable" => "Reachable",
        _ => step.Id,
    };

    private static string Line(SetupStep step, string host)
    {
        var label = Label(step, host);
        return step.State switch
        {
            "done" => $"[x] {label}",
            "failed" => $"[!] {label}: {step.Problem?.Title} {step.Problem?.Fix}" + (step.Problem?.Detail is { } d ? $" (details: {d})" : ""),
            "working" => $"[ ] {label}: working on it...",
            _ when step.Note == "claim" => $"[ ] {label}: waiting for your claim",
            _ when step.Note == "moving" => $"[ ] {label}: the name moves to this server after its 24 hours",
            _ => $"[ ] {label}",
        };
    }

    private static string Next(IReadOnlyList<SetupStep> steps, string host, bool ownerExists)
    {
        var first = steps.First(x => x.State != "done");
        if (first.State == "failed") return first.Problem?.Fix ?? "see above.";
        return first.Id switch
        {
            "claimed" when first.Note == "claim" => $"open the claim link and sign in with Discord. No link, or it expired? Type kilnwarden setup hosted {PanelNames.LabelOf(host)}",
            "claimed" when first.Note == "moving" => "wait: the name switches to this server by itself.",
            "secure" or "ready" => ownerExists ? "wait about a minute, then type kilnwarden status again." : "wait about a minute, then type kilnwarden setup again.",
            _ => "wait a few seconds, then type kilnwarden status again.",
        };
    }

    /// <summary>The old name's copy is still installed (0.1.2: said in the console and on the panel's address).</summary>
    public static string OldCopy(string where) =>
        $"{Product.Name} is off: Kiln Web Admin, its old name, is still installed. Stop the server, delete the folder {where}, then start the server again. " +
        $"Your accounts and settings are kept: {Product.Name} moves them over.";
}
