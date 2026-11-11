using System.Text.RegularExpressions;
using ATIP.Application.Common.Interfaces;
using ATIP.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace ATIP.Infrastructure.Storage;

/// <summary>
/// Keeps test upload files on local disk, one folder per project, deliberately OUTSIDE the general
/// file-storage root so they are never reachable through the generic files endpoint.
/// </summary>
public sealed partial class LocalTestFileStore : ITestFileStore
{
    private readonly string _root;

    public LocalTestFileStore(IOptions<StorageOptions> options)
    {
        _root = Path.GetFullPath(options.Value.TestFilesPath);
        Directory.CreateDirectory(_root);
    }

    public string DirectoryFor(Guid projectId)
    {
        var dir = Path.Combine(_root, projectId.ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    public IReadOnlyList<TestFileInfo> List(Guid projectId) =>
        new DirectoryInfo(DirectoryFor(projectId))
            .EnumerateFiles()
            .Where(f => IsValidName(f.Name))
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => new TestFileInfo(f.Name, f.Length, f.LastWriteTimeUtc))
            .ToList();

    public async Task<TestFileInfo> SaveAsync(Guid projectId, string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        if (!IsValidName(fileName))
        {
            throw new ArgumentException($"'{fileName}' is not a valid test file name.", nameof(fileName));
        }

        var path = Path.Combine(DirectoryFor(projectId), fileName);
        await using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        {
            await content.CopyToAsync(stream, cancellationToken);
        }

        var info = new FileInfo(path);
        return new TestFileInfo(info.Name, info.Length, info.LastWriteTimeUtc);
    }

    public bool Delete(Guid projectId, string fileName)
    {
        var path = Resolve(projectId, fileName);
        if (path is null)
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    public string? Resolve(Guid projectId, string fileName)
    {
        if (!IsValidName(fileName))
        {
            return null;
        }

        var dir = DirectoryFor(projectId);
        var path = Path.GetFullPath(Path.Combine(dir, fileName));
        return path.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.Ordinal) && File.Exists(path) ? path : null;
    }

    public bool IsValidName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName) && SafeName().IsMatch(fileName);

    // A bare name only: no separators, no leading dot, so it can never escape the project folder.
    [GeneratedRegex(@"^(?!\.)[A-Za-z0-9 ._()\-]{1,120}$")]
    private static partial Regex SafeName();
}
