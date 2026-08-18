using ATIP.Application.Common.Interfaces;
using ATIP.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace ATIP.Infrastructure.Storage;

/// <summary>
/// Local-disk implementation of <see cref="IFileStorage"/> used in development. Files are stored
/// under a configurable root; the returned path is relative to that root so it can later be swapped
/// for an Azure Blob / S3 backend without changing stored references.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<StorageOptions> options)
    {
        _root = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(
        string containerPath,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var safeContainer = SanitizeRelative(containerPath);
        var safeFile = $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";
        var relativePath = Path.Combine(safeContainer, safeFile);
        var absolutePath = Path.Combine(_root, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using var fileStream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(fileStream, cancellationToken);

        // Normalize to forward slashes so stored paths are portable.
        return relativePath.Replace(Path.DirectorySeparatorChar, '/');
    }

    public Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var absolutePath = Path.GetFullPath(Path.Combine(_root, SanitizeRelative(path)));

        // Guard against path traversal outside the storage root.
        if (!absolutePath.StartsWith(_root, StringComparison.Ordinal) || !File.Exists(absolutePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read);
        return Task.FromResult<Stream?>(stream);
    }

    private static string SanitizeRelative(string path)
    {
        var normalized = path.Replace('\\', '/');
        var segments = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(s => s is not "." and not "..");
        return Path.Combine([.. segments]);
    }
}
