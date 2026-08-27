using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Security;
using ATIP.Application.Features.ApiKeys.Dtos;
using ATIP.Domain.Entities;
using MediatR;

namespace ATIP.Application.Features.ApiKeys.Commands.CreateApiKey;

/// <summary>Creates a tenant API key and returns the plaintext exactly once.</summary>
public sealed record CreateApiKeyCommand(string Name, DateTimeOffset? ExpiresAtUtc = null)
    : IRequest<CreatedApiKeyDto>;

public sealed class CreateApiKeyCommandHandler : IRequestHandler<CreateApiKeyCommand, CreatedApiKeyDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;

    public CreateApiKeyCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, IAuditLogger audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<CreatedApiKeyDto> Handle(CreateApiKeyCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("No user context.");

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException([
                new FluentValidation.Results.ValidationFailure(nameof(request.Name), "A name is required."),
            ]);
        }

        var (plainText, hash, prefix) = ApiKeyHashing.Generate();

        var key = new ApiKey
        {
            TenantId = tenantId,
            Name = name,
            KeyHash = hash,
            Prefix = prefix,
            CreatedByUserId = userId,
            ExpiresAtUtc = request.ExpiresAtUtc,
        };

        _db.ApiKeys.Add(key);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync("apikey.created", "Security", $"Created API key \"{name}\"", nameof(ApiKey), key.Id, cancellationToken);

        return new CreatedApiKeyDto { Key = ApiKeyDto.FromEntity(key), PlainText = plainText };
    }
}
