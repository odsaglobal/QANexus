using ATIP.Application.Features.Requirements.Dtos;
using MediatR;

namespace ATIP.Application.Features.Requirements.Commands.AnalyzeRequirement;

/// <summary>
/// Runs AI analysis on a previously uploaded requirement, replacing any existing extracted
/// modules/features/stories with a freshly derived structure.
/// </summary>
public sealed record AnalyzeRequirementCommand(Guid Id) : IRequest<RequirementDetailDto>;
