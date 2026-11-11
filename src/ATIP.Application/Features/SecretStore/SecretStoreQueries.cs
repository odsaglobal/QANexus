using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using MediatR;

namespace ATIP.Application.Features.SecretStore;

public sealed record GetSecretStoreStatusQuery : IRequest<SecretStoreStatus>;

public sealed class GetSecretStoreStatusQueryHandler : IRequestHandler<GetSecretStoreStatusQuery, SecretStoreStatus>
{
    private readonly ISecretStore _store;

    public GetSecretStoreStatusQueryHandler(ISecretStore store) => _store = store;

    public Task<SecretStoreStatus> Handle(GetSecretStoreStatusQuery request, CancellationToken cancellationToken) =>
        _store.GetStatusAsync(cancellationToken);
}

/// <summary>Checks that a reference resolves, without ever returning the value.</summary>
public sealed record VerifySecretReferenceQuery(string Reference) : IRequest<SecretReferenceCheck>;

public sealed record SecretReferenceCheck(bool Ok, string? Error);

public sealed class VerifySecretReferenceQueryHandler : IRequestHandler<VerifySecretReferenceQuery, SecretReferenceCheck>
{
    private readonly ISecretStore _store;
    private readonly ICurrentUser _currentUser;

    public VerifySecretReferenceQueryHandler(ISecretStore store, ICurrentUser currentUser)
    {
        _store = store;
        _currentUser = currentUser;
    }

    public async Task<SecretReferenceCheck> Handle(VerifySecretReferenceQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new ForbiddenAccessException("No tenant context is available for the current user.");

        try
        {
            await _store.GetSecretAsync(tenantId, request.Reference, cancellationToken);
            return new SecretReferenceCheck(true, null);
        }
        catch (SecretStoreException ex)
        {
            return new SecretReferenceCheck(false, ex.Message);
        }
    }
}
