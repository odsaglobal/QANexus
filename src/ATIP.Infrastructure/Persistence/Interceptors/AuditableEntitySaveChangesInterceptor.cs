using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ATIP.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Populates audit columns and converts hard deletes of <see cref="ISoftDeletable"/> entities
/// into soft deletes just before changes are persisted, keeping this cross-cutting concern
/// out of every handler.
/// </summary>
public sealed class AuditableEntitySaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public AuditableEntitySaveChangesInterceptor(ICurrentUser currentUser, IDateTimeProvider clock)
    {
        _currentUser = currentUser;
        _clock = clock;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = _clock.UtcNow;
        var actor = _currentUser.Email ?? _currentUser.UserId?.ToString();

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    entry.Entity.CreatedBy = actor;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = now;
                    entry.Entity.UpdatedBy = actor;
                    break;
                case EntityState.Deleted when entry.Entity is ISoftDeletable:
                    ConvertToSoftDelete(entry, now);
                    break;
            }
        }
    }

    private static void ConvertToSoftDelete(EntityEntry<AuditableEntity> entry, DateTimeOffset now)
    {
        entry.State = EntityState.Modified;
        var soft = (ISoftDeletable)entry.Entity;
        soft.IsDeleted = true;
        soft.DeletedAtUtc = now;
    }
}
