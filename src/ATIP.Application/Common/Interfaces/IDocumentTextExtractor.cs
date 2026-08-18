using ATIP.Domain.Enums;

namespace ATIP.Application.Common.Interfaces;

/// <summary>Extracts plain text from an uploaded document stream based on its source type.</summary>
public interface IDocumentTextExtractor
{
    /// <summary>Returns the extracted plain text, or an empty string if none could be recovered.</summary>
    Task<string> ExtractAsync(
        Stream content,
        RequirementSourceType sourceType,
        CancellationToken cancellationToken = default);
}
