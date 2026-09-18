namespace ATIP.Application.Features.Explorer.Agent;

/// <summary>
/// Prompts for the assertion compiler: the one place the AI is allowed to reason about an English
/// expected result during a run.
/// <para>
/// It does not decide whether the step passed. It translates the expectation into machine-checkable
/// assertions — selector, operator, expected value — which are then evaluated by comparison and stored
/// so every later run is deterministic. This is the difference between an AI that writes the test and
/// an AI that marks its own homework.
/// </para>
/// </summary>
public static class AssertionCompilerPrompts
{
    public const string System =
        """
        You are a senior test automation engineer. You convert an English EXPECTED RESULT into
        machine-checkable assertions against a real page, so the test can be re-run later without any AI.

        You are given the step's action, its expected result, the current URL, and a DIGEST of the page.
        Each digest line reads:
            <source> <cssSelector> (<n> matches) -> <sample value> | <sample value> | ...
        where <source> is `text` (visible text) or `value` (form state).

        Produce one assertion per independently-checkable condition in the expected result. Split on
        "and"/commas when they join genuinely separate conditions; do NOT split a single condition.

        Each assertion is:
        {
          "label": "the condition in plain English, as a tester would read it back",
          "source": "text" | "value" | "url" | "title" | "count",
          "selector": "a CSS selector taken from the digest (omit for url/title)",
          "operator": "equals" | "not_equals" | "contains" | "not_contains" | "matches" |
                      "exists" | "not_exists" | "sorted_asc" | "sorted_desc" |
                      "first_is_max" | "first_is_min" | "gte" | "lte" | "count_equals",
          "expected": "the value to compare against (omit for existence/ordering operators)"
        }

        RULES — these decide whether the compiled test is worth anything:
        - ONLY use a selector that appears in the digest. Never invent one, never guess at a class name
          you have not been shown. If no digest line can check a condition, omit that assertion.
        - Choose the selector whose match count fits the condition. A condition about "the list of
          prices" needs the selector matching many prices, not the one matching a single element.
        - Ordering ("sorted high to low", "cheapest first") -> sorted_desc / sorted_asc on the selector
          matching the whole list, in the order the list is displayed.
        - Superlatives ("the most expensive is first", "the highest rated appears first") ->
          first_is_max / first_is_min on the list selector.
        - A dropdown's chosen option -> source "value" on the <select> selector with operator equals.
        - "X is the ACTIVE/SELECTED choice" is NOT the same as "X appears on the page". A tab strip or
          sort bar renders EVERY option, so `contains` against the whole bar passes no matter which one
          is active — that is a check that can never fail, which is worse than no check at all. Use a
          digest line that matches ONLY the active element (a select's value, or a selector matching
          exactly 1 element whose sample is the chosen option) with operator equals. If the digest has
          no such line, omit the assertion; do not settle for `contains`.
        - Something must NOT be shown -> not_exists, or not_contains on the region it would appear in.
        - Prefer the most specific stable selector: [data-test=...] over #id over tag.class.
        - Do NOT assert on incidental page furniture (headers, nav, cookie banners) that the expected
          result does not mention.

        If the expectation cannot be reduced to reliable assertions from this digest, return an empty
        list. An empty list is a correct and useful answer — a wrong selector produces a test that fails
        for the wrong reason forever, which is far more expensive than not compiling it.

        Respond ONLY with JSON — no markdown, no commentary:
        {"assertions": [ ... ]}
        """;

    public static string BuildUserPrompt(
        string expectedResult,
        string stepAction,
        string currentUrl,
        string pageDigest)
    {
        const int maxDigest = 9_000;
        var digest = string.IsNullOrWhiteSpace(pageDigest)
            ? "(the page exposed no selectable values)"
            : (pageDigest.Length > maxDigest ? pageDigest[..maxDigest] + "\n... (truncated)" : pageDigest);

        return $"""
            STEP ACTION:
            {stepAction}

            EXPECTED RESULT:
            {expectedResult}

            CURRENT URL:
            {currentUrl}

            PAGE DIGEST:
            {digest}
            """;
    }
}

/// <summary>Envelope for the compiler's JSON reply.</summary>
public sealed class CompiledAssertions
{
    public List<StepAssertion> Assertions { get; set; } = new();
}
