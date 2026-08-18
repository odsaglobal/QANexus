namespace ATIP.Domain.Enums;

/// <summary>Origin format of an uploaded requirement document.</summary>
public enum RequirementSourceType
{
    PlainText = 0,
    Markdown = 1,
    Pdf = 2,
    Docx = 3,
    Swagger = 4,
    Confluence = 5,
    Jira = 6
}
