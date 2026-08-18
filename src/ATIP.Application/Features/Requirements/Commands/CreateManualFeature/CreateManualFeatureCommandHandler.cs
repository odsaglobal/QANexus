using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Requirements.Commands.CreateManualFeature;

public sealed class CreateManualFeatureCommandHandler
    : IRequestHandler<CreateManualFeatureCommand, ManualFeatureDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public CreateManualFeatureCommandHandler(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        IDateTimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ManualFeatureDto> Handle(
        CreateManualFeatureCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new ForbiddenAccessException("No tenant context.");

        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken)
            ?? throw new NotFoundException(nameof(Project), request.ProjectId);

        var moduleName = string.IsNullOrWhiteSpace(request.ModuleName) ? "Manual" : request.ModuleName.Trim();

        var feature = new Feature
        {
            TenantId = tenantId,
            ProjectId = project.Id,
            Name = request.FeatureName.Trim(),
            Description = request.Description?.Trim(),
            Priority = Priority.Medium,
        };

        var module = new RequirementModule
        {
            TenantId = tenantId,
            ProjectId = project.Id,
            Name = moduleName,
            Features = { feature },
        };

        var requirement = new Requirement
        {
            TenantId = tenantId,
            ProjectId = project.Id,
            Name = $"Manual: {request.FeatureName.Trim()}",
            SourceType = RequirementSourceType.PlainText,
            Status = RequirementStatus.Analyzed,
            ExtractedText = request.Description?.Trim(),
            AnalyzedAtUtc = _clock.UtcNow,
            Modules = { module },
        };

        // Add via the aggregate root so EF marks the whole new subgraph as Added.
        _db.Requirements.Add(requirement);
        await _db.SaveChangesAsync(cancellationToken);

        return new ManualFeatureDto
        {
            RequirementId = requirement.Id,
            ModuleId = module.Id,
            FeatureId = feature.Id,
            FeatureName = feature.Name,
            ModuleName = module.Name,
        };
    }
}
