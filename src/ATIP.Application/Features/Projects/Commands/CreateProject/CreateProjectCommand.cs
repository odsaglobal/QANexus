using ATIP.Application.Features.Projects.Dtos;
using MediatR;

namespace ATIP.Application.Features.Projects.Commands.CreateProject;

/// <summary>Creates a new project within the caller's tenant and enrolls the caller as its Owner.</summary>
public sealed record CreateProjectCommand : IRequest<ProjectDto>
{
    public required string Name { get; init; }

    /// <summary>Optional explicit key; auto-derived from the name when omitted.</summary>
    public string? Key { get; init; }

    public string? Description { get; init; }
}
