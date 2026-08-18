using System.Text.Json;
using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Scenarios.Dtos;
using ATIP.Application.Features.Scenarios.Import;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Scenarios.Commands.ImportScenariosFromFile;

public sealed class ImportScenariosFromFileCommandHandler
    : IRequestHandler<ImportScenariosFromFileCommand, IReadOnlyList<ScenarioDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ImportScenariosFromFileCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ScenarioDto>> Handle(
        ImportScenariosFromFileCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new ForbiddenAccessException("No tenant context.");

        var feature = await _db.Features
            .Include(f => f.Scenarios)
            .ThenInclude(s => s.Steps)
            .FirstOrDefaultAsync(f => f.Id == request.FeatureId && f.ProjectId == request.ProjectId, cancellationToken)
            ?? throw new NotFoundException(nameof(Feature), request.FeatureId);

        var parsed = ScenarioImportParser.Parse(request.FileName, request.Content);
        if (parsed.Count == 0)
        {
            throw new ValidationException([
                new FluentValidation.Results.ValidationFailure(nameof(request.Content), "No valid scenarios with steps were found in the uploaded file."),
            ]);
        }

        var existingManual = feature.Scenarios.Where(s => s.Source == ScenarioSource.Manual).ToList();
        _db.Scenarios.RemoveRange(existingManual);

        var created = new List<Scenario>(parsed.Count);
        foreach (var imported in parsed)
        {
            var tags = new List<string> { "import:manual" };
            if (!string.IsNullOrWhiteSpace(imported.ExternalReference))
            {
                tags.Add($"external:{imported.ExternalReference}");
            }

            var scenario = new Scenario
            {
                TenantId = tenantId,
                ProjectId = request.ProjectId,
                FeatureId = request.FeatureId,
                Title = Truncate(imported.Title, 300),
                Type = ScenarioType.Positive,
                Priority = Priority.Medium,
                Risk = RiskLevel.Medium,
                Source = ScenarioSource.Manual,
                Preconditions = imported.Preconditions,
                ExpectedResult = imported.ExpectedResult,
                TagsJson = JsonSerializer.Serialize(tags),
            };

            foreach (var step in imported.Steps.OrderBy(s => s.Order))
            {
                scenario.Steps.Add(new ScenarioStep
                {
                    TenantId = tenantId,
                    ScenarioId = scenario.Id,
                    Order = step.Order,
                    Action = Truncate(step.Action, 1000),
                    ExpectedResult = step.ExpectedResult,
                });
            }

            _db.Scenarios.Add(scenario);
            created.Add(scenario);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return created
            .OrderBy(s => s.Title)
            .Select(ScenarioDto.FromEntity)
            .ToList();
    }

    private static string Truncate(string value, int max)
    {
        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }
}
