using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Environments.Dtos;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using EnvEntity = ATIP.Domain.Entities.Environment;

namespace ATIP.Application.Features.Environments.Commands.UpdateEnvironment;

public sealed class UpdateEnvironmentCommandHandler : IRequestHandler<UpdateEnvironmentCommand, EnvironmentDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateEnvironmentCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<EnvironmentDto> Handle(UpdateEnvironmentCommand request, CancellationToken cancellationToken)
    {
        var environment = await _db.Environments
            .FirstOrDefaultAsync(
                e => e.Id == request.Id && e.ProjectId == request.ProjectId,
                cancellationToken)
            ?? throw new NotFoundException(nameof(EnvEntity), request.Id);

        environment.Name = request.Name.Trim();
        environment.Type = Enum.Parse<EnvironmentType>(request.Type, ignoreCase: true);
        environment.BaseUrl = request.BaseUrl.Trim();

        if (request.IsDefault && !environment.IsDefault)
        {
            await ClearExistingDefaultAsync(environment.ProjectId, cancellationToken);
        }
        environment.IsDefault = request.IsDefault;

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
