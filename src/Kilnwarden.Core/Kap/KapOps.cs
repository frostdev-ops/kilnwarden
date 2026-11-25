// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Kilnwarden.Kap;

/// <summary>Kiln.Admin ops the client itself treats specially, whoever the caller is (defence in depth: the web gate
/// refuses these too).</summary>
public static partial class KapOps
{
    /// <summary>The ops that run a command line or describe commands: Kiln.Admin runs them as the full-admin
    /// "dashboard" unless <c>source</c> and <c>level</c> are given, so the client always sets both from the caller and
    /// refuses a caller without a level (requirements, 30.9 review-2). The one frame not stamped here is the client's
    /// connect-time capability probe (KapClient.MissingAsync): commands_describe with source "web:&lt;mod id&gt;" and a
    /// level no Kiln.Admin accepts, so a 30.9 Kiln.Admin refuses it and an older one (which ignores both) is detected.</summary>
    public static readonly IReadOnlySet<string> CommandOps = new HashSet<string>(StringComparer.Ordinal)
    {
        "command_run", "command_complete", "commands_describe",
    };

    /// <summary>Never sent for anyone: <c>command</c> (the old console op: always full admin, no source or level; use
    /// command_run), <c>login_mint</c> (mints owner logins: the launcher's, never the panel's) and
    /// <c>admin_token_rotate</c> (the launcher's; it would cut every other KAP client off).</summary>
    public static readonly IReadOnlySet<string> Never = new HashSet<string>(StringComparer.Ordinal)
    {
        "command", "login_mint", "admin_token_rotate",
    };

    /// <summary>Only the panel's own code may send these (never on behalf of a browser): <c>login_redeem</c> turns a
    /// launcher's one-time secret into an owner session.</summary>
    public static readonly IReadOnlySet<string> PanelOnly = new HashSet<string>(StringComparer.Ordinal)
    {
        "login_redeem",
    };

    /// <summary>Ops that record who asked in <c>by</c>: the client sets it to the caller ("web:&lt;user&gt;").</summary>
    public static readonly IReadOnlySet<string> ByOps = new HashSet<string>(StringComparer.Ordinal)
    {
        "server_restart", "server_restart_cancel", "update_stage", "update_cancel", "update_rollback",
    };

    /// <summary>An op's budget in seconds (admin-protocol.md §4).</summary>
    public static int BudgetS(string op) => op switch
    {
        "mod_test" or "update_stage" => 180,
        "backup_create" => 90,
        "save_now" => 60,
        _ when op.StartsWith("mod_", StringComparison.Ordinal) => 45,
        _ => 30,
    };

    /// <summary>What an op name may be: lower-case letters, digits and '_', starting with a letter.</summary>
    public static bool IsOpName(string? s) => s is { Length: > 0 and <= 64 } && OpRe().IsMatch(s);

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex OpRe();

    /// <summary>The arguments as they go to Kiln.Admin for <paramref name="caller"/>: a copy of
    /// <paramref name="args"/> with source/level (command ops) and by (restart/update ops) set from the caller, whatever
    /// the args said. Throws <see cref="KapRefusedException"/> for ops the caller may never send.</summary>
    public static JsonObject Stamp(KapCaller caller, string op, JsonObject? args)
    {
        if (!IsOpName(op)) throw new KapRefusedException(op, "bad op name");
        if (Never.Contains(op)) throw new KapRefusedException(op, $"{op} is never sent by the web panel");
        if (PanelOnly.Contains(op) && !caller.IsPanel) throw new KapRefusedException(op, $"{op} is the panel's own");
        var a = args is null ? new JsonObject() : (JsonObject)args.DeepClone();
        // source/level are identity only for the command ops and by only for ByOps (both overwritten below). Other ops
        // use these names as data (give_item's item level, build_shape's flatten level, update_stage's incoming
        // folder), so they pass through; an op the gate does not list never reaches here from a browser.
        if (CommandOps.Contains(op))
        {
            if (caller.IsPanel || caller.Level == CommandLevel.None)
                throw new KapRefusedException(op, $"{op} needs a web user with a command level");
            a.Remove("source");
            a.Remove("level");
            a["source"] = caller.Source;
            a["level"] = CommandLevels.Wire(caller.Level);
        }
        if (ByOps.Contains(op))
        {
            a.Remove("by");
            a["by"] = caller.By;
        }
        return a;
    }
}

/// <summary>A call the client refuses to send (<see cref="KapOps.Stamp"/>).</summary>
public sealed class KapRefusedException(string op, string message) : Exception(message)
{
    public string Op { get; } = op;
}
