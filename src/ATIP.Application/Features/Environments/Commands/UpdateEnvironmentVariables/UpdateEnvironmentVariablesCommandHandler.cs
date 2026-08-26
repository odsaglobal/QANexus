using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Environments.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using EnvEntity = ATIP.Domain.Entities.Environment;

namespace ATIP.Application.Features.Environments.Commands.UpdateEnvironmentVariables;

public sealed class UpdateEnvironmentVariablesCommandHandler
    : IRequestHandler<UpdateEnvironmentVariablesCommand, IReadOnlyList<EnvironmentVariableDto>>
{
    private readonly IApplicationDbContext _db;

    public UpdateEnvironmentVariablesCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<EnvironmentVariableDto>> Handle(
        UpdateEnvironmentVariablesCommand request,
        CancellationToken cancellationToken)
    {
        // Load every environment in the project so variable KEYS can be kept in sync across all of them
        // (Postman-style): a key added to one environment appears in all, so runs in a different
        // environment never fail on a missing variable — only the per-environment value differs.
        var environments = await _db.Environments
            .Where(e => e.ProjectId == request.ProjectId)
            .ToListAsync(cancellationToken);

        var target = environments.FirstOrDefault(e => e.Id == request.EnvironmentId)
            ?? throw new NotFoundException(nameof(EnvEntity), request.EnvironmentId);

        // Normalize the incoming variables for the target environment: trim keys, drop empty, last wins.
        var targetVars = new Dictionary<string, EnvironmentVariableDto>(StringComparer.Ordinal);
        foreach (var variable in request.Variables)
        {
            var key = variable.Key?.Trim();
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }
            targetVars[key] = new EnvironmentVariableDto
            {
                Key = key,
                Value = variable.Value,
                Type = EnvironmentVariableSerialization.NormalizeType(variable.Type),
            };
        }
        target.VariablesJson = EnvironmentVariableSerialization.Serialize(targetVars.Values);

        // Union of all keys across every environment (target's types win — types are shared per key).
        var keyType = new Dictionary<string, string>(StringComparer.Ordinal);
        var parsed = new Dictionary<Guid, Dictionary<string, EnvironmentVariableDto>>();
        foreach (var env in environments)
        {
            var vars = env.Id == target.Id
                ? targetVars
                : EnvironmentVariableSerialization.Parse(env.VariablesJson)
                    .ToDictionary(v => v.Key, v => v, StringComparer.Ordinal);
            parsed[env.Id] = vars;
            foreach (var kv in vars)
            {
                keyType.TryAdd(kv.Key, kv.Value.Type);
            }
        }
        foreach (var kv in targetVars)
        {
            keyType[kv.Key] = kv.Value.Type;
        }

        // Ensure every environment has every key with the shared type. Missing keys get an empty value.
        foreach (var env in environments)
        {
            var vars = parsed[env.Id];
            var changed = false;
            foreach (var (key, type) in keyType)
            {
                if (!vars.TryGetValue(key, out var existing))
                {
                    vars[key] = new EnvironmentVariableDto { Key = key, Value = string.Empty, Type = type };
                    changed = true;
                }
                else if (!string.Equals(existing.Type, type, StringComparison.Ordinal))
                {
                    vars[key] = existing with { Type = type };
                    changed = true;
                }
            }

            if (changed || env.Id == target.Id)
            {
                env.VariablesJson = EnvironmentVariableSerialization.Serialize(vars.Values);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return parsed[target.Id].Values
            .OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
