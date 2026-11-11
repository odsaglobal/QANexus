using ATIP.Infrastructure.Configuration;
using ATIP.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace ATIP.Application.Tests.Common;

public sealed class LocalTestFileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "patternsai-testfiles-" + Guid.NewGuid().ToString("N"));
    private readonly LocalTestFileStore _store;

    public LocalTestFileStoreTests() =>
        _store = new LocalTestFileStore(Options.Create(new StorageOptions { TestFilesPath = _root }));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task Saves_lists_and_resolves_inside_the_project_folder()
    {
        var project = Guid.NewGuid();
        await _store.SaveAsync(project, "resume (1).pdf", new MemoryStream([1, 2, 3]));

        _store.List(project).Should().ContainSingle(f => f.Name == "resume (1).pdf" && f.SizeBytes == 3);
        _store.Resolve(project, "resume (1).pdf").Should().StartWith(_store.DirectoryFor(project));
    }

    [Fact]
    public async Task Files_are_isolated_per_project()
    {
        var a = Guid.NewGuid();
        await _store.SaveAsync(a, "secret.csv", new MemoryStream([1]));

        _store.Resolve(Guid.NewGuid(), "secret.csv").Should().BeNull();
    }

    [Theory]
    [InlineData("../appsettings.json")]
    [InlineData("..")]
    [InlineData(".env")]
    [InlineData("/etc/passwd")]
    [InlineData("a/b.txt")]
    [InlineData("a\\b.txt")]
    public void Rejects_names_that_could_escape_the_folder(string name)
    {
        _store.IsValidName(name).Should().BeFalse();
        _store.Resolve(Guid.NewGuid(), name).Should().BeNull();
    }
}
