namespace ATIP.Application.Features.Explorer.Agent;

/// <summary>
/// Prompts that turn a manual test step into a concrete browser action so the explorer can walk
/// a scenario (navigate, click, type) and discover the elements involved along the way.
/// </summary>
public static class StepInterpretationPrompts
{
    public const string System =
        """
        You convert one natural-language test step into a single concrete browser action, using
        the current page's visible interactive elements to ground your choice of target.

        Return ONLY a JSON object — no markdown:
        { "action": "navigate|click|type|assert|none", "target": "url/path or the EXACT visible text/label of the element", "value": "text to type" }

        Rules:
        - "navigate": target is a URL or path (e.g. /login).
        - "click": target MUST be the exact visible text, aria-label, or role name of one of the listed
          elements. Do not invent targets that are not in the list.
        - "type": target is the field's label/placeholder/aria-label (prefer one from the list); value is
          the text to enter (use a realistic sample value if the step doesn't specify one).
        - "assert" or "none": when the step only verifies state and needs no interaction.
        - If the step asks to close/dismiss a popup or modal, and no such control is listed, return action "none".
        - Prefer human-readable targets (text / aria-label) over CSS. Choose the closest matching listed element.
        """;

    public static string BuildUserPrompt(string stepAction, string? stepExpected, string currentUrl, string accessibilityTree)
        => BuildUserPrompt(stepAction, stepExpected, currentUrl, accessibilityTree, null);

    public static string BuildUserPrompt(
        string stepAction,
        string? stepExpected,
        string currentUrl,
        string accessibilityTree,
        string? interactiveElements)
    {
        const int maxTreeChars = 3_000;
        var tree = accessibilityTree.Length > maxTreeChars ? accessibilityTree[..maxTreeChars] + "\n... (truncated)" : accessibilityTree;

        var elementsBlock = string.IsNullOrWhiteSpace(interactiveElements)
            ? ""
            : $"""

            Visible interactive elements (choose the target from these when clicking/typing):
            {interactiveElements}
            """;

        return $"""
            Current URL: {currentUrl}
            Test step: {stepAction}
            {(string.IsNullOrWhiteSpace(stepExpected) ? "" : $"Expected result: {stepExpected}")}
            {elementsBlock}
            Accessibility tree (context):
            {tree}

            Convert the step into a single action as JSON per the system prompt.
            """;
    }

}
