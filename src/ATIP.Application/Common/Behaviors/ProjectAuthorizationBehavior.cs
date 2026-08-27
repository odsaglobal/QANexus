using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Security;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that enforces project-level RBAC. For any request implementing
/// <see cref="IProjectScopedRequest"/>, the caller must be a member of the target project with at
/// least the required <see cref="ProjectRole"/>. Tenant/platform admins bypass the membership check.
/// </summary>
public sealed class ProjectAuthorizationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ProjectAuthorizationBehavior(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IProjectScopedRequest scoped)
        {
            await EnforceAsync(scoped, cancellationToken);
        }

        return await next();
    }

    private async Task EnforceAsync(IProjectScopedRequest scoped, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("No user context.");

        // Tenant/platform admins have full access within their tenant.
        if (_currentUser.SystemRole is SystemRole.TenantAdmin or SystemRole.PlatformAdmin)
        {
            return;
        }

        var membership = await _db.ProjectMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ProjectId == scoped.ProjectId && m.UserId == userId, cancellationToken);

        if (membership is null)
        {
            throw new ForbiddenAccessException("You are not a member of this project.");
        }

        if ((int)membership.Role < (int)scoped.RequiredRole)
        {
            throw new ForbiddenAccessException(
                $"This action requires the '{scoped.RequiredRole}' project role; you have '{membership.Role}'.");
        }
    }
}
