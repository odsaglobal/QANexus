using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Tenants.Settings;

/// <summary>Workspace-wide execution settings.</summary>
public sealed record TenantSettingsDto(int FailedTestRetries);

public sealed record GetTenantSettingsQuery : IRequest<TenantSettingsDto>;

/// <summary>Updates workspace execution settings. TenantAdmin only.</summary>
public sealed record UpdateTenantSettingsCommand(int FailedTestRetries) : IRequest<TenantSettingsDto>;

public sealed class UpdateTenantSettingsCommandValidator : AbstractValidator<UpdateTenantSettingsCommand>
{
    public const int MaxRetries = 3;

    public UpdateTenantSettingsCommandValidator()
    {
        RuleFor(x => x.FailedTestRetries).InclusiveBetween(0, MaxRetries);
    }
}

public sealed class TenantSettingsHandlers :
    IRequestHandler<GetTenantSettingsQuery, TenantSettingsDto>,
    IRequestHandler<UpdateTenantSettingsCommand, TenantSettingsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public TenantSettingsHandlers(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TenantSettingsDto> Handle(GetTenantSettingsQuery request, CancellationToken cancellationToken)
    {
        var tenant = await LoadAsync(cancellationToken);
        return new TenantSettingsDto(tenant.FailedTestRetries);
    }

    public async Task<TenantSettingsDto> Handle(UpdateTenantSettingsCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.SystemRole != SystemRole.TenantAdmin)
        {
            throw new ForbiddenAccessException("Only a workspace admin can change execution settings.");
        }

        var tenant = await LoadAsync(cancellationToken);
        tenant.FailedTestRetries = request.FailedTestRetries;
        await _db.SaveChangesAsync(cancellationToken);
        return new TenantSettingsDto(tenant.FailedTestRetries);
    }

    private async Task<Tenant> LoadAsync(CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");
        return await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);
    }
}
