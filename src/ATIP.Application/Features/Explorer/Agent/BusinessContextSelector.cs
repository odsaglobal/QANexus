using System.Text;
using System.Text.RegularExpressions;

namespace ATIP.Application.Features.Explorer.Agent;

/// <summary>
/// Picks the parts of the project's business-context documents that bear on the current step, so a
/// rule written on page 9 of a spec reaches the agent instead of being cut off by a head-only budget.
/// The agent is told to ask the user only when the context does not settle a question, which is only
/// fair if the relevant context was actually shown to it.
/// </summary>
public static class BusinessContextSelector
{
    private static readonly Regex Word = new(@"[\p{L}\p{N}]{3,}", RegexOptions.CultureInvariant);

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "that", "this", "from", "into", "are", "was", "were", "has", "have",
        "page", "click", "enter", "type", "select", "should", "displayed", "shown", "user", "then", "when",
        "step", "expected", "result", "button", "field", "link", "results", "verify", "check", "its", "not",
    };

    public static string Select(string? context, string? query, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return string.Empty;
        }

        if (context.Length <= maxChars)
        {
            return context;
        }

        var terms = Terms(query);
        var blocks = SplitBlocks(context);

        var chosen = blocks
            .Select((text, index) => (text, index, score: Score(text, terms)))
            .OrderByDescending(b => b.score)
            .ThenBy(b => b.index)
            .ToList();

        var picked = new List<(string text, int index)>();
        var used = 0;
        foreach (var block in chosen)
        {
            if (used + block.text.Length > maxChars)
            {
                continue;
            }

            picked.Add((block.text, block.index));
            used += block.text.Length + 6;
        }

        if (picked.Count == 0)
        {
            return context[..maxChars] + "\n... (truncated)";
        }

        var output = new StringBuilder();
        var previous = -1;
        foreach (var (text, index) in picked.OrderBy(p => p.index))
        {
            if (previous >= 0 && index != previous + 1)
            {
                output.AppendLine("…");
            }

            output.AppendLine(text);
            previous = index;
        }

        return output.ToString().TrimEnd();
    }

    private static HashSet<string> Terms(string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : Word.Matches(query)
                .Select(m => m.Value.ToLowerInvariant())
                .Where(w => !StopWords.Contains(w))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static int Score(string block, HashSet<string> terms)
    {
        if (terms.Count == 0)
        {
            return 0;
        }

        var words = Word.Matches(block).Select(m => m.Value.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        return terms.Count(words.Contains);
    }

    // Paragraphs, with a markdown heading kept attached to the paragraph under it.
    private static List<string> SplitBlocks(string context)
    {
        var paragraphs = Regex.Split(context.Replace("\r\n", "\n"), @"\n\s*\n")
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        var blocks = new List<string>();
        for (var i = 0; i < paragraphs.Count; i++)
        {
            var p = paragraphs[i];
            if (p.StartsWith('#') && !p.Contains('\n') && i + 1 < paragraphs.Count)
            {
                p = $"{p}\n{paragraphs[++i]}";
            }

            blocks.Add(p);
        }

        return blocks;
    }
}
