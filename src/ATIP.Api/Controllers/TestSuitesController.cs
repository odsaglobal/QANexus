using ATIP.Application.Features.Explorer.Dtos;
using ATIP.Application.Features.TestSuites.Commands.CreateTestSuite;
using ATIP.Application.Features.TestSuites.Commands.DeleteTestSuite;
using ATIP.Application.Features.TestSuites.Commands.RunTestSuite;
using ATIP.Application.Features.TestSuites.Commands.UpdateTestSuite;
using ATIP.Application.Features.TestSuites.Dtos;
using ATIP.Application.Features.TestSuites.Queries.GetTestSuite;
using ATIP.Application.Features.TestSuites.Queries.ListTestSuites;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

/// <summary>Test suites: named, ordered groups of scenarios that run together on an environment.</summary>
[Route("api/v1/projects/{projectId:guid}/suites")]
public sealed class TestSuitesController : ApiControllerBase
{
    /// <summary>Lists all suites for the project with their ordered scenarios.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TestSuiteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TestSuiteDto>>> List(
        Guid projectId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ListTestSuitesQuery(projectId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns a single suite by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TestSuiteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestSuiteDto>> Get(
        Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetTestSuiteQuery(projectId, id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Creates a suite with an ordered set of scenarios.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TestSuiteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TestSuiteDto>> Create(
        Guid projectId,
        CreateTestSuiteCommand command,
        CancellationToken cancellationToken)
    {
        if (projectId != command.ProjectId)
        {
            return BadRequest("Route projectId and body projectId do not match.");
        }

        var result = await Mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { projectId, id = result.Id }, result);
    }

    /// <summary>Updates a suite's name/description and replaces its ordered scenario membership.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TestSuiteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestSuiteDto>> Update(
        Guid projectId,
        Guid id,
        UpdateTestSuiteCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id || projectId != command.ProjectId)
        {
            return BadRequest("Route and body identifiers do not match.");
        }

        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>Soft-deletes a suite.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteTestSuiteCommand(projectId, id), cancellationToken);
        return NoContent();
    }

    /// <summary>Runs the whole suite as one session, walking every scenario's steps sequentially on the chosen environment.</summary>
    [HttpPost("{id:guid}/run")]
    [ProducesResponseType(typeof(ExplorationSessionDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExplorationSessionDto>> Run(
        Guid projectId,
        Guid id,
        [FromBody] RunTestSuiteRequest? body,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new RunTestSuiteCommand
        {
            ProjectId = projectId,
            SuiteId = id,
            EnvironmentId = body?.EnvironmentId,
        }, cancellationToken);
        return Accepted(result);
    }
}

/// <summary>Optional body for the suite run endpoint.</summary>
public sealed record RunTestSuiteRequest
{
    public Guid? EnvironmentId { get; init; }
}
