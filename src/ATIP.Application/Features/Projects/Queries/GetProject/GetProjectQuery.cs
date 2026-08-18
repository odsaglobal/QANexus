using ATIP.Application.Features.Projects.Dtos;
using MediatR;

namespace ATIP.Application.Features.Projects.Queries.GetProject;

public sealed record GetProjectQuery(Guid Id) : IRequest<ProjectDto>;
