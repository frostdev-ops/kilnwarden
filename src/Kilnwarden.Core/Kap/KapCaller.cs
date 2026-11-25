// SPDX-License-Identifier: MIT
using System.Text.RegularExpressions;

namespace Kilnwarden.Kap;

/// <summary>Who a KAP call is for. Every call names one, and the client stamps it into the request where Kiln.Admin
/// takes it (requirements: the command ops ALWAYS carry <c>source</c> "web:&lt;user&gt;" and <c>level</c>; ops with
/// <c>by</c> carry the user). A caller is either a signed-in web user (<see cref="ForUser"/>) or the panel itself
/// (<see cref="Panel"/>: its own bookkeeping, never a command op and never on behalf of a browser).</summary>
public sealed partial class KapCaller
{
    /// <summary>The longest Kiln.Admin accepts for <c>source</c> (Ops.CommandSource.MaxSourceName).</summary>
    public const int MaxSource = 80;

    private KapCaller(string? userName, CommandLevel level)
    {
        UserName = userName;
        Level = level;
    }

    /// <summary>The panel's own calls (admin_info, login_redeem, the catalog for its own gate): no user, no command level.</summary>
    public static KapCaller Panel { get; } = new(null, CommandLevel.None);

    /// <summary>A web user: <paramref name="userName"/> is the account's sign-in name (account names are
    /// [A-Za-z0-9._-]), <paramref name="level"/> the highest command level the user's roles give (None: no command op).</summary>
    public static KapCaller ForUser(string userName, CommandLevel level)
    {
        if (!SafeName().IsMatch(userName) || ("web:" + userName).Length > MaxSource)
            throw new ArgumentException($"'{userName}' cannot be a KAP source name");
        return new KapCaller(userName, level);
    }

    /// <summary>The account name, null for <see cref="Panel"/>.</summary>
    public string? UserName { get; }

    public CommandLevel Level { get; }

    public bool IsPanel => UserName is null;

    /// <summary>"web:&lt;user&gt;" (the command source and the <c>by</c> of restart and update ops); null for the panel.</summary>
    public string? Source => UserName is null ? null : "web:" + UserName;

    /// <summary>What ops with a <c>by</c> argument record: the user's source, or the product's id for the panel.</summary>
    public string By => Source ?? Product.ModId;

    public override string ToString() => IsPanel ? "panel" : $"{Source} (up to {CommandLevels.Name(Level)})";

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex SafeName();
}
