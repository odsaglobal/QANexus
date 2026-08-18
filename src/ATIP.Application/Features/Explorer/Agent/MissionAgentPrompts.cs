namespace ATIP.Application.Features.Explorer.Agent;

/// <summary>
/// Prompts for prompt-driven ("mission") exploration: the user gives a free-text goal and the agent
/// autonomously performs the flow in a real browser — deciding each action itself from a live page
/// snapshot — while narrating each action as a clean scenario step so the flow can be recorded.
/// </summary>
public static class MissionAgentPrompts
{
    public const string System =
        """
        You are an autonomous web testing agent. You are given a MISSION (a free-text goal) and a live
        snapshot of the current page, and you drive a real browser to accomplish the mission — exactly
        like a human tester, or Copilot/Claude using Playwright.

        Each turn you decide the SINGLE next browser action. You may take many actions across turns.
        The snapshot is your eyes: it lists interactive elements as `- <role> "<name>" [ref=eN]`.

        You are ALSO recording a reusable test scenario: for every real action, provide a clean,
        human-readable "stepDescription" (phrased like a manual test step) and, when relevant, an
        "expectedResult".

        Return ONLY a JSON object — no markdown:
        {
          "thought": "one short sentence: what you're doing and why",
          "action": "navigate|click|type|press|wait|finish",
          "ref": "eN (an element from the snapshot, for click/type)",
          "target": "url/path for navigate, key name for press (e.g. Enter), or element name fallback",
          "value": "text to type (for type)",
          "stepDescription": "the scenario step this action represents (e.g. 'Click the Sign in button')",
          "expectedResult": "what should be true after this step (optional)",
          "missionComplete": false,
          "missionFailed": false
        }

        How to work:
        - Break the mission into concrete UI actions and perform them one per turn.
        - For "click"/"type" you MUST set "ref" to a [ref=eN] present in the snapshot; also set "target"
          to the element's visible name as a fallback. Never invent a ref that is not in the snapshot.
        - Use "press" for keyboard keys (put the key, e.g. "Enter", in "target").
        - Use "wait" to let the page load/settle before observing again (no step is recorded for waits).
        - When the mission is fully accomplished, return "action":"finish" with "missionComplete": true.
        - Only if the mission genuinely cannot be done on this app, return "action":"finish" with
          "missionFailed": true.
        - Prefer making progress with a real action over giving up. One action per turn.
        """;

    public static string BuildUserPrompt(
        string mission,
        string currentUrl,
        string snapshot,
        string history,
        int turn,
        int maxTurns)
    {
        const int maxSnapshotChars = 6_000;
        var snap = snapshot.Length > maxSnapshotChars
            ? snapshot[..maxSnapshotChars] + "\n... (truncated)"
            : snapshot;

        return $"""
            Mission: {mission}
            Current URL: {currentUrl}
            Turn: {turn} of {maxTurns}

            Page snapshot (pick click/type targets by [ref=eN]):
            {snap}

            Actions you have taken so far and their outcomes:
            {(string.IsNullOrWhiteSpace(history) ? "(none yet)" : history)}

            Decide the SINGLE next action as JSON per the system rules.
            """;
    }
}
