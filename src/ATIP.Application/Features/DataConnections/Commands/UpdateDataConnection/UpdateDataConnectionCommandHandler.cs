using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.DataConnections.Dtos;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.DataConnections.Commands.UpdateDataConnection;

public sealed class UpdateDataConnectionCommandHandler
    : IRequestHandler<UpdateDataConnectionCommand, DataConnectionDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ISecretProtector _protector;

    public UpdateDataConnectionCommandHandler(IApplicationDbContext db, ISecretProtector protector)
    {
        _db = db;
        _protector = protector;
    }

    public async Task<DataConnectionDto> Handle(
        UpdateDataConnectionCommand request,
        CancellationToken cancellationToken)
    {
        var connection = await _db.DataConnections
            .FirstOrDefaultAsync(
                c => c.Id == request.Id && c.EnvironmentId == request.EnvironmentId && !c.IsDeleted,
                cancellationToken)
            ?? throw new NotFoundException(nameof(DataConnection), request.Id);

        var name = request.Name.Trim();

        var duplicate = await _db.DataConnections
            .AnyAsync(
                c => c.EnvironmentId == request.EnvironmentId && c.Name == name && c.Id != connection.Id && !c.IsDeleted,
                cancellationToken);

        if (duplicate)
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(request.Name), $"A data connection named '{name}' already exists in this environment.")
            ]);
        }

        connection.Name = name;
        connection.Provider = Enum.Parse<DataProviderKind>(request.Provider, ignoreCase: true);
        connection.CommandTimeoutSeconds = request.CommandTimeoutSeconds;
        connection.ReadOnly = request.ReadOnly;

        if (!string.IsNullOrWhiteSpace(request.ConnectionString))
        {
            var secret = _protector.Protect(request.ConnectionString!);
            connection.EncryptedConnectionString = secret.Ciphertext;
            connection.EncryptionNonce = secret.Nonce;
            connection.KeyId = secret.KeyId;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return DataConnectionDto.FromEntity(connection);
    }
}
