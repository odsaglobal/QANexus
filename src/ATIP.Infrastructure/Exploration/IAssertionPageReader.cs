namespace ATIP.Infrastructure.Exploration;

/// <summary>
/// The narrow slice of a browser that compiled assertions need: where the page is, what it is called,
/// and the ability to read rendered values out of it.
/// <para>
/// It exists so that assertion compilation and evaluation are not tied to one browser backend. EXPLORE
/// drives the page through the Playwright MCP server while RUN drives it through Playwright directly;
/// if only the latter could evaluate assertions, an exploration could never produce them, and every
/// first RUN would have to re-derive its checks with the AI. Both backends implement this, so an
/// expectation compiled during exploration is the same object the deterministic run later replays.
/// </para>
/// </summary>
public interface IAssertionPageReader
{
    /// <summary>The URL currently displayed.</summary>
    string CurrentUrl { get; }

    /// <summary>The document title currently displayed.</summary>
    string CurrentTitle { get; }

    /// <summary>
    /// Reads the rendered text (or form value) of every element matching <paramref name="selector"/>.
    /// </summary>
    /// <param name="source">"text" to read textContent, "value" to read form values.</param>
    /// <returns>
    /// The matched values, or <c>null</c> when the page could not be queried at all (a malformed
    /// selector). A malformed selector is a defect in the test; matching nothing is a finding about the
    /// application, so the two are reported distinctly.
    /// </returns>
    Task<IReadOnlyList<string>?> ReadValuesAsync(
        string selector,
        string source = "text",
        int max = 200,
        CancellationToken ct = default);

    /// <summary>
    /// Builds a digest of the page's value-bearing elements — stable selector, match count and sample
    /// values — as the evidence an expectation is compiled against.
    /// </summary>
    Task<string> GetAssertionContextAsync(int maxGroups = 120, CancellationToken ct = default);
}
