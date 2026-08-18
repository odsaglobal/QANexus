using System.Text.Json;
using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Utilities;
using ATIP.Application.Features.Scenarios.Dtos;
using ATIP.Application.Features.Scenarios.Generation;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Scenarios.Commands.GenerateScenarios;

public sealed class GenerateScenariosCommandHandler
    : IRequestHandler<GenerateScenariosCommand, IReadOnlyList<ScenarioDto>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IApplicationDbContext _db;
    private readonly ILlmClient _llm;

    public GenerateScenariosCommandHandler(IApplicationDbContext db, ILlmClient llm)
    {
        _db = db;
        _llm = llm;
    }

    public async Task<IReadOnlyList<ScenarioDto>> Handle(
        GenerateScenariosCommand request,
        CancellationToken cancellationToken)
    {
        var feature = await _db.Features
            .Include(f => f.UserStories)
            .Include(f => f.Scenarios)
            .Include(f => f.Module)
            .ThenInclude(m => m.Requirement)
            .FirstOrDefaultAsync(f => f.Id == request.FeatureId, cancellationToken)
            ?? throw new NotFoundException(nameof(Feature), request.FeatureId);

        // Ground generation in the actual SRS / requirement source text and extracted business rules.
        var srsContext = feature.Module?.Requirement?.ExtractedText;
        var businessRules = DeserializeStringList(feature.BusinessRulesJson);

        var raw = await _llm.CompleteAsync(
            ScenarioGenerationPrompts.System,
            ScenarioGenerationPrompts.BuildUserPrompt(feature, feature.UserStories, srsContext, businessRules),
            jsonMode: true,
            cancellationToken);

        var result = Deserialize(raw);

        // Replace prior AI-generated scenarios; keep manual ones.
        var aiScenarios = feature.Scenarios.Where(s => s.Source == ScenarioSource.AiGenerated).ToList();
        _db.Scenarios.RemoveRange(aiScenarios);

        var created = new List<Scenario>();
        foreach (var generated in result.Scenarios)
        {
            var scenario = new Scenario
            {
                TenantId = feature.TenantId,
                ProjectId = feature.ProjectId,
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
                    TenantId = feature.TenantId,
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

    private static IReadOnlyList<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
