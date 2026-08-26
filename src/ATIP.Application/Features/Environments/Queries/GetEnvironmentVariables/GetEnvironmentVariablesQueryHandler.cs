using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Environments.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using EnvEntity = ATIP.Domain.Entities.Environment;

namespace ATIP.Application.Features.Environments.Queries.GetEnvironmentVariables;

public sealed class GetEnvironmentVariablesQueryHandler
    : IRequestHandler<GetEnvironmentVariablesQuery, IReadOnlyList<EnvironmentVariableDto>>
{
    private readonly IApplicationDbContext _db;

    public GetEnvironmentVariablesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<EnvironmentVariableDto>> Handle(
        GetEnvironmentVariablesQuery request,
        CancellationToken cancellationToken)
    {
        var environment = await _db.Environments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.Id == request.EnvironmentId && e.ProjectId == request.ProjectId,
                cancellationToken)
            ?? throw new NotFoundException(nameof(EnvEntity), request.EnvironmentId);

        return EnvironmentVariableSerialization.Parse(environment.VariablesJson);
    }
}
