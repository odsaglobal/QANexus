using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.KnowledgeGraph.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.KnowledgeGraph.Queries.GetKnowledgeGraph;

public sealed class GetKnowledgeGraphQueryHandler : IRequestHandler<GetKnowledgeGraphQuery, KnowledgeGraphDto>
{
    private readonly IApplicationDbContext _db;

    public GetKnowledgeGraphQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<KnowledgeGraphDto> Handle(
        GetKnowledgeGraphQuery request,
        CancellationToken cancellationToken)
    {
        var project = await _db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Project), request.ProjectId);

        var modules = await _db.RequirementModules
            .AsNoTracking()
            .Include(m => m.Features).ThenInclude(f => f.Scenarios)
            .Where(m => m.ProjectId == request.ProjectId)
            .ToListAsync(cancellationToken);

        var nodes = new List<GraphNodeDto>();
        var edges = new List<GraphEdgeDto>();

        var projectNodeId = $"project:{project.Id}";
        nodes.Add(new GraphNodeDto
        {
            Id = projectNodeId,
            Type = "project",
            Label = project.Name,
            Sublabel = project.Key,
        });

        foreach (var module in modules)
        {
            var moduleNodeId = $"module:{module.Id}";
            nodes.Add(new GraphNodeDto
            {
                Id = moduleNodeId,
                Type = "module",
                Label = module.Name,
                Sublabel = $"{module.Features.Count} features",
            });
            edges.Add(Edge(projectNodeId, moduleNodeId));

            foreach (var feature in module.Features)
            {
                var featureNodeId = $"feature:{feature.Id}";
                nodes.Add(new GraphNodeDto
                {
                    Id = featureNodeId,
                    Type = "feature",
                    Label = feature.Name,
                    Sublabel = feature.Priority.ToString(),
                });
                edges.Add(Edge(moduleNodeId, featureNodeId));

                foreach (var scenario in feature.Scenarios)
                {
                    var scenarioNodeId = $"scenario:{scenario.Id}";
                    nodes.Add(new GraphNodeDto
                    {
                        Id = scenarioNodeId,
                        Type = "scenario",
                        Label = scenario.Title,
                        Sublabel = scenario.Type.ToString(),
                    });
                    edges.Add(Edge(featureNodeId, scenarioNodeId));
                }
            }
        }

        return new KnowledgeGraphDto { Nodes = nodes, Edges = edges };
    }

    private static GraphEdgeDto Edge(string source, string target) => new()
    {
        Id = $"{source}->{target}",
        Source = source,
        Target = target,
    };
}
