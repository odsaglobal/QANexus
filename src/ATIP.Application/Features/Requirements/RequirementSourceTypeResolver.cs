using ATIP.Domain.Enums;

namespace ATIP.Application.Features.Requirements;

/// <summary>Maps an uploaded file's name/content-type to a <see cref="RequirementSourceType"/>.</summary>
public static class RequirementSourceTypeResolver
{
    public static RequirementSourceType Resolve(string fileName, string? contentType)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".pdf" => RequirementSourceType.Pdf,
            ".docx" => RequirementSourceType.Docx,
            ".md" or ".markdown" => RequirementSourceType.Markdown,
            ".json" or ".yaml" or ".yml" => RequirementSourceType.Swagger,
            ".txt" or ".text" => RequirementSourceType.PlainText,
            _ => contentType switch
            {
                "application/pdf" => RequirementSourceType.Pdf,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                    => RequirementSourceType.Docx,
                "text/markdown" => RequirementSourceType.Markdown,
                _ => RequirementSourceType.PlainText,
            },
        };
    }
}
