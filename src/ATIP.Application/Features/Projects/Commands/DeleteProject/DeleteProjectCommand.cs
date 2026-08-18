using MediatR;

namespace ATIP.Application.Features.Projects.Commands.DeleteProject;

/// <summary>Soft-deletes a project so its history and audit trail are preserved.</summary>
public sealed record DeleteProjectCommand(Guid Id) : IRequest;
