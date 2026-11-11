using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.TestFiles;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

/// <summary>Files a project's tests may upload into the application under test.</summary>
[Route("api/v1/projects/{projectId:guid}/test-files")]
public sealed class TestFilesController : ApiControllerBase
{
    private const long MaxFileBytes = 25 * 1024 * 1024;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TestFileInfo>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TestFileInfo>>> List(Guid projectId, CancellationToken cancellationToken) =>
        Ok(await Mediator.Send(new ListTestFilesQuery(projectId), cancellationToken));

    [HttpPost]
    [RequestSizeLimit(MaxFileBytes + 1024 * 1024)]
    [ProducesResponseType(typeof(TestFileInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TestFileInfo>> Upload(Guid projectId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0 || file.Length > MaxFileBytes)
        {
            return BadRequest($"Test files must be between 1 byte and {MaxFileBytes / (1024 * 1024)} MB.");
        }

        await using var stream = file.OpenReadStream();
        return Ok(await Mediator.Send(new UploadTestFileCommand(projectId, Path.GetFileName(file.FileName), stream), cancellationToken));
    }

    [HttpDelete("{fileName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid projectId, string fileName, CancellationToken cancellationToken) =>
        await Mediator.Send(new DeleteTestFileCommand(projectId, fileName), cancellationToken) ? NoContent() : NotFound();
}
