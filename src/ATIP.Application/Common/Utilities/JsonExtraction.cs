namespace ATIP.Application.Common.Utilities;

/// <summary>
/// Helpers for recovering a JSON payload from an LLM response that may wrap it in markdown
/// code fences or surrounding prose. Keeps parsing tolerant of imperfect model output.
/// </summary>
public static class JsonExtraction
{
    /// <summary>
    /// Returns the substring spanning the first balanced top-level JSON object or array found in
    /// <paramref name="text"/>. Falls back to the trimmed input when no braces are present.
    /// </summary>
    public static string ExtractJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "{}";
        }

        var trimmed = text.Trim();

        // Strip ```json ... ``` or ``` ... ``` fences.
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
            {
                trimmed = trimmed[(firstNewline + 1)..];
            }

            var closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (closingFence >= 0)
            {
                trimmed = trimmed[..closingFence];
            }

            trimmed = trimmed.Trim();
        }

        var start = trimmed.IndexOfAny(['{', '[']);
        var end = trimmed.LastIndexOfAny(['}', ']']);

        if (start >= 0 && end > start)
        {
            return trimmed[start..(end + 1)];
        }

        return trimmed;
    }
}
