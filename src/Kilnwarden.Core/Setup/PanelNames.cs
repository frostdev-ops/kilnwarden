// SPDX-License-Identifier: MIT
namespace Kilnwarden.Setup;

/// <summary>The names hosted access gives out: <c>&lt;name&gt;.kiln.nexus</c>. The same rules as the relay and the claim
/// pages (a copy of the relay's own rules, kept by hand: the relay refuses anything else anyway), so the setup wizard
/// and <c>kilnwarden setup hosted</c> can say what is wrong before the owner goes to Discord.</summary>
public static class PanelNames
{
    /// <summary>The zone frostdev.io's relay serves panels under.</summary>
    public const string Zone = "kiln.nexus";

    public const int MinLength = 3, MaxLength = 40;

    /// <summary>Labels nobody may claim (kwa-common names.rs RESERVED; the operator can ban more).</summary>
    public static readonly IReadOnlySet<string> Reserved = new HashSet<string>(StringComparer.Ordinal)
    {
        "www", "api", "app", "admin", "administrator", "root", "relay", "panel", "panels", "mail", "smtp", "imap", "pop", "ftp", "ns", "ns1", "ns2",
        "dns", "kiln", "frostdev", "official", "staff", "team", "support", "help", "security", "abuse", "status", "docs", "test", "testing",
        "localhost", "local", "example", "invalid", "acme", "cdn", "static", "assets", "auth", "login", "signin", "account", "billing",
        "discord", "steam", "nexus", "enshrouded", "keen", "keengames", "moderator", "owner", "wiki", "dev", "beta", "blog", "forum", "news",
        "shop", "store", "download", "downloads", "launcher", "webadmin", "broker", "mods", "claim",
    };

    /// <summary>What the owner typed, as a label: trimmed, lower case, and with ".kiln.nexus" (or "https://" and a
    /// slash) taken off when they pasted the whole address.</summary>
    public static string Normalize(string? typed)
    {
        var s = (typed ?? "").Trim().ToLowerInvariant();
        if (s.StartsWith("https://", StringComparison.Ordinal)) s = s[8..];
        else if (s.StartsWith("http://", StringComparison.Ordinal)) s = s[7..];
        s = s.TrimEnd('/');
        if (s.EndsWith("." + Zone, StringComparison.Ordinal)) s = s[..^(Zone.Length + 1)];
        return s;
    }

    /// <summary>Null when <paramref name="label"/> can be claimed, else what is wrong, in plain words (one sentence).</summary>
    public static string? Problem(string label)
    {
        if (label.Length == 0) return "Type a name.";
        if (label.Length < MinLength) return $"Use at least {MinLength} characters.";
        if (label.Length > MaxLength) return $"Use at most {MaxLength} characters.";
        if (label.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))) return "Use only letters, digits and dashes.";
        if (label[0] == '-' || label[^1] == '-') return "Start and end with a letter or a digit.";
        if (label.Length >= 4 && label[2] == '-' && label[3] == '-') return "Don't put two dashes in the third and fourth places.";
        if (Reserved.Contains(label)) return $"\"{label}\" is kept for Kiln itself. Pick another name.";
        return null;
    }

    /// <summary>The full host for a label under <paramref name="zone"/>.</summary>
    public static string Host(string label, string zone = Zone) => $"{label}.{zone}";

    /// <summary>The label of <paramref name="host"/> when it is one label under a zone ("myserver.kiln.nexus": "myserver").</summary>
    public static string? LabelOf(string? host) =>
        host is { Length: > 0 } h && h.IndexOf('.', StringComparison.Ordinal) is > 0 and var dot ? h[..dot] : null;

    /// <summary>The zone of <paramref name="host"/> ("myserver.kiln.nexus": "kiln.nexus").</summary>
    public static string? ZoneOf(string? host) =>
        host is { Length: > 0 } h && h.IndexOf('.', StringComparison.Ordinal) is > 0 and var dot && h[(dot + 1)..].Contains('.') ? h[(dot + 1)..] : null;
}
