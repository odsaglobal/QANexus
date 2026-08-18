using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Utilities;
using ATIP.Application.Features.Projects.Dtos;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Projects.Commands.CreateProject;

public sealed class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, ProjectDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateProjectCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ProjectDto> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new ForbiddenAccessException("No tenant context is available for the current user.");
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("No user context is available.");

        var key = string.IsNullOrWhiteSpace(request.Key)
            ? SlugGenerator.Create(request.Name)
            : request.Key.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(key))
        {
            key = "project";
        }

        key = await EnsureUniqueKeyAsync(tenantId, key, cancellationToken);

        var project = new Project
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Key = key,
            Description = request.Description?.Trim(),
            Status = ProjectStatus.Active
        };

        project.Members.Add(new ProjectMember
        {
            TenantId = tenantId,
            ProjectId = project.Id,
            UserId = userId,
            Role = ProjectRole.Owner
        });

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(cancellationToken);

        return ProjectDto.FromEntity(project);
    }

    private async Task<string> EnsureUniqueKeyAsync(Guid tenantId, string baseKey, CancellationToken cancellationToken)
    {
        var candidate = baseKey;
        var suffix = 1;
        // The unique index on (TenantId, Key) also covers soft-deleted projects, so the
        // uniqueness check must bypass the global query filter to avoid colliding with the
        // key of a previously deleted project (which would fail the insert with a 500).
        while (await _db.Projects
                   .IgnoreQueryFilters()
                   .AnyAsync(p => p.TenantId == tenantId && p.Key == candidate, cancellationToken))
        {
            candidate = $"{baseKey}-{suffix++}";
        }

        return candidate;
    }
}
