using MediatR;

namespace ATIP.Application.Features.Requirements.Commands.CreateManualFeature;

/// <summary>
/// Creates a lightweight manual requirement → module → feature container without uploading a
/// document, so users can author scenarios (and drive scenario-based exploration) by hand.
/// </summary>
public sealed record CreateManualFeatureCommand : IRequest<ManualFeatureDto>
{
    public Guid ProjectId { get; init; }
    public required string FeatureName { get; init; }
    public string? ModuleName { get; init; }
    public string? Description { get; init; }
}

public sealed record ManualFeatureDto
{
    public required Guid RequirementId { get; init; }
    public required Guid ModuleId { get; init; }
    public required Guid FeatureId { get; init; }
    public required string FeatureName { get; init; }
    public required string ModuleName { get; init; }
}
