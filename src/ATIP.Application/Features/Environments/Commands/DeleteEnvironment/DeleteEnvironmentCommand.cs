using MediatR;

namespace ATIP.Application.Features.Environments.Commands.DeleteEnvironment;

public sealed record DeleteEnvironmentCommand(Guid Id) : IRequest;
