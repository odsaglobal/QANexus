using ATIP.Application.Features.TestData.Commands.CreateTestDataSet;
using ATIP.Application.Features.TestData.Commands.DeleteTestDataSet;
using ATIP.Application.Features.TestData.Commands.UpdateTestDataSet;
using ATIP.Application.Features.TestData.Dtos;
using ATIP.Application.Features.TestData.Queries.ListTestDataSets;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

/// <summary>
/// Test data endpoints, nested under an environment. Test data is environment-specific:
/// each data set belongs to exactly one environment of a project.
/// </summary>
[Route("api/v1/projects/{projectId:guid}/environments/{environmentId:guid}/test-data")]
public sealed class TestDataController : ApiControllerBase
{
    /// <summary>Lists all test data sets for the environment.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TestDataSetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TestDataSetDto>>> List(
        Guid projectId,
        Guid environmentId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ListTestDataSetsQuery(projectId, environmentId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Creates a new test data set within the environment.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TestDataSetDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestDataSetDto>> Create(
        Guid projectId,
        Guid environmentId,
        CreateTestDataSetCommand command,
        CancellationToken cancellationToken)
    {
        if (projectId != command.ProjectId || environmentId != command.EnvironmentId)
        {
            return BadRequest("Route projectId/environmentId and body values do not match.");
        }

        var result = await Mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(List), new { projectId, environmentId }, result);
    }

    /// <summary>Updates an existing test data set.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TestDataSetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestDataSetDto>> Update(
        Guid projectId,
        Guid environmentId,
        Guid id,
        UpdateTestDataSetCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id || environmentId != command.EnvironmentId)
        {
            return BadRequest("Route ids and body ids do not match.");
        }

        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>Deletes a test data set.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid projectId,
        Guid environmentId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteTestDataSetCommand(id), cancellationToken);
        return NoContent();
    }
}
