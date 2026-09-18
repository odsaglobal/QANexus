using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.DataConnections.Dtos;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.DataConnections.Commands.CreateDataConnection;

public sealed class CreateDataConnectionCommandHandler
    : IRequestHandler<CreateDataConnectionCommand, DataConnectionDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ISecretProtector _protector;

    public CreateDataConnectionCommandHandler(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        ISecretProtector protector)
    {
        _db = db;
        _currentUser = currentUser;
        _protector = protector;
    }

    public async Task<DataConnectionDto> Handle(
        CreateDataConnectionCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new ForbiddenAccessException("No tenant context is available for the current user.");

        var environment = await _db.Environments
            .FirstOrDefaultAsync(
                e => e.Id == request.EnvironmentId && e.ProjectId == request.ProjectId,
                cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Environment), request.EnvironmentId);

        var name = request.Name.Trim();

        var duplicate = await _db.DataConnections
            .AnyAsync(c => c.EnvironmentId == environment.Id && c.Name == name && !c.IsDeleted, cancellationToken);

        if (duplicate)
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(request.Name), $"A data connection named '{name}' already exists in this environment.")
            ]);
        }

        var secret = _protector.Protect(request.ConnectionString);

        var connection = new DataConnection
        {
            TenantId = tenantId,
            ProjectId = environment.ProjectId,
            EnvironmentId = environment.Id,
            Name = name,
            Provider = Enum.Parse<DataProviderKind>(request.Provider, ignoreCase: true),
            EncryptedConnectionString = secret.Ciphertext,
            EncryptionNonce = secret.Nonce,
            KeyId = secret.KeyId,
            CommandTimeoutSeconds = request.CommandTimeoutSeconds,
            ReadOnly = request.ReadOnly
        };

        _db.DataConnections.Add(connection);
        await _db.SaveChangesAsync(cancellationToken);

        return DataConnectionDto.FromEntity(connection);
    }
}
