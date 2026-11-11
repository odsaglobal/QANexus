namespace ATIP.Application.Common.Interfaces;

/// <summary>
/// Files a project's tests may upload into the application under test (resumes, images, CSVs).
/// Uploads are restricted to this store so a test can never send arbitrary server files elsewhere.
/// </summary>
public interface ITestFileStore
{
    /// <summary>Absolute directory holding the project's test files (created on demand).</summary>
    string DirectoryFor(Guid projectId);

    IReadOnlyList<TestFileInfo> List(Guid projectId);

    Task<TestFileInfo> SaveAsync(Guid projectId, string fileName, Stream content, CancellationToken cancellationToken = default);

    bool Delete(Guid projectId, string fileName);

    /// <summary>Absolute path of a stored file, or null when the name is unsafe or the file is missing.</summary>
    string? Resolve(Guid projectId, string fileName);

    /// <summary>True for a bare file name that is safe to store.</summary>
    bool IsValidName(string? fileName);
}

public sealed record TestFileInfo(string Name, long SizeBytes, DateTimeOffset UpdatedAtUtc);
