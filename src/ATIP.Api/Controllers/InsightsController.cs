using ATIP.Application.Features.Insights.Dtos;
using ATIP.Application.Features.Insights.Queries.GetAiInsights;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

[Route("api/v1/insights")]
public sealed class InsightsController : ApiControllerBase
{
    /// <summary>
    /// Quality findings derived from execution history and authoring state, tenant-wide or scoped
    /// to a single project.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(AiInsightsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AiInsightsDto>> Get([FromQuery] Guid? projectId, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetAiInsightsQuery(projectId), ct);
        return Ok(result);
    }
}
