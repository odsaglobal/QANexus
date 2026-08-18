using ATIP.Application.Features.Scenarios.Dtos;
using MediatR;

namespace ATIP.Application.Features.Scenarios.Commands.ImportScenariosFromFile;

public sealed record ImportScenariosFromFileCommand : IRequest<IReadOnlyList<ScenarioDto>>
{
    public Guid ProjectId { get; init; }
    public Guid FeatureId { get; init; }
    public required string FileName { get; init; }
    public required byte[] Content { get; init; }
}
