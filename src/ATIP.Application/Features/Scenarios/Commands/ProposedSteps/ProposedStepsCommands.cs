using ATIP.Application.Features.Scenarios.Dtos;
using MediatR;

namespace ATIP.Application.Features.Scenarios.Commands.ProposedSteps;

/// <summary>Applies the AI-proposed steps to the scenario, backing up the current steps for revert.</summary>
public sealed record ApplyProposedStepsCommand(Guid ProjectId, Guid Id) : IRequest<ScenarioDto>;

/// <summary>Discards the AI-proposed steps without changing the scenario's steps.</summary>
public sealed record DiscardProposedStepsCommand(Guid ProjectId, Guid Id) : IRequest<ScenarioDto>;

/// <summary>Reverts the scenario's steps to the backup taken when proposed steps were last applied.</summary>
public sealed record RevertStepsCommand(Guid ProjectId, Guid Id) : IRequest<ScenarioDto>;
