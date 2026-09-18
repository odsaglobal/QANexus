using System.Text.Json;
using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Explorer.Agent;
using ATIP.Application.Features.Scenarios.Dtos;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Scenarios.Commands.UpdateScenario;

public sealed class UpdateScenarioCommandHandler : IRequestHandler<UpdateScenarioCommand, ScenarioDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateScenarioCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ScenarioDto> Handle(UpdateScenarioCommand request, CancellationToken cancellationToken)
    {
        var scenario = await _db.Scenarios
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Scenario), request.Id);

        scenario.Title = request.Title.Trim();
        scenario.Type = Enum.Parse<ScenarioType>(request.Type, ignoreCase: true);
        scenario.Priority = Enum.Parse<Priority>(request.Priority, ignoreCase: true);
        scenario.Risk = Enum.Parse<RiskLevel>(request.Risk, ignoreCase: true);
        scenario.Preconditions = request.Preconditions?.Trim();
        scenario.ExpectedResult = request.ExpectedResult?.Trim();
        scenario.JiraKey = string.IsNullOrWhiteSpace(request.JiraKey) ? null : request.JiraKey.Trim();
        scenario.AutoHealEnabled = request.AutoHealEnabled;
        scenario.TagsJson = request.Tags.Count > 0
            ? JsonSerializer.Serialize(request.Tags)
            : null;

        // Full step replacement: remove old, add new with correct ordering.
        // Recordings are keyed by step text so that renaming a step does not silently keep a
        // recording that no longer matches it, while merely reordering or editing a neighbour
        // does not throw away everything the engine has learned about this scenario.
        var existingRecordings = scenario.Steps
            .Where(s => !string.IsNullOrWhiteSpace(s.RecordedActionsJson))
            .GroupBy(s => s.Action.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().RecordedActionsJson, StringComparer.OrdinalIgnoreCase);

        _db.ScenarioSteps.RemoveRange(scenario.Steps);
        scenario.Steps.Clear();

        var order = 1;
        foreach (var stepRequest in request.Steps)
        {
            if (string.IsNullOrWhiteSpace(stepRequest.Action))
            {
                continue; // Skip blank steps the user may have left empty.
            }

            var action = stepRequest.Action.Trim();
            var platform = Enum.TryParse<TestPlatform>(stepRequest.Platform, ignoreCase: true, out var parsed)
                ? parsed
                : TestPlatform.Web;

            var authored = BuildAuthoredRecording(stepRequest, platform);

            _db.ScenarioSteps.Add(new ScenarioStep
            {
                TenantId = scenario.TenantId,
                ScenarioId = scenario.Id,
                Order = order++,
                Action = action,
                ExpectedResult = stepRequest.ExpectedResult?.Trim(),
                Platform = platform,
                RecordedActionsJson = authored
                    ?? (existingRecordings.TryGetValue(action, out var kept) ? kept : null),
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Reload with fresh steps for the response.
        var reloaded = await _db.Scenarios
            .AsNoTracking()
            .Include(s => s.Steps)
            .FirstAsync(s => s.Id == request.Id, cancellationToken);

        return ScenarioDto.FromEntity(reloaded);
    }

    /// <summary>
    /// Turns an explicitly authored step into a recording the engine can replay, or null when the
    /// step is an ordinary browser step to be discovered by the agent.
    /// </summary>
    /// <remarks>
    /// Authoring writes the same <c>RecordedActionsJson</c> that a successful exploration writes,
    /// so an API or database step needs no separate execution route: it is simply a recording that
    /// happened to be typed rather than observed, and replay treats it identically.
    /// </remarks>
    private static string? BuildAuthoredRecording(UpdateScenarioStepRequest step, TestPlatform platform)
    {
        var kind = step.Kind?.Trim();

        if (string.IsNullOrWhiteSpace(kind))
        {
            // A non-web step with no verb cannot be discovered — there is no page to explore — so
            // it is left unrecorded and reported as needing review at run time rather than being
            // guessed at here.
            return null;
        }

        var recorded = new RecordedStepAction
        {
            Kind = kind,
            Platform = platform,
            Target = string.IsNullOrWhiteSpace(step.Target) ? null : step.Target.Trim(),
            Value = step.Value,
            Options = step.Options is { Count: > 0 }
                ? step.Options.ToDictionary(kv => kv.Key, kv => kv.Value)
                : null
        };

        return JsonSerializer.Serialize(new[] { recorded }, RecordingJsonOptions);
    }

    private static readonly JsonSerializerOptions RecordingJsonOptions = new(JsonSerializerDefaults.Web);
}
