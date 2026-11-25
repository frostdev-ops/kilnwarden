// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Kilnwarden.PublicPage;

/// <summary>The public page's rules and join info as markdown (DESIGN §5): a small, safe subset. Blocks: paragraphs
/// (blank lines), "# " and "## " headings, "- "/"* " and "1. " lists. Inline: **bold**, *italic* or _italic_, `code`,
/// [text](https://...) links. Everything else is text, HTML-encoded; there is no raw HTML and no URL scheme but https
/// (and http). A fuzz target.</summary>
public static partial class PublicMarkdown
{
    public static string Render(string? text)
    {
        var sb = new StringBuilder();
        if (string.IsNullOrEmpty(text)) return "";
        foreach (var block in text.Replace("\r\n", "\n").Replace('\r', '\n').Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var lines = block.Split('\n');
            var i = 0;
            while (i < lines.Length)
            {
                var line = lines[i].TrimStart();
                if (line.StartsWith("## ", StringComparison.Ordinal)) { sb.Append("<h4>").Append(Inline(line[3..].Trim())).Append("</h4>"); i++; continue; }
                if (line.StartsWith("# ", StringComparison.Ordinal)) { sb.Append("<h3>").Append(Inline(line[2..].Trim())).Append("</h3>"); i++; continue; }
                if (Bullet(line) is not null)
                {
                    sb.Append("<ul>");
                    while (i < lines.Length && Bullet(lines[i].TrimStart()) is { } item) { sb.Append("<li>").Append(Inline(item)).Append("</li>"); i++; }
                    sb.Append("</ul>");
                    continue;
                }
                if (Numbered(line) is not null)
                {
                    sb.Append("<ol>");
                    while (i < lines.Length && Numbered(lines[i].TrimStart()) is { } item) { sb.Append("<li>").Append(Inline(item)).Append("</li>"); i++; }
                    sb.Append("</ol>");
                    continue;
                }
                var para = new List<string>();
                while (i < lines.Length && !IsBlockStart(lines[i].TrimStart())) para.Add(lines[i++].Trim());
                sb.Append("<p>").Append(string.Join("<br>", para.Select(Inline))).Append("</p>");
            }
        }
        return sb.ToString();
    }

    private static bool IsBlockStart(string line) =>
        line.StartsWith("# ", StringComparison.Ordinal) || line.StartsWith("## ", StringComparison.Ordinal) || Bullet(line) is not null || Numbered(line) is not null;

    private static string? Bullet(string line) => line.Length > 2 && (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal)) ? line[2..].Trim() : null;

    private static string? Numbered(string line) => NumberedRe().Match(line) is { Success: true } m ? m.Groups[1].Value.Trim() : null;

    private static string E(string s) => WebUtility.HtmlEncode(s);

    /// <summary>Inline markup over raw text: the earliest token wins, the text around it is encoded.</summary>
    private static string Inline(string s)
    {
        var sb = new StringBuilder();
        var pos = 0;
        while (pos < s.Length)
        {
            var m = InlineRe().Match(s, pos);
            if (!m.Success) { sb.Append(E(s[pos..])); break; }
            sb.Append(E(s[pos..m.Index]));
            if (m.Groups["code"].Success) sb.Append("<code>").Append(E(m.Groups["code"].Value)).Append("</code>");
            else if (m.Groups["bold"].Success) sb.Append("<strong>").Append(E(m.Groups["bold"].Value)).Append("</strong>");
            else if (m.Groups["em"].Success) sb.Append("<em>").Append(E(m.Groups["em"].Value)).Append("</em>");
            else if (m.Groups["em2"].Success) sb.Append("<em>").Append(E(m.Groups["em2"].Value)).Append("</em>");
            else if (m.Groups["url"].Success && Uri.TryCreate(m.Groups["url"].Value, UriKind.Absolute, out var u) && u.Scheme is "https" or "http")
                sb.Append("<a href=\"").Append(E(m.Groups["url"].Value)).Append("\" rel=\"nofollow noopener noreferrer\">").Append(E(m.Groups["label"].Value)).Append("</a>");
            else sb.Append(E(m.Value));
            pos = m.Index + Math.Max(1, m.Length);
        }
        return sb.ToString();
    }

    [GeneratedRegex(@"^\d{1,3}[.)] (.+)$")]
    private static partial Regex NumberedRe();

    [GeneratedRegex(@"`(?<code>[^`\n]{1,200})`|\*\*(?<bold>[^*\n]{1,200})\*\*|\*(?<em>[^*\n]{1,200})\*|(?<![A-Za-z0-9])_(?<em2>[^_\n]{1,200})_(?![A-Za-z0-9])|\[(?<label>[^\]\n]{1,200})\]\((?<url>https?://[^\s)]{1,500})\)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex InlineRe();
}
