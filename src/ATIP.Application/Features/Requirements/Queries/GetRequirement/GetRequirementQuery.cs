using ATIP.Application.Features.Requirements.Dtos;
using MediatR;

namespace ATIP.Application.Features.Requirements.Queries.GetRequirement;

/// <summary>Returns a requirement with its full extracted module → feature → story structure.</summary>
public sealed record GetRequirementQuery(Guid Id) : IRequest<RequirementDetailDto>;
