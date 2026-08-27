using ATIP.Application.Common.Security;
using ATIP.Application.Features.Requirements.Dtos;
using ATIP.Domain.Enums;
using MediatR;

namespace ATIP.Application.Features.Requirements.Commands.UploadRequirement;

/// <summary>
/// Uploads a requirement document to a project: persists the original file, extracts its text,
/// and stores it ready for AI analysis. Analysis is triggered separately.
/// </summary>
public sealed record UploadRequirementCommand : IRequest<RequirementDto>, IProjectScopedRequest
{
    public Guid ProjectId { get; init; }

    public required string Name { get; init; }

    public required string FileName { get; init; }

    /// <summary>Raw bytes of the uploaded document.</summary>
    public required byte[] Content { get; init; }

    /// <summary>MIME content type reported by the client, used to infer the source type.</summary>
    public string? ContentType { get; init; }

    public ProjectRole RequiredRole => ProjectRole.Editor;
}
