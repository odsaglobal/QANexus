namespace ATIP.Application.Common.Interfaces;

/// <summary>
/// Blob storage abstraction (local disk in development, Azure Blob / S3 in production).
/// Returns a relative path/key used to retrieve the object later.
/// </summary>
public interface IFileStorage
{
    Task<string> SaveAsync(
        string containerPath,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default);
}
