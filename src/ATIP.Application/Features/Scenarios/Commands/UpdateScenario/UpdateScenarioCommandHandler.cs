using System.Text.Json;
using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
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
        scenario.TagsJson = request.Tags.Count > 0
            ? JsonSerializer.Serialize(request.Tags)
            : null;

        // Full step replacement: remove old, add new with correct ordering.
        _db.ScenarioSteps.RemoveRange(scenario.Steps);
        scenario.Steps.Clear();

        var order = 1;
        foreach (var stepRequest in request.Steps)
        {
            if (string.IsNullOrWhiteSpace(stepRequest.Action))
            {
                continue; // Skip blank steps the user may have left empty.
            }

            _db.ScenarioSteps.Add(new ScenarioStep
            {
                TenantId = scenario.TenantId,
                ScenarioId = scenario.Id,
                Order = order++,
                Action = stepRequest.Action.Trim(),
                ExpectedResult = stepRequest.ExpectedResult?.Trim(),
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
}
