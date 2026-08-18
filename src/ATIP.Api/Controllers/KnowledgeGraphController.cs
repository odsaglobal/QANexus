using ATIP.Application.Features.KnowledgeGraph.Dtos;
using ATIP.Application.Features.KnowledgeGraph.Queries.GetKnowledgeGraph;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

/// <summary>Knowledge graph of a project (project → modules → features → scenarios).</summary>
[Route("api/v1/projects/{projectId:guid}/knowledge-graph")]
public sealed class KnowledgeGraphController : ApiControllerBase
{
    /// <summary>Returns the nodes and edges of the project's knowledge graph.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(KnowledgeGraphDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KnowledgeGraphDto>> Get(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetKnowledgeGraphQuery(projectId), cancellationToken);
        return Ok(result);
    }
}
