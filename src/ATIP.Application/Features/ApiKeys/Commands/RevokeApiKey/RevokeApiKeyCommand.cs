using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.ApiKeys.Commands.RevokeApiKey;

/// <summary>Revokes (deactivates) an API key so it can no longer authenticate.</summary>
public sealed record RevokeApiKeyCommand(Guid Id) : IRequest;

public sealed class RevokeApiKeyCommandHandler : IRequestHandler<RevokeApiKeyCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IAuditLogger _audit;

    public RevokeApiKeyCommandHandler(IApplicationDbContext db, IDateTimeProvider clock, IAuditLogger audit)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(RevokeApiKeyCommand request, CancellationToken cancellationToken)
    {
        var key = await _db.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ApiKey), request.Id);

        if (key.RevokedAtUtc is null)
        {
            key.RevokedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await _audit.LogAsync("apikey.revoked", "Security", $"Revoked API key \"{key.Name}\"", nameof(ApiKey), key.Id, cancellationToken);
        }
    }
}
