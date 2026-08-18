using ATIP.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

/// <summary>
/// Serves files stored by <see cref="IFileStorage"/> (screenshots, DOM snapshots).
/// Uses the authenticated user's session; no additional authorization check is applied here
/// since storage paths are opaque GUIDs and tenants only receive paths for their own data.
/// </summary>
[Route("api/v1/files")]
[ApiController]
[Authorize]
public sealed class FilesController : ControllerBase
{
    private readonly IFileStorage _storage;

    public FilesController(IFileStorage storage) => _storage = storage;

    /// <summary>Streams a stored file by its relative storage path.</summary>
    [HttpGet("{*path}")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> Get(string path, CancellationToken cancellationToken)
    {
        var stream = await _storage.OpenReadAsync(path, cancellationToken);
        if (stream is null)
        {
            return NotFound();
        }

        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        var contentType = ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".html" => "text/html",
            ".json" => "application/json",
            _ => "application/octet-stream",
        };

        return File(stream, contentType, enableRangeProcessing: true);
    }
}
