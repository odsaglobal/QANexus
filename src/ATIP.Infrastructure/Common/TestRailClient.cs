using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Models;
using ATIP.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ATIP.Infrastructure.Common;

public sealed class TestRailClient : ITestRailClient
{
    private const string HttpClientName = "testrail";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TestRailOptions _options;
    private readonly ILogger<TestRailClient> _logger;

    public TestRailClient(
        IHttpClientFactory httpClientFactory,
        IOptions<TestRailOptions> options,
        ILogger<TestRailClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ImportedTestScenario>> GetCasesAsync(
        int testRailProjectId,
        int? suiteId,
        int? sectionId,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException("TestRail is not configured. Set TestRail:BaseUrl, TestRail:Username and TestRail:ApiKey.");
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var endpoint = BuildEndpoint(testRailProjectId, suiteId, sectionId);

        using var response = await client.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        using var json = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 256 });

        if (!json.RootElement.TryGetProperty("cases", out var casesElement)
            || casesElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var scenarios = new List<ImportedTestScenario>();
        foreach (var caseElement in casesElement.EnumerateArray())
        {
            var title = TryGetString(caseElement, "title");
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var preconditions = TryGetString(caseElement, "custom_preconds");
            var expected = TryGetString(caseElement, "custom_expected");
            var externalRef = TryGetInt(caseElement, "id")?.ToString();

            var steps = ParseSteps(caseElement);
            if (steps.Count == 0)
            {
                _logger.LogDebug("Skipping TestRail case without actionable steps: {Title}", title);
                continue;
            }

            scenarios.Add(new ImportedTestScenario(
                Title: StripHtml(title)!,
                Preconditions: StripHtml(preconditions),
                ExpectedResult: StripHtml(expected),
                Steps: steps,
                ExternalReference: externalRef));
        }

        return scenarios;
    }

    private static string BuildEndpoint(int projectId, int? suiteId, int? sectionId)
    {
        var sb = new StringBuilder($"index.php?/api/v2/get_cases/{projectId}");
        if (suiteId.HasValue)
        {
            sb.Append($"&suite_id={suiteId.Value}");
        }

        if (sectionId.HasValue)
        {
            sb.Append($"&section_id={sectionId.Value}");
        }

        return sb.ToString();
    }

    private static List<ImportedTestStep> ParseSteps(JsonElement caseElement)
    {
        if (caseElement.TryGetProperty("custom_steps_separated", out var separated)
            && separated.ValueKind == JsonValueKind.Array)
        {
            var result = new List<ImportedTestStep>();
            var order = 1;
            foreach (var step in separated.EnumerateArray())
            {
                var action = TryGetString(step, "content");
                if (string.IsNullOrWhiteSpace(action))
                {
                    continue;
                }

                var expected = TryGetString(step, "expected");
                result.Add(new ImportedTestStep(order++, StripHtml(action)!, StripHtml(expected)));
            }

            if (result.Count > 0)
            {
                return result;
            }
        }

        var combined = TryGetString(caseElement, "custom_steps");
        if (string.IsNullOrWhiteSpace(combined))
        {
            return [];
        }

        var lines = combined
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => StripHtml(line.Trim()))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line!)
            .ToList();

        var parsed = new List<ImportedTestStep>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            parsed.Add(new ImportedTestStep(i + 1, lines[i], null));
        }

        return parsed;
    }

    /// <summary>
    /// TestRail returns rich-text fields (steps, preconditions, expected) as HTML. Convert to clean
    /// plain text: block-closing tags → newlines, strip remaining tags, decode entities, trim.
    /// </summary>
    private static string? StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html;
        }

        var text = html;
        text = System.Text.RegularExpressions.Regex.Replace(text, "<\\s*br\\s*/?>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, "</\\s*(p|div|li|tr|h[1-6])\\s*>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", string.Empty);
        text = System.Net.WebUtility.HtmlDecode(text);
        // Collapse 3+ newlines to a single blank line and trim trailing spaces per line.
        text = System.Text.RegularExpressions.Regex.Replace(text, "[ \\t]+\n", "\n");
        text = System.Text.RegularExpressions.Regex.Replace(text, "\n{3,}", "\n\n");
        return text.Trim();
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static int? TryGetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
