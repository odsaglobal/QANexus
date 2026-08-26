using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Scenarios.Common;

/// <summary>
/// Scenarios must hang off a Feature, but the product no longer exposes requirement analysis /
/// modules / features in the UI. This helper lazily provisions a single hidden "General" bucket
/// (Requirement → Module → Feature) per project so scenarios can be created without the user ever
/// picking a feature.
/// </summary>
public static class ScenarioBucket
{
    public const string BucketName = "General";

    public static async Task<Feature> EnsureAsync(
        IApplicationDbContext db,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct)
    {
        var feature = await db.Features
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Name == BucketName, ct);
        if (feature is not null)
        {
            return feature;
        }

        var requirement = await db.Requirements
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.ProjectId == projectId && r.Name == BucketName, ct);
        if (requirement is null)
        {
            requirement = new Requirement
            {
                TenantId = tenantId,
                ProjectId = projectId,
                Name = BucketName,
                SourceType = RequirementSourceType.PlainText,
                Status = RequirementStatus.Analyzed,
            };
            db.Requirements.Add(requirement);
            await db.SaveChangesAsync(ct);
        }

        var module = await db.RequirementModules
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.RequirementId == requirement.Id && m.Name == BucketName, ct);
        if (module is null)
        {
            module = new RequirementModule
            {
                TenantId = tenantId,
                ProjectId = projectId,
                RequirementId = requirement.Id,
                Name = BucketName,
                Description = "Auto-created bucket for scenarios/test cases.",
            };
            db.RequirementModules.Add(module);
            await db.SaveChangesAsync(ct);
        }

        feature = new Feature
        {
            TenantId = tenantId,
            ProjectId = projectId,
            ModuleId = module.Id,
            Name = BucketName,
            Description = "Auto-created bucket for scenarios/test cases.",
            Priority = Priority.Medium,
        };
        db.Features.Add(feature);
        await db.SaveChangesAsync(ct);
        return feature;
    }

    /// <summary>Concatenates the project's uploaded business-context document text (capped).</summary>
    public static async Task<string> LoadBusinessContextAsync(
        IApplicationDbContext db,
        Guid projectId,
        CancellationToken ct,
        int maxChars = 12_000)
    {
        var texts = await db.Requirements
            .IgnoreQueryFilters()
            .Where(r => r.ProjectId == projectId && r.Name != BucketName && r.ExtractedText != null && r.ExtractedText != "")
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new { r.Name, r.ExtractedText })
            .ToListAsync(ct);

        if (texts.Count == 0)
        {
            return string.Empty;
        }

        var combined = string.Join("\n\n", texts.Select(t => $"# {t.Name}\n{t.ExtractedText}"));
        return combined.Length > maxChars ? combined[..maxChars] + "\n... (truncated)" : combined;
    }
}
