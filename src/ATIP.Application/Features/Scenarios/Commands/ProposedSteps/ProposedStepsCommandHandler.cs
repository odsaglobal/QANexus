using System.Text.Json;
using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Scenarios.Dtos;
using ATIP.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Scenarios.Commands.ProposedSteps;

public sealed class ProposedStepsCommandHandler :
    IRequestHandler<ApplyProposedStepsCommand, ScenarioDto>,
    IRequestHandler<DiscardProposedStepsCommand, ScenarioDto>,
    IRequestHandler<RevertStepsCommand, ScenarioDto>
{
    private readonly IApplicationDbContext _db;

    public ProposedStepsCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ScenarioDto> Handle(ApplyProposedStepsCommand request, CancellationToken cancellationToken)
    {
        var scenario = await LoadAsync(request.ProjectId, request.Id, cancellationToken);

        var proposed = Deserialize(scenario.ProposedStepsJson);
        if (proposed.Count == 0)
        {
            throw new ValidationException(
            [
                new FluentValidation.Results.ValidationFailure("ProposedSteps", "There are no proposed steps to apply."),
            ]);
        }

        // Back up the current steps so the user can revert, then replace with the proposed ones.
        scenario.PreviousStepsJson = JsonSerializer.Serialize(await LoadCurrentStepsAsync(scenario.Id, cancellationToken));
        scenario.ProposedStepsJson = null;
        await ReplaceStepsAsync(scenario, proposed, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return await ReloadAsync(request.ProjectId, request.Id, cancellationToken);
    }

    public async Task<ScenarioDto> Handle(DiscardProposedStepsCommand request, CancellationToken cancellationToken)
    {
        var scenario = await LoadAsync(request.ProjectId, request.Id, cancellationToken);
        scenario.ProposedStepsJson = null;
        await _db.SaveChangesAsync(cancellationToken);
        return await ReloadAsync(request.ProjectId, request.Id, cancellationToken);
    }

    public async Task<ScenarioDto> Handle(RevertStepsCommand request, CancellationToken cancellationToken)
    {
        var scenario = await LoadAsync(request.ProjectId, request.Id, cancellationToken);

        var previous = Deserialize(scenario.PreviousStepsJson);
        if (previous.Count == 0)
        {
            throw new ValidationException(
            [
                new FluentValidation.Results.ValidationFailure("PreviousSteps", "There is no previous step set to revert to."),
            ]);
        }

        scenario.PreviousStepsJson = null;
        await ReplaceStepsAsync(scenario, previous, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return await ReloadAsync(request.ProjectId, request.Id, cancellationToken);
    }

    private async Task<Scenario> LoadAsync(Guid projectId, Guid id, CancellationToken ct) =>
        await _db.Scenarios
            .FirstOrDefaultAsync(s => s.Id == id && s.ProjectId == projectId, ct)
        ?? throw new NotFoundException(nameof(Scenario), id);

    private async Task<List<ProposedStep>> LoadCurrentStepsAsync(Guid scenarioId, CancellationToken ct) =>
        await _db.ScenarioSteps
            .AsNoTracking()
            .Where(s => s.ScenarioId == scenarioId)
            .OrderBy(s => s.Order)
            .Select(s => new ProposedStep { Order = s.Order, Action = s.Action, ExpectedResult = s.ExpectedResult })
            .ToListAsync(ct);

    private async Task<ScenarioDto> ReloadAsync(Guid projectId, Guid id, CancellationToken ct)
    {
        var reloaded = await _db.Scenarios
            .AsNoTracking()
            .Include(s => s.Steps)
            .FirstAsync(s => s.Id == id && s.ProjectId == projectId, ct);
        return ScenarioDto.FromEntity(reloaded);
    }

    private async Task ReplaceStepsAsync(Scenario scenario, List<ProposedStep> steps, CancellationToken ct)
    {
        // Bulk-delete existing steps directly so we avoid the tracked delete/insert
        // reconciliation (which trips EF's row-count concurrency check on the collection).
        await _db.ScenarioSteps.Where(s => s.ScenarioId == scenario.Id).ExecuteDeleteAsync(ct);

        var order = 1;
        foreach (var s in steps.OrderBy(x => x.Order))
        {
            _db.ScenarioSteps.Add(new ScenarioStep
            {
                TenantId = scenario.TenantId,
                ScenarioId = scenario.Id,
                Order = order++,
                Action = s.Action,
                ExpectedResult = s.ExpectedResult,
            });
        }
    }

    private static List<ProposedStep> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ProposedStep>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
