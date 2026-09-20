using System.Text.Json;
using System.Text.RegularExpressions;

namespace ATIP.Application.Common.Utilities;

/// <summary>
/// Helpers for recovering a JSON payload from an LLM response that may wrap it in markdown code
/// fences, a reasoning-model "thinking" preamble, or surrounding prose. Different providers/models
/// format their replies very differently (e.g. DeepSeek-R1/QwQ-style reasoning models emit a
/// &lt;think&gt;…&lt;/think&gt; block before the answer; some models echo the question back first) —
/// this class keeps parsing tolerant of that so the engine isn't tuned to one model's output style.
/// </summary>
public static class JsonExtraction
{
    private static readonly Regex ReasoningBlockPattern =
        new(@"(?is)<(think|thinking|reasoning)>.*?</\1>", RegexOptions.Compiled);

    /// <summary>
    /// Returns the first balanced, actually-parseable top-level JSON object or array found in
    /// <paramref name="text"/> — scanning past any leading reasoning/prose/code-fence noise instead of
    /// naively slicing between the first and last bracket (which breaks when the surrounding prose
    /// itself contains braces, common in more verbose/local models). Falls back to a naive slice, then
    /// the trimmed input, when no candidate parses (e.g. the response was truncated mid-JSON).
    /// </summary>
    public static string ExtractJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "{}";
        }

        var trimmed = StripCodeFences(StripReasoningBlocks(text.Trim()));

        var balanced = FindBalancedJson(trimmed);
        if (balanced is not null)
        {
            return balanced;
        }

        var start = trimmed.IndexOfAny(['{', '[']);
        var end = trimmed.LastIndexOfAny(['}', ']']);
        return start >= 0 && end > start ? trimmed[start..(end + 1)] : trimmed;
    }

    /// <summary>Drops any &lt;think&gt;/&lt;thinking&gt;/&lt;reasoning&gt; block some reasoning models
    /// prepend before their actual answer.</summary>
    private static string StripReasoningBlocks(string text) =>
        ReasoningBlockPattern.Replace(text, string.Empty).Trim();

    /// <summary>Strips a leading ```json / ``` fence (and its matching closing fence, if present).</summary>
    private static string StripCodeFences(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstNewline = text.IndexOf('\n');
        if (firstNewline < 0)
        {
            return text;
        }

        var body = text[(firstNewline + 1)..];
        var closingFence = body.LastIndexOf("```", StringComparison.Ordinal);
        if (closingFence >= 0)
        {
            body = body[..closingFence];
        }

        return body.Trim();
    }

    /// <summary>
    /// Scans left-to-right for a '{'/'[' that opens a bracket-balanced span (honoring quoted strings and
    /// escapes) which also parses as valid JSON, skipping over any candidate that doesn't (e.g. an
    /// example snippet mentioned in reasoning prose before the real answer).
    /// </summary>
    private static string? FindBalancedJson(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '{' && text[i] != '[')
            {
                continue;
            }

            var end = FindMatchingClose(text, i);
            if (end is null)
            {
                continue;
            }

            var candidate = text[i..(end.Value + 1)];
            if (IsValidJson(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Stack-based bracket matcher so nested '{'/'[' of the other kind (and brackets inside
    /// quoted strings) don't desync a same-character depth counter.</summary>
    private static int? FindMatchingClose(string text, int start)
    {
        var stack = new Stack<char>();
        var inString = false;
        var escaped = false;

        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    stack.Push('}');
                    break;
                case '[':
                    stack.Push(']');
                    break;
                case '}':
                case ']':
                    if (stack.Count == 0 || stack.Peek() != c)
                    {
                        return null;
                    }

                    stack.Pop();
                    if (stack.Count == 0)
                    {
                        return i;
                    }

                    break;
            }
        }

        return null;
    }

    private static bool IsValidJson(string candidate)
    {
        try
        {
            using var _ = JsonDocument.Parse(candidate);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
