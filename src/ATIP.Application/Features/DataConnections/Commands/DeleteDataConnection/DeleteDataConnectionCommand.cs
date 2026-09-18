using ATIP.Application.Common.Security;
using ATIP.Domain.Enums;
using MediatR;

namespace ATIP.Application.Features.DataConnections.Commands.DeleteDataConnection;

/// <summary>Soft-deletes a data connection.</summary>
public sealed record DeleteDataConnectionCommand(Guid ProjectId, Guid EnvironmentId, Guid Id)
    : IRequest, IProjectScopedRequest
{
    public ProjectRole RequiredRole => ProjectRole.Owner;
}
