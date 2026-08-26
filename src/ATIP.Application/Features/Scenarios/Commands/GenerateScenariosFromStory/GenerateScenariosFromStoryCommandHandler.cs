using System.Text.Json;
using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Utilities;
using ATIP.Application.Features.Scenarios.Common;
using ATIP.Application.Features.Scenarios.Dtos;
using ATIP.Application.Features.Scenarios.Generation;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using MediatR;

namespace ATIP.Application.Features.Scenarios.Commands.GenerateScenariosFromStory;

public sealed class GenerateScenariosFromStoryCommandHandler
    : IRequestHandler<GenerateScenariosFromStoryCommand, IReadOnlyList<ScenarioDto>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IApplicationDbContext _db;
    private readonly ILlmClient _llm;
    private readonly ICurrentUser _currentUser;

    public GenerateScenariosFromStoryCommandHandler(IApplicationDbContext db, ILlmClient llm, ICurrentUser currentUser)
    {
        _db = db;
        _llm = llm;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ScenarioDto>> Handle(
        GenerateScenariosFromStoryCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new ForbiddenAccessException("No tenant context is available for the current user.");

        var feature = await ScenarioBucket.EnsureAsync(_db, tenantId, request.ProjectId, cancellationToken);
        var businessContext = await ScenarioBucket.LoadBusinessContextAsync(_db, request.ProjectId, cancellationToken);

        var raw = await _llm.CompleteAsync(
            ScenarioGenerationPrompts.System,
            ScenarioGenerationPrompts.BuildStoryUserPrompt(request.Story, businessContext),
            jsonMode: true,
            cancellationToken);

        var result = Deserialize(raw);

        var created = new List<Scenario>();
        foreach (var generated in result.Scenarios)
        {
            var scenario = new Scenario
            {
                TenantId = tenantId,
                ProjectId = request.ProjectId,
                FeatureId = feature.Id,
                Title = Truncate(generated.Title, 300),
                Type = ParseEnum(generated.Type, ScenarioType.Positive),
                Priority = ParseEnum(generated.Priority, Priority.Medium),
                Risk = ParseEnum(generated.Risk, RiskLevel.Low),
                Source = ScenarioSource.AiGenerated,
                Preconditions = generated.Preconditions,
                ExpectedResult = generated.ExpectedResult,
                TagsJson = generated.Tags.Count > 0 ? JsonSerializer.Serialize(generated.Tags) : null,
            };

            var order = 1;
            foreach (var step in generated.Steps)
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
                    Action = Truncate(step.Action, 1000),
                    ExpectedResult = step.ExpectedResult,
                });
            }

            _db.Scenarios.Add(scenario);
            created.Add(scenario);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return created
            .OrderBy(s => s.Type)
            .ThenBy(s => s.Title)
            .Select(ScenarioDto.FromEntity)
            .ToList();
    }

    private static ScenarioGenerationResult Deserialize(string raw)
    {
        var json = JsonExtraction.ExtractJsonObject(raw);
        var result = JsonSerializer.Deserialize<ScenarioGenerationResult>(json, JsonOptions);
        if (result is null || result.Scenarios.Count == 0)
        {
            throw new InvalidOperationException("The AI response did not contain any scenarios.");
        }

        return result;
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
