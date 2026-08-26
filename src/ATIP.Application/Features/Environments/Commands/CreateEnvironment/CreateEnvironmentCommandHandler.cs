using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Environments.Dtos;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using EnvEntity = ATIP.Domain.Entities.Environment;

namespace ATIP.Application.Features.Environments.Commands.CreateEnvironment;

public sealed class CreateEnvironmentCommandHandler : IRequestHandler<CreateEnvironmentCommand, EnvironmentDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateEnvironmentCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<EnvironmentDto> Handle(CreateEnvironmentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new ForbiddenAccessException("No tenant context is available for the current user.");

        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Project), request.ProjectId);

        // Seed the new environment with the project's existing variable KEYS (empty values) so variables
        // stay in sync across environments — runs never fail on a variable that only existed elsewhere.
        var siblingVariablesJson = await _db.Environments
            .Where(e => e.ProjectId == project.Id && e.VariablesJson != null)
            .Select(e => e.VariablesJson)
            .ToListAsync(cancellationToken);

        var seededKeys = new Dictionary<string, EnvironmentVariableDto>(StringComparer.Ordinal);
        foreach (var json in siblingVariablesJson)
        {
            foreach (var variable in EnvironmentVariableSerialization.Parse(json))
            {
                // Inherit the key + its type from siblings, but start with an empty value.
                seededKeys.TryAdd(variable.Key, new EnvironmentVariableDto
                {
                    Key = variable.Key,
                    Value = string.Empty,
                    Type = variable.Type,
                });
            }
        }

        var environment = new EnvEntity
        {
            TenantId = tenantId,
            ProjectId = project.Id,
            Name = request.Name.Trim(),
            Type = Enum.Parse<EnvironmentType>(request.Type, ignoreCase: true),
            BaseUrl = request.BaseUrl.Trim(),
            IsDefault = request.IsDefault,
            VariablesJson = EnvironmentVariableSerialization.Serialize(seededKeys.Values)
        };

        if (request.IsDefault)
        {
            await ClearExistingDefaultAsync(project.Id, cancellationToken);
        }

        _db.Environments.Add(environment);
        await _db.SaveChangesAsync(cancellationToken);

        return EnvironmentDto.FromEntity(environment);
    }

    private async Task ClearExistingDefaultAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var existingDefaults = await _db.Environments
            .Where(e => e.ProjectId == projectId && e.IsDefault)
            .ToListAsync(cancellationToken);

        foreach (var env in existingDefaults)
        {
            env.IsDefault = false;
        }
    }
}
