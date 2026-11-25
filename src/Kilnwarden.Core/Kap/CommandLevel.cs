// SPDX-License-Identifier: MIT
namespace Kilnwarden.Kap;

/// <summary>The highest Kiln command level a web user's lines may use (KAP command_run/command_complete/
/// commands_describe <c>level</c>, admin-protocol.md §11). Ordered: a higher value may do everything a lower one may.
/// <see cref="None"/> is the panel's own: no command op at all.</summary>
public enum CommandLevel
{
    None = 0,
    Everyone = 1,
    Admin = 2,
    Console = 3,
}

public static class CommandLevels
{
    /// <summary>The KAP wire name ("everyone", "admin", "console"); throws for <see cref="CommandLevel.None"/>, which is
    /// never sent (a caller without a level cannot run command ops).</summary>
    public static string Wire(CommandLevel level) => level switch
    {
        CommandLevel.Everyone => "everyone",
        CommandLevel.Admin => "admin",
        CommandLevel.Console => "console",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "no command level"),
    };

    /// <summary>Parses "none", "everyone", "admin", "console" (lower case only).</summary>
    public static bool TryParse(string? s, out CommandLevel level)
    {
        level = s switch
        {
            "none" => CommandLevel.None,
            "everyone" => CommandLevel.Everyone,
            "admin" => CommandLevel.Admin,
            "console" => CommandLevel.Console,
            _ => (CommandLevel)(-1),
        };
        return (int)level >= 0;
    }

    public static string Name(CommandLevel level) => level == CommandLevel.None ? "none" : Wire(level);
}
