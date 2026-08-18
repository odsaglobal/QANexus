using ATIP.Application.Features.Requirements.Dtos;
using MediatR;

namespace ATIP.Application.Features.Requirements.Queries.ListRequirements;

/// <summary>Lists requirement documents for a project (small set, unpaged).</summary>
public sealed record ListRequirementsQuery(Guid ProjectId) : IRequest<IReadOnlyList<RequirementDto>>;
