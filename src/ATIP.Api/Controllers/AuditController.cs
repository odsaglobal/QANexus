using ATIP.Application.Common.Models;
using ATIP.Application.Features.Audit.Dtos;
using ATIP.Application.Features.Audit.Queries.ListAuditLogs;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

/// <summary>Tenant audit trail (activity log).</summary>
[Route("api/v1/audit")]
public sealed class AuditController : ApiControllerBase
{
    /// <summary>Lists audit-log entries for the current tenant, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditLogEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuditLogEntryDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(new ListAuditLogsQuery(page, pageSize, category), cancellationToken);
        return Ok(result);
    }
}
