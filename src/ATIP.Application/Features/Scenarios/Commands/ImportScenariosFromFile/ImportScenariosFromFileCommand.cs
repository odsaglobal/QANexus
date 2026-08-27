using ATIP.Application.Common.Security;
using ATIP.Application.Features.Scenarios.Dtos;
using ATIP.Domain.Enums;
using MediatR;

namespace ATIP.Application.Features.Scenarios.Commands.ImportScenariosFromFile;

public sealed record ImportScenariosFromFileCommand : IRequest<IReadOnlyList<ScenarioDto>>, IProjectScopedRequest
{
    public Guid ProjectId { get; init; }
    public Guid FeatureId { get; init; }
    public required string FileName { get; init; }
    public required byte[] Content { get; init; }

    public ProjectRole RequiredRole => ProjectRole.Editor;
}
