using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Tenants.Dtos;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Tenants.Commands.UpdateTenant;

/// <summary>
/// Renames the caller's tenant (workspace) and marks it onboarded. Used both by first-run
/// onboarding ("Name your workspace") and the Settings rename. TenantAdmin only.
/// </summary>
public sealed record UpdateTenantCommand(string Name) : IRequest<TenantDto>;

public sealed class UpdateTenantCommandHandler : IRequestHandler<UpdateTenantCommand, TenantDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpdateTenantCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TenantDto> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new ForbiddenAccessException("No tenant context.");

        if (_currentUser.SystemRole != SystemRole.TenantAdmin)
        {
            throw new ForbiddenAccessException("Only a workspace admin can rename the workspace.");
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.Name), "Workspace name is required."),
            });
        }

        if (name.Length > 200)
        {
            name = name[..200];
        }

        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        tenant.Name = name;
        tenant.IsOnboarded = true;
        await _db.SaveChangesAsync(cancellationToken);

        return TenantDto.FromEntity(tenant);
    }
}
