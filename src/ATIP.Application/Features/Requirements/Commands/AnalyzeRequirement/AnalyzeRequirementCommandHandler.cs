using System.Text.Json;
using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Utilities;
using ATIP.Application.Features.Requirements.Analysis;
using ATIP.Application.Features.Requirements.Dtos;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using ATIP.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ATIP.Application.Features.Requirements.Commands.AnalyzeRequirement;

public sealed class AnalyzeRequirementCommandHandler
    : IRequestHandler<AnalyzeRequirementCommand, RequirementDetailDto>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IApplicationDbContext _db;
    private readonly ILlmClient _llm;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<AnalyzeRequirementCommandHandler> _logger;

    public AnalyzeRequirementCommandHandler(
        IApplicationDbContext db,
        ILlmClient llm,
        IDateTimeProvider clock,
        ILogger<AnalyzeRequirementCommandHandler> logger)
    {
        _db = db;
        _llm = llm;
        _clock = clock;
        _logger = logger;
    }

    public async Task<RequirementDetailDto> Handle(
        AnalyzeRequirementCommand request,
        CancellationToken cancellationToken)
    {
        var requirement = await _db.Requirements
            .Include(r => r.Modules)
            .ThenInclude(m => m.Features)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Requirement), request.Id);

        if (string.IsNullOrWhiteSpace(requirement.ExtractedText))
        {
            throw new ValidationException(
            [
                new FluentValidation.Results.ValidationFailure(
                    nameof(request.Id),
                    "This requirement has no extracted text to analyze."),
            ]);
        }

        // Atomically claim the requirement for analysis. If another request is already analyzing it,
        // the update affects 0 rows and we reject the duplicate instead of running two analyses in
        // parallel (which would race on the child module rows and throw a DbUpdateConcurrencyException).
        var claimed = await _db.Requirements
            .Where(r => r.Id == requirement.Id && r.Status != RequirementStatus.Analyzing)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.Status, RequirementStatus.Analyzing),
                cancellationToken);

        if (claimed == 0)
        {
            throw new DomainRuleException("Analysis is already in progress for this requirement.");
        }

        requirement.Status = RequirementStatus.Analyzing;

        try
        {
            var raw = await _llm.CompleteAsync(
                RequirementAnalysisPrompts.System,
                RequirementAnalysisPrompts.BuildUserPrompt(requirement.Name, requirement.ExtractedText),
                jsonMode: true,
                cancellationToken);

            var result = Deserialize(raw);

            ReplaceExtractedStructure(requirement, result);

            requirement.Status = RequirementStatus.Analyzed;
            requirement.AnalyzedAtUtc = _clock.UtcNow;
            requirement.ErrorMessage = null;
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not NotFoundException and not ValidationException)
        {
            _logger.LogError(ex, "Requirement analysis failed for {RequirementId}", requirement.Id);
            requirement.Status = RequirementStatus.Failed;
            requirement.ErrorMessage = $"AI analysis failed: {ex.Message}";
            await _db.SaveChangesAsync(cancellationToken);
            throw;
        }

        var reloaded = await _db.Requirements
            .AsNoTracking()
            .Include(r => r.Modules).ThenInclude(m => m.Features).ThenInclude(f => f.UserStories)
            .Include(r => r.Modules).ThenInclude(m => m.Features).ThenInclude(f => f.Scenarios)
            .FirstAsync(r => r.Id == requirement.Id, cancellationToken);

        return RequirementDetailDto.FromEntity(reloaded);
    }

    private void ReplaceExtractedStructure(Requirement requirement, RequirementAnalysisResult result)
    {
        // Clear any prior analysis so re-running is idempotent.
        _db.RequirementModules.RemoveRange(requirement.Modules);
        requirement.Modules.Clear();

        foreach (var module in result.Modules)
        {
            var moduleEntity = new RequirementModule
            {
                TenantId = requirement.TenantId,
                ProjectId = requirement.ProjectId,
                RequirementId = requirement.Id,
                Name = Truncate(module.Name, 200),
                Description = module.Description,
            };

            foreach (var feature in module.Features)
            {
                var featureEntity = new Feature
                {
                    TenantId = requirement.TenantId,
                    ProjectId = requirement.ProjectId,
                    ModuleId = moduleEntity.Id,
                    Name = Truncate(feature.Name, 200),
                    Description = feature.Description,
                    Priority = ParsePriority(feature.Priority),
                    BusinessRulesJson = feature.BusinessRules.Count > 0
                        ? JsonSerializer.Serialize(feature.BusinessRules)
                        : null,
                };

                foreach (var story in feature.UserStories)
                {
                    featureEntity.UserStories.Add(new UserStory
                    {
                        TenantId = requirement.TenantId,
                        ProjectId = requirement.ProjectId,
                        FeatureId = featureEntity.Id,
                        AsA = Truncate(string.IsNullOrWhiteSpace(story.AsA) ? "user" : story.AsA, 200),
                        IWant = Truncate(string.IsNullOrWhiteSpace(story.IWant) ? feature.Name : story.IWant, 500),
                        SoThat = story.SoThat,
                        AcceptanceCriteriaJson = story.AcceptanceCriteria.Count > 0
                            ? JsonSerializer.Serialize(story.AcceptanceCriteria)
                            : null,
                    });
                }

                moduleEntity.Features.Add(featureEntity);
            }

            // Explicitly Add the new subgraph. Adding to the tracked requirement's navigation
            // instead would make EF's "is-key-set" heuristic treat these client-keyed entities as
            // existing rows (UPDATE), which then fail with a 0-rows concurrency error.
            _db.RequirementModules.Add(moduleEntity);
            requirement.Modules.Add(moduleEntity);
        }
    }

    private static RequirementAnalysisResult Deserialize(string raw)
    {
        var json = JsonExtraction.ExtractJsonObject(raw);
        var result = JsonSerializer.Deserialize<RequirementAnalysisResult>(json, JsonOptions);
        if (result is null || result.Modules.Count == 0)
        {
            throw new InvalidOperationException("The AI response did not contain any modules.");
        }

        return result;
    }

    private static Priority ParsePriority(string? value) =>
        Enum.TryParse<Priority>(value, ignoreCase: true, out var p) ? p : Priority.Medium;

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
