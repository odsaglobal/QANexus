using ATIP.Application.Features.Reports.Dtos;
using ATIP.Application.Features.Reports.Queries.GetReportsSummary;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

/// <summary>Tenant-scoped quality reports backed by real run + step data.</summary>
[Route("api/v1/reports")]
public sealed class ReportsController : ApiControllerBase
{
    /// <summary>Returns real execution/step metrics, scenario mix, weekly trend and top failing scenarios.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ReportsSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportsSummaryDto>> GetSummary(CancellationToken ct)
    {
        var result = await Mediator.Send(new GetReportsSummaryQuery(), ct);
        return Ok(result);
    }
}
