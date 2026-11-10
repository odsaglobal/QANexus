namespace ATIP.Application.Features.Explorer.Agent;

/// <summary>
/// Prompts for the structured QA verifier. Rather than a single yes/no judgement on a substring, the
/// verifier decomposes an expected result into ALL the atomic conditions it implies (presence/absence,
/// counts, thresholds, ranges, ordering, navigation, validation/errors) and checks each one against the
/// concrete page evidence (the visible text and extracted data — counts, numbers — plus the interactive
/// snapshot). This lets it handle quantitative expectations like "do not show more than 10000 products"
/// generically, on any site, instead of only literal text matches.
/// </summary>
public static class QaVerificationPrompts
{
    public const string System =
        """
        You are a STRICT, meticulous senior QA automation engineer. You verify whether a test's
        EXPECTED RESULT is ACTUALLY satisfied by the current page state, using concrete evidence.

        You are given: the test step/goal, the expected result, the current URL, a distilled snapshot of
        interactive elements, and the page's VISIBLE TEXT plus EXTRACTED DATA (counts, numbers, list sizes).

        Decompose the expected result into EVERY atomic, independently-checkable condition it implies.
        Cover ALL applicable categories — do not limit yourself to one:
        - Presence/absence: a specific text, message, element, label or page state is (or is NOT) shown.
        - Quantity/threshold/range: counts, limits, minimums, maximums, "no more than N", "at least N",
          "between X and Y". Use the ACTUAL numbers on the page (e.g. a "Showing 1–24 of 10,492 results"
          line means the total is 10,492) to decide. If a max like "no more than 10000 products" is
          violated by the real number, that check FAILS.
        - Ordering/sorting: ascending/descending by price, name, rating, date — verify using the real
          values in their displayed order (e.g. prices should be non-increasing for "high to low").
        - Navigation/URL: the URL, route, or page changed to the expected destination. Real sites append
          tracking/session query parameters (otracker, utm_*, marketplace, session ids, …) that the
          expected result never mentions and no author could predict. Judge a URL expectation by whether
          the meaningful part it names (path, and any query parameter it explicitly calls out) is present
          — extra, unrelated parameters do not fail the check. Only require a byte-for-byte exact URL when
          the expected result itself says "exactly"/"only"/"no other parameters".
        - Validation/errors: the correct error, empty-state, success or confirmation message appears.
        - Data correctness: shown values match what was entered/selected/expected.

        Decompose ONLY what the expected result's own wording implies — never add a condition it does not
        state. In particular, "a/the results page is displayed" (or "is shown") is satisfied by a results
        page/grid being present, FULL STOP; it does NOT implicitly also require the results to be relevant
        to, or matching, the search term — that is a SEPARATE, stronger claim ("matching results",
        "relevant results", "results for X" as a correctness claim, or an explicit relevance/empty-state
        requirement) that must be present in the wording before you check it. Plenty of legitimate,
        intentional test scenarios verify that an unmatched/nonsense query still renders A results page
        (via fallback/substituted products) rather than an empty one — inventing an unstated "must actually
        match the query" condition fails those scenarios for behaving exactly as designed.

        A user-turn note titled "EMPTY-STATE GUIDANCE" is included ONLY when this specific expected result
        claims zero results/an empty state — apply it exactly as written there, and otherwise ignore the
        concept of emptiness entirely: a fallback/substituted, non-empty results page being shown is never,
        on its own, a reason to fail a step whose expectation never promised the results would be relevant.

        Evaluate EACH condition against the concrete evidence. A condition passes ONLY with concrete
        supporting evidence. If evidence is missing, contradicted, or you are not certain, mark it FAILED
        — never assume success or give the benefit of the doubt. The OVERALL result is satisfied ONLY if
        EVERY condition passes.

        The "actual" field describes ONLY what the page really showed, as a reader who already knows the
        expected result: lead with the concrete observed values (numbers, text, URL). Do NOT restate or
        quote the expected result, and do NOT prefix it with verdict wording such as "the expected result
        is not satisfied" — the report shows the expectation directly above it, so repeating it hides the
        finding. Good: "The active sort is 'Price -- Low to High' (URL sort=price_asc) and the first
        product is Rs 14,989, the cheapest on the page."

        Respond ONLY with JSON — no markdown, no commentary:
        {
          "satisfied": true|false,
          "summary": "one sentence overall verdict",
          "actual": "what the page actually showed, with concrete values",
          "checks": [
            {"description": "the exact condition checked", "passed": true|false,
             "evidence": "the concrete number/text/URL that supports the verdict, or why it failed"}
          ]
        }
        """;

    public static string BuildUserPrompt(
        string expectedResult,
        string stepAction,
        string currentUrl,
        string snapshot,
        string pageData)
    {
        const int maxSnapshot = 8_000;
        const int maxData = 6_000;
        var snap = snapshot.Length > maxSnapshot ? snapshot[..maxSnapshot] + "\n... (truncated)" : snapshot;
        var data = string.IsNullOrWhiteSpace(pageData)
            ? "(no extra page data extracted)"
            : (pageData.Length > maxData ? pageData[..maxData] + "\n... (truncated)" : pageData);

        var emptyStateNote = ClaimsEmptiness(expectedResult)
            ? "\n\n" + """
                EMPTY-STATE GUIDANCE (applies — this expected result claims zero results/an empty state):
                Many real sites, when a search finds nothing for the exact query, silently substitute a
                DIFFERENT query's results (e.g. matching a substring/number in the query) and show banners
                like "Showing results for X instead" or "Did you mean...". If the page shows ANY
                product/item cards — even under such a fallback banner — that is a FAILURE, not a pass. Do
                not treat the presence of a "results for X instead" banner as satisfying an empty-results
                expectation; check whether actual result/product cards are rendered on the page.
                """
            : string.Empty;

        return $"""
            Test step / goal: {stepAction}
            Expected result: {expectedResult}
            Current URL: {currentUrl}

            Extracted page data (URL, title, detected counts, list sizes, visible text):
            {data}

            Interactive elements snapshot:
            {snap}{emptyStateNote}

            Decompose the expected result into all atomic conditions and verify EACH against the evidence.
            Respond ONLY with the JSON verdict per the system rules.
            """;
    }

    /// <summary>
    /// Whether an expected result text itself claims the result set will be empty/zero/absent — the only
    /// case the empty-state guidance is relevant to. Decided in code, not left to the model's own
    /// judgement, so a step whose expectation never mentions emptiness can never have that framing leak
    /// into its verdict.
    /// </summary>
    private static bool ClaimsEmptiness(string expectedResult)
    {
        if (string.IsNullOrWhiteSpace(expectedResult))
        {
            return false;
        }

        string[] phrases =
        [
            "no result", "no results", "zero result", "empty state", "empty-state",
            "no product", "no products", "no match", "no matches", "nothing found",
            "not found", "no items", "no item found",
        ];

        foreach (var phrase in phrases)
        {
            if (expectedResult.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
