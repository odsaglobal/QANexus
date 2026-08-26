using System.Text;
using ATIP.Domain.Entities;

namespace ATIP.Application.Features.Scenarios.Generation;

/// <summary>Prompt templates for the Scenario Agent that generates tests for a feature.</summary>
public static class ScenarioGenerationPrompts
{
    public const string System =
        """
        You are a principal QA engineer generating comprehensive, executable test scenarios
        for a single feature. Cover a balanced spread of scenario types where relevant:
        Positive, Negative, Boundary, Regression, Smoke, Sanity, Accessibility, Security, Api.

        Return ONLY a JSON object (no markdown, no commentary):
        {
          "scenarios": [
            {
              "title": "string",
              "type": "Positive|Negative|Boundary|Regression|Smoke|Sanity|Accessibility|Security|Api|CrossBrowser|Performance",
              "priority": "Low|Medium|High|Critical",
              "risk": "Low|Medium|High",
              "preconditions": "string",
              "expectedResult": "string",
              "tags": ["string"],
              "steps": [ { "action": "string", "expectedResult": "string" } ]
            }
          ]
        }

        Rules:
        - Generate 4-8 scenarios covering at least 3 different types.
        - Each scenario must have 2-6 concrete, ordered steps.
        - Titles must be specific and unique. Tie steps to the acceptance criteria.
        - Do not include locators or code; describe actions in business terms.
        """;

    public static string BuildUserPrompt(Feature feature, IEnumerable<UserStory> stories)
        => BuildUserPrompt(feature, stories, null, null);

    /// <summary>
    /// Builds the scenario-generation prompt. When <paramref name="srsContext"/> is supplied
    /// (the extracted SRS / requirement source text) it is included as grounding so the AI ties
    /// scenarios to the actual specification rather than inventing behavior.
    /// </summary>
    public static string BuildUserPrompt(
        Feature feature,
        IEnumerable<UserStory> stories,
        string? srsContext,
        IEnumerable<string>? businessRules)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Feature: {feature.Name}");
        if (!string.IsNullOrWhiteSpace(feature.Description))
        {
            sb.AppendLine($"Description: {feature.Description}");
        }

        sb.AppendLine($"Priority: {feature.Priority}");

        var ruleList = businessRules?.Where(r => !string.IsNullOrWhiteSpace(r)).ToList() ?? [];
        if (ruleList.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Business rules:");
            foreach (var rule in ruleList)
            {
                sb.AppendLine($"- {rule}");
            }
        }

        var storyList = stories.ToList();
        if (storyList.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("User stories & acceptance criteria:");
            foreach (var story in storyList)
            {
                sb.AppendLine($"- As a {story.AsA}, I want {story.IWant}" +
                              (string.IsNullOrWhiteSpace(story.SoThat) ? "" : $", so that {story.SoThat}"));
            }
        }

        if (!string.IsNullOrWhiteSpace(srsContext))
        {
            const int maxContextChars = 6_000;
            var trimmed = srsContext.Trim();
            if (trimmed.Length > maxContextChars)
            {
                trimmed = trimmed[..maxContextChars] + "\n... (truncated)";
            }

            sb.AppendLine();
            sb.AppendLine("Reference specification (SRS / context) — ground the scenarios strictly in this source:");
            sb.AppendLine("\"\"\"");
            sb.AppendLine(trimmed);
            sb.AppendLine("\"\"\"");
        }

        sb.AppendLine();
        sb.AppendLine("Generate the scenarios as JSON per the system prompt.");
        return sb.ToString();
    }

    /// <summary>
    /// Builds a scenario-generation prompt from a free-text story/description plus the project's
    /// business-context documents, for authoring manual test cases without a formal feature model.
    /// </summary>
    public static string BuildStoryUserPrompt(string story, string? businessContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Author test cases for the following story / description:");
        sb.AppendLine("\"\"\"");
        sb.AppendLine(story.Trim());
        sb.AppendLine("\"\"\"");

        if (!string.IsNullOrWhiteSpace(businessContext))
        {
            const int maxContextChars = 8_000;
            var trimmed = businessContext.Trim();
            if (trimmed.Length > maxContextChars)
            {
                trimmed = trimmed[..maxContextChars] + "\n... (truncated)";
            }

            sb.AppendLine();
            sb.AppendLine("Business context (domain knowledge to ground the test cases) — use it, don't invent behavior:");
            sb.AppendLine("\"\"\"");
            sb.AppendLine(trimmed);
            sb.AppendLine("\"\"\"");
        }

        sb.AppendLine();
        sb.AppendLine("Generate the scenarios as JSON per the system prompt.");
        return sb.ToString();
    }
}
