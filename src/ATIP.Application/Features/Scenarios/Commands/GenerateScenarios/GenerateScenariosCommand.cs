using ATIP.Application.Features.Scenarios.Dtos;
using MediatR;

namespace ATIP.Application.Features.Scenarios.Commands.GenerateScenarios;

/// <summary>
/// Generates AI test scenarios for a feature. Existing AI-generated scenarios for the feature
/// are replaced; manually authored scenarios are preserved.
/// </summary>
public sealed record GenerateScenariosCommand(Guid FeatureId) : IRequest<IReadOnlyList<ScenarioDto>>;
