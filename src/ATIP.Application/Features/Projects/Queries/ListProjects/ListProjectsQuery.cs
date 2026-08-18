using ATIP.Application.Common.Models;
using ATIP.Application.Features.Projects.Dtos;
using MediatR;

namespace ATIP.Application.Features.Projects.Queries.ListProjects;

/// <summary>Returns a paged, optionally searched and sorted list of projects in the caller's tenant.</summary>
public sealed record ListProjectsQuery : PaginationQuery, IRequest<PagedResult<ProjectDto>>
{
    /// <summary>Optional status filter (Active, Archived, Suspended).</summary>
    public string? Status { get; init; }
}
