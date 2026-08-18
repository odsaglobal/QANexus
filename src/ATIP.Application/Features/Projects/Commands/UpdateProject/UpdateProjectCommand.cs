using ATIP.Application.Features.Projects.Dtos;
using MediatR;

namespace ATIP.Application.Features.Projects.Commands.UpdateProject;

/// <summary>Updates the mutable fields of an existing project.</summary>
public sealed record UpdateProjectCommand : IRequest<ProjectDto>
{
    public Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>One of the <c>ProjectStatus</c> names: Active, Archived, Suspended.</summary>
    public required string Status { get; init; }
}
