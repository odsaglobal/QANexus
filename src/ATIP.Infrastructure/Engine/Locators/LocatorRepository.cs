using ATIP.Application.Common.Interfaces;
using ATIP.Application.Engine.Contracts;
using ATIP.Application.Engine.Model;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ATIP.Infrastructure.Engine.Locators;

/// <summary>
/// EF-backed object repository. Stores every known way to reach an element together with the
/// evidence of whether it still works, so locator quality improves run over run instead of being
/// rediscovered from scratch each time a page changes.
/// </summary>
public sealed class LocatorRepository : ILocatorRepository
{
    /// <summary>
    /// Consecutive misses tolerated before a locator is parked. Three is enough to ride out a
    /// transient (a slow render, a one-off overlay) without letting a genuinely dead selector keep
    /// charging a full timeout to every future run.
    /// </summary>
    private const int QuarantineThreshold = 3;

    private readonly IApplicationDbContext _db;
    private readonly ILogger<LocatorRepository> _logger;

    public LocatorRepository(IApplicationDbContext db, ILogger<LocatorRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LocatorCandidate>> GetCandidatesAsync(
        Guid tenantId,
        Guid projectId,
        string elementKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(elementKey))
        {
            return [];
        }

        var key = elementKey.Trim();

        var rows = await _db.UiElementLocators
            .IgnoreQueryFilters()
            .Where(l => l.TenantId == tenantId
                && !l.IsQuarantined
                && l.Element.ProjectId == projectId
                && l.Element.Key == key
                && !l.Element.IsDeleted)
            .OrderBy(l => l.Rank)
            .ThenByDescending(l => l.SuccessCount)
            .ThenBy(l => l.FailureCount)
            .Select(l => new { l.Id, l.Strategy, l.Value, l.Rank })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new LocatorCandidate
            {
                LocatorId = r.Id,
                Strategy = r.Strategy,
                Value = r.Value,
                Rank = r.Rank
            })
            .ToList();
    }

    public async Task RecordSuccessAsync(Guid locatorId, CancellationToken cancellationToken)
    {
        var locator = await _db.UiElementLocators
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Id == locatorId, cancellationToken);

        if (locator is null)
        {
            return;
        }

        locator.SuccessCount++;
        // Consecutive counter: one success means the locator is alive again, whatever came before.
        locator.FailureCount = 0;
        locator.IsQuarantined = false;
        locator.LastSucceededAtUtc = DateTimeOffset.UtcNow;

        // Promote the winner so the next run reaches it first. Clamped at zero: ranks stay
        // non-negative so a long-serving locator cannot run away and become unbeatable by a
        // better one added later.
        if (locator.Rank > 0)
        {
            locator.Rank--;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordFailureAsync(Guid locatorId, CancellationToken cancellationToken)
    {
        var locator = await _db.UiElementLocators
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Id == locatorId, cancellationToken);

        if (locator is null)
        {
            return;
        }

        locator.FailureCount++;
        locator.LastFailedAtUtc = DateTimeOffset.UtcNow;
        locator.Rank++;

        if (locator.FailureCount >= QuarantineThreshold)
        {
            locator.IsQuarantined = true;
            _logger.LogInformation(
                "Quarantined locator {LocatorId} ({Strategy}) after {Failures} consecutive failures.",
                locator.Id,
                locator.Strategy,
                locator.FailureCount);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> UpsertLocatorAsync(
        Guid tenantId,
        Guid projectId,
        string elementKey,
        string elementName,
        TestPlatform platform,
        LocatorStrategy strategy,
        string value,
        LocatorOrigin origin,
        CancellationToken cancellationToken)
    {
        var key = elementKey.Trim();

        var element = await _db.UiElements
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                e => e.TenantId == tenantId && e.ProjectId == projectId && e.Key == key && !e.IsDeleted,
                cancellationToken);

        if (element is null)
        {
            element = new UiElement
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                Key = key,
                Name = string.IsNullOrWhiteSpace(elementName) ? key : elementName.Trim(),
                Platform = platform
            };

            _db.UiElements.Add(element);
        }

        var existing = await _db.UiElementLocators
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                l => l.ElementId == element.Id && l.Strategy == strategy && l.Value == value,
                cancellationToken);

        if (existing is not null)
        {
            // The healer rediscovered a locator we already knew about. Treat that as evidence in
            // its favour rather than inserting a duplicate.
            existing.IsQuarantined = false;
            existing.FailureCount = 0;
            existing.Origin = origin;
            await _db.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }

        var locator = new UiElementLocator
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ElementId = element.Id,
            Element = element,
            Strategy = strategy,
            Value = value,
            Origin = origin,
            Rank = InitialRank(strategy, origin),
            LastSucceededAtUtc = DateTimeOffset.UtcNow,
            SuccessCount = 1
        };

        _db.UiElementLocators.Add(locator);
        element.LastResolvedAtUtc = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return locator.Id;
    }

    /// <summary>
    /// Seeds a new locator's position from how durable its strategy tends to be. A dedicated test
    /// hook survives redesigns; a generated CSS path or an absolute XPath breaks on the next
    /// layout change, so those start at the back and have to earn their way forward.
    /// </summary>
    private static int InitialRank(LocatorStrategy strategy, LocatorOrigin origin)
    {
        var baseRank = strategy switch
        {
            LocatorStrategy.TestId or LocatorStrategy.DataAttribute => 10,
            LocatorStrategy.AccessibilityId => 12,
            LocatorStrategy.ARIA => 20,
            LocatorStrategy.Role => 25,
            LocatorStrategy.Label => 30,
            LocatorStrategy.Placeholder => 35,
            LocatorStrategy.AltText or LocatorStrategy.Title => 40,
            LocatorStrategy.Text => 45,
            LocatorStrategy.AndroidUiAutomator or LocatorStrategy.IosPredicate or LocatorStrategy.IosClassChain => 50,
            LocatorStrategy.CSS => 60,
            LocatorStrategy.XPath => 70,
            LocatorStrategy.NearbyLabel => 75,
            LocatorStrategy.Visual => 90,
            _ => 80
        };

        // A locator a human curated is a stronger claim than one a model guessed under pressure.
        return origin switch
        {
            LocatorOrigin.Manual => baseRank - 5,
            LocatorOrigin.Healed => baseRank + 5,
            _ => baseRank
        };
    }
}
