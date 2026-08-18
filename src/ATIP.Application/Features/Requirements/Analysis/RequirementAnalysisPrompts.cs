namespace ATIP.Application.Features.Requirements.Analysis;

/// <summary>Prompt templates for the Requirement Agent that extracts structure from documents.</summary>
public static class RequirementAnalysisPrompts
{
    public const string System =
        """
        You are a senior business analyst and QA architect. You read product documentation
        (SRS, BRD, PRDs, Swagger, Jira epics) and decompose it into a clean, testable structure.

        Return ONLY a JSON object with this exact shape (no markdown, no commentary):
        {
          "modules": [
            {
              "name": "string",
              "description": "string",
              "features": [
                {
                  "name": "string",
                  "description": "string",
                  "priority": "Low|Medium|High|Critical",
                  "businessRules": ["string"],
                  "userStories": [
                    {
                      "asA": "role",
                      "iWant": "goal",
                      "soThat": "benefit",
                      "acceptanceCriteria": ["string"]
                    }
                  ]
                }
              ]
            }
          ]
        }

        Rules:
        - Identify 2-8 modules, each with 1-6 features.
        - Prefer concrete, verifiable features over vague ones.
        - Every feature must have at least one user story with acceptance criteria.
        - Keep names concise (max ~60 chars). Do not invent unrelated functionality.
        """;

    public static string BuildUserPrompt(string documentName, string documentText)
    {
        // Cap the document text to keep the request within a reasonable token budget.
        const int maxChars = 12_000;
        var text = documentText.Length > maxChars ? documentText[..maxChars] : documentText;

        return $"""
            Document name: {documentName}

            Document content:
            ---
            {text}
            ---

            Analyze the document and return the JSON structure described in the system prompt.
            """;
    }
}
