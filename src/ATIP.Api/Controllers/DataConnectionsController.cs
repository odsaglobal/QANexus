using ATIP.Application.Features.DataConnections.Commands.CreateDataConnection;
using ATIP.Application.Features.DataConnections.Commands.DeleteDataConnection;
using ATIP.Application.Features.DataConnections.Commands.UpdateDataConnection;
using ATIP.Application.Features.DataConnections.Dtos;
using ATIP.Application.Features.DataConnections.Queries.ListDataConnections;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

/// <summary>
/// Database connections the engine may query during a run, nested under an environment because
/// staging and production point at different databases.
/// </summary>
/// <remarks>
/// There is no endpoint that returns a connection string. Secrets are write-only by design: the
/// engine decrypts them server-side when a step runs, and nothing else ever needs to read them.
/// </remarks>
[Route("api/v1/projects/{projectId:guid}/environments/{environmentId:guid}/data-connections")]
public sealed class DataConnectionsController : ApiControllerBase
{
    /// <summary>Lists the data connections registered for the environment.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DataConnectionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DataConnectionDto>>> List(
        Guid projectId,
        Guid environmentId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ListDataConnectionsQuery(projectId, environmentId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Registers a new data connection.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(DataConnectionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DataConnectionDto>> Create(
        Guid projectId,
        Guid environmentId,
        CreateDataConnectionCommand command,
        CancellationToken cancellationToken)
    {
        if (projectId != command.ProjectId || environmentId != command.EnvironmentId)
        {
            return BadRequest("Route projectId/environmentId and body values do not match.");
        }

        var result = await Mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(List), new { projectId, environmentId }, result);
    }

    /// <summary>Updates a data connection. Omit the connection string to keep the stored secret.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DataConnectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DataConnectionDto>> Update(
        Guid projectId,
        Guid environmentId,
        Guid id,
        UpdateDataConnectionCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id || projectId != command.ProjectId || environmentId != command.EnvironmentId)
        {
            return BadRequest("Route ids and body ids do not match.");
        }

        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>Deletes a data connection.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid projectId,
        Guid environmentId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteDataConnectionCommand(projectId, environmentId, id), cancellationToken);
        return NoContent();
    }
}
