using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Security;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.TestFiles;

public sealed record ListTestFilesQuery(Guid ProjectId) : IRequest<IReadOnlyList<TestFileInfo>>, IProjectScopedRequest
{
    public ProjectRole RequiredRole => ProjectRole.Viewer;
}

public sealed record UploadTestFileCommand(Guid ProjectId, string FileName, Stream Content) : IRequest<TestFileInfo>, IProjectScopedRequest
{
    public ProjectRole RequiredRole => ProjectRole.Editor;
}

public sealed record DeleteTestFileCommand(Guid ProjectId, string FileName) : IRequest<bool>, IProjectScopedRequest
{
    public ProjectRole RequiredRole => ProjectRole.Editor;
}

public sealed class TestFileHandlers :
    IRequestHandler<ListTestFilesQuery, IReadOnlyList<TestFileInfo>>,
    IRequestHandler<UploadTestFileCommand, TestFileInfo>,
    IRequestHandler<DeleteTestFileCommand, bool>
{
    private readonly IApplicationDbContext _db;
    private readonly ITestFileStore _store;
    private readonly IAuditLogger _audit;

    public TestFileHandlers(IApplicationDbContext db, ITestFileStore store, IAuditLogger audit)
    {
        _db = db;
        _store = store;
        _audit = audit;
    }

    public async Task<IReadOnlyList<TestFileInfo>> Handle(ListTestFilesQuery request, CancellationToken cancellationToken)
    {
        await EnsureProjectAsync(request.ProjectId, cancellationToken);
        return _store.List(request.ProjectId);
    }

    public async Task<TestFileInfo> Handle(UploadTestFileCommand request, CancellationToken cancellationToken)
    {
        await EnsureProjectAsync(request.ProjectId, cancellationToken);
        if (!_store.IsValidName(request.FileName))
        {
            throw new ValidationException([new FluentValidation.Results.ValidationFailure("FileName",
                "Use letters, digits, spaces, dots, dashes, underscores or brackets (max 120 characters), not starting with a dot.")]);
        }

        var saved = await _store.SaveAsync(request.ProjectId, request.FileName, request.Content, cancellationToken);
        await _audit.LogAsync("testfile.uploaded", "TestFiles", $"Uploaded test file '{saved.Name}' ({saved.SizeBytes} bytes)", "Project", request.ProjectId, cancellationToken);
        return saved;
    }

    public async Task<bool> Handle(DeleteTestFileCommand request, CancellationToken cancellationToken)
    {
        await EnsureProjectAsync(request.ProjectId, cancellationToken);
        var deleted = _store.Delete(request.ProjectId, request.FileName);
        if (deleted)
        {
            await _audit.LogAsync("testfile.deleted", "TestFiles", $"Deleted test file '{request.FileName}'", "Project", request.ProjectId, cancellationToken);
        }

        return deleted;
    }

    // The tenant query filter makes another tenant's project invisible, so this also blocks cross-tenant access.
    private async Task EnsureProjectAsync(Guid projectId, CancellationToken ct)
    {
        if (!await _db.Projects.AnyAsync(p => p.Id == projectId, ct))
        {
            throw new NotFoundException(nameof(Domain.Entities.Project), projectId);
        }
    }
}
