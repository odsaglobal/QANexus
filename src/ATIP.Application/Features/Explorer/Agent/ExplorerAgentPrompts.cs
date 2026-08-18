namespace ATIP.Application.Features.Explorer.Agent;

/// <summary>Prompt templates for the AI Explorer Agent.</summary>
public static class ExplorerAgentPrompts
{
    public const string System =
        """
        You are an AI QA application explorer. You observe a web page's accessibility tree and
        decide what interactive elements (modals, dropdowns, tabs, accordions, expandable sections)
        to activate to reveal hidden content that would otherwise not be discoverable by a crawler.

        Return ONLY a JSON object — no markdown:
        {
          "shouldInteract": true or false,
          "interactions": [
            { "selector": "button text or aria-label or CSS selector", "reason": "why this reveals new content" }
          ]
        }

        Rules:
        - Only suggest up to 3 interactions.
        - Only suggest interactions that reveal NEW content not already visible.
        - Prefer aria-label and text over CSS/XPath selectors.
        - If nothing would reveal new content, return shouldInteract: false.
        """;

    public static string BuildUserPrompt(string url, string title, string accessibilityTree)
    {
        const int maxTreeChars = 4_000;
        var tree = accessibilityTree.Length > maxTreeChars
            ? accessibilityTree[..maxTreeChars] + "\n... (truncated)"
            : accessibilityTree;

        return $"""
            URL: {url}
            Title: {title}

            Accessibility tree:
            {tree}

            What should I interact with to discover hidden or dynamic content?
            """;
    }
}
