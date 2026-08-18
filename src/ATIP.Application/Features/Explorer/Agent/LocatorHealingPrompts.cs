using System.Text;

namespace ATIP.Application.Features.Explorer.Agent;

/// <summary>
/// Prompts for the self-healing locator engine. Given the semantic description of an element
/// (and optionally the scenario step that targets it) plus a slice of the live DOM, the model
/// proposes a corrected CSS or XPath selector that uniquely resolves the element.
/// </summary>
public static class LocatorHealingPrompts
{
    public const string System =
        """
        You are a self-healing test-automation locator engine. An element's stored locator no
        longer resolves on the page. Using the element's semantic description, the intended step
        (if provided), and the live DOM slice, produce ONE corrected locator that uniquely and
        robustly identifies the SAME element.

        Return ONLY a JSON object — no markdown, no commentary:
        { "strategy": "CSS" | "XPath", "value": "string", "confidence": 0.0-1.0 }

        Rules:
        - Prefer stable attributes: id, data-testid/data-test, name, aria-label, role+text.
        - Avoid brittle, auto-generated class names and absolute positional XPath when possible.
        - The selector MUST match exactly one element.
        - If you cannot find a reliable locator, return confidence 0.
        """;

    public static string BuildUserPrompt(
        string elementDescription,
        string? intendedStep,
        string domSlice)
    {
        const int maxDomChars = 8_000;
        var dom = domSlice.Length > maxDomChars ? domSlice[..maxDomChars] + "\n... (truncated)" : domSlice;

        var sb = new StringBuilder();
        sb.AppendLine("Element to locate:");
        sb.AppendLine(elementDescription);
        if (!string.IsNullOrWhiteSpace(intendedStep))
        {
            sb.AppendLine();
            sb.AppendLine($"Intended test step: {intendedStep}");
        }

        sb.AppendLine();
        sb.AppendLine("Live DOM slice:");
        sb.AppendLine("\"\"\"");
        sb.AppendLine(dom);
        sb.AppendLine("\"\"\"");
        sb.AppendLine();
        sb.AppendLine("Return the corrected locator as JSON per the system prompt.");
        return sb.ToString();
    }
}
