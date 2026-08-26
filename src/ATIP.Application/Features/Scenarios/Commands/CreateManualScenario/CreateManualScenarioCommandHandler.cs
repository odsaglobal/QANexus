using System.Text.Json;
using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Scenarios.Common;
using ATIP.Application.Features.Scenarios.Dtos;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Scenarios.Commands.CreateManualScenario;

public sealed class CreateManualScenarioCommandHandler
    : IRequestHandler<CreateManualScenarioCommand, ScenarioDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateManualScenarioCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ScenarioDto> Handle(
        CreateManualScenarioCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new ForbiddenAccessException("No tenant context.");

        // Feature is optional: fall back to the hidden per-project "General" bucket.
        var feature = request.FeatureId == Guid.Empty
            ? await ScenarioBucket.EnsureAsync(_db, tenantId, request.ProjectId, cancellationToken)
            : await _db.Features
                .FirstOrDefaultAsync(f => f.Id == request.FeatureId && f.ProjectId == request.ProjectId, cancellationToken)
              ?? throw new NotFoundException(nameof(Feature), request.FeatureId);

        var tags = request.Tags?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? [];
        if (!tags.Contains("import:manual"))
        {
            tags.Insert(0, "import:manual");
        }

        var scenario = new Scenario
        {
            TenantId = tenantId,
            ProjectId = request.ProjectId,
            FeatureId = feature.Id,
            Title = Truncate(request.Title, 300),
            Type = ParseEnum(request.Type, ScenarioType.Positive),
            Priority = ParseEnum(request.Priority, Priority.Medium),
            Risk = ParseEnum(request.Risk, RiskLevel.Medium),
            Source = ScenarioSource.Manual,
            Preconditions = request.Preconditions?.Trim(),
            ExpectedResult = request.ExpectedResult?.Trim(),
            JiraKey = string.IsNullOrWhiteSpace(request.JiraKey) ? null : request.JiraKey.Trim(),
            TagsJson = JsonSerializer.Serialize(tags),
        };

        var order = 1;
        foreach (var step in request.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Action))
            {
                continue;
            }

            scenario.Steps.Add(new ScenarioStep
            {
                TenantId = tenantId,
                ScenarioId = scenario.Id,
                Order = order++,
                Action = Truncate(step.Action.Trim(), 1000),
                ExpectedResult = step.ExpectedResult?.Trim(),
            });
        }

        if (scenario.Steps.Count == 0)
        {
            throw new ValidationException([
                new FluentValidation.Results.ValidationFailure(nameof(request.Steps), "At least one step with an action is required."),
            ]);
        }

        _db.Scenarios.Add(scenario);
        await _db.SaveChangesAsync(cancellationToken);

        return ScenarioDto.FromEntity(scenario);
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
