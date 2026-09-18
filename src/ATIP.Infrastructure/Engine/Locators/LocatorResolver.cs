using ATIP.Application.Engine.Contracts;
using ATIP.Application.Engine.Model;
using ATIP.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ATIP.Infrastructure.Engine.Locators;

/// <summary>
/// Merges stored locator history with the candidates an action carries inline, and feeds the
/// result of each attempt back so the ordering keeps improving.
/// </summary>
public sealed class LocatorResolver : ILocatorResolver
{
    private readonly ILocatorRepository _repository;
    private readonly ILogger<LocatorResolver> _logger;

    public LocatorResolver(ILocatorRepository repository, ILogger<LocatorResolver> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LocatorCandidate>> ResolveAsync(
        ElementTarget target,
        RunContext context,
        CancellationToken cancellationToken)
    {
        var merged = new List<LocatorCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Stored candidates lead: they are the only ones backed by evidence that they worked
        // against this application rather than by a guess made when the step was written.
        if (!string.IsNullOrWhiteSpace(target.ElementKey))
        {
            var stored = await _repository.GetCandidatesAsync(
                context.TenantId,
                context.ProjectId,
                target.ElementKey!,
                cancellationToken);

            foreach (var candidate in stored)
            {
                if (seen.Add(Signature(candidate)))
                {
                    merged.Add(candidate);
                }
            }
        }

        foreach (var candidate in target.Candidates.OrderBy(c => c.Rank))
        {
            if (seen.Add(Signature(candidate)))
            {
                merged.Add(candidate);
            }
        }

        return merged;
    }

    public async Task ReportOutcomeAsync(
        ElementTarget target,
        IReadOnlyList<LocatorCandidate> attempted,
        LocatorCandidate? winner,
        RunContext context,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in attempted)
        {
            if (candidate.LocatorId is not { } id)
            {
                // Inline candidate — nothing persisted to score.
                continue;
            }

            if (winner is not null && candidate.LocatorId == winner.LocatorId)
            {
                await _repository.RecordSuccessAsync(id, cancellationToken);
                // Everything after the winner was never tried, so it has no outcome to record.
                break;
            }

            await _repository.RecordFailureAsync(id, cancellationToken);
        }

        await PromoteAsync(target, winner, context, cancellationToken);
    }

    /// <summary>
    /// Persists a winner that was not already in the store, so the next run starts from proven
    /// knowledge instead of re-deriving it.
    /// </summary>
    /// <remarks>
    /// This is the only thing that ever populates the object repository, and it is deliberately
    /// driven by success rather than by authoring: a locator earns its place by having actually
    /// resolved against the live application. Targets with no stable identity (no key and no
    /// descriptor) are skipped — keying them off a raw CSS path would fill the repository with
    /// entries nobody can recognise or reuse.
    /// </remarks>
    private async Task PromoteAsync(
        ElementTarget target,
        LocatorCandidate? winner,
        RunContext context,
        CancellationToken cancellationToken)
    {
        if (winner is null || winner.LocatorId is not null || winner.Volatile)
        {
            return;
        }

        var key = RepositoryKey(target);
        if (key is null)
        {
            return;
        }

        // A candidate the action carried explicitly was recorded against this application; one the
        // driver derived from the descriptor was discovered by probing. The distinction matters
        // because it seeds a different starting rank.
        var origin = target.Candidates.Any(c => Signature(c).Equals(Signature(winner), StringComparison.OrdinalIgnoreCase))
            ? LocatorOrigin.Recorded
            : LocatorOrigin.Discovered;

        try
        {
            await _repository.UpsertLocatorAsync(
                context.TenantId,
                context.ProjectId,
                key,
                target.Descriptor ?? key,
                TestPlatform.Web,
                winner.Strategy,
                winner.Value,
                origin,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Learning is an optimisation. A step that just passed must not be reported as failed
            // because we could not write down how it passed.
            _logger.LogWarning(ex, "Could not promote locator for element key {ElementKey}.", key);
        }
    }

    /// <summary>
    /// The repository key for a target: its explicit key, else a slug of its human descriptor.
    /// </summary>
    private static string? RepositoryKey(ElementTarget target)
    {
        if (!string.IsNullOrWhiteSpace(target.ElementKey))
        {
            return target.ElementKey!.Trim();
        }

        if (string.IsNullOrWhiteSpace(target.Descriptor))
        {
            return null;
        }

        var slug = new string(target.Descriptor!
            .Trim()
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray());

        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        slug = slug.Trim('-');

        // Long enough to stay readable and distinct, short enough to sit in an index.
        return slug.Length switch
        {
            0 => null,
            > 120 => slug[..120].TrimEnd('-'),
            _ => slug
        };
    }

    private static string Signature(LocatorCandidate candidate) =>
        $"{candidate.Strategy}::{candidate.Value}";
}
