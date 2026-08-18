using ATIP.Application.Features.Dashboard.Dtos;
using ATIP.Application.Features.Dashboard.Queries.GetDashboardSummary;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

/// <summary>Tenant-scoped dashboard analytics backed by real persisted data.</summary>
[Route("api/v1/dashboard")]
public sealed class DashboardController : ApiControllerBase
{
    /// <summary>Returns real counts (projects, requirements, features, scenarios by source, exploration).</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken ct)
    {
        var result = await Mediator.Send(new GetDashboardSummaryQuery(), ct);
        return Ok(result);
    }
}
