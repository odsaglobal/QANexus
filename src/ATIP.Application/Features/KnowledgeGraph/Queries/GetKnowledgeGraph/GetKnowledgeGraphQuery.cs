using ATIP.Application.Features.KnowledgeGraph.Dtos;
using MediatR;

namespace ATIP.Application.Features.KnowledgeGraph.Queries.GetKnowledgeGraph;

/// <summary>Builds the knowledge graph (project → modules → features → scenarios) for a project.</summary>
public sealed record GetKnowledgeGraphQuery(Guid ProjectId) : IRequest<KnowledgeGraphDto>;
