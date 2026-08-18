using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Requirements.Dtos;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Requirements.Commands.UploadRequirement;

public sealed class UploadRequirementCommandHandler : IRequestHandler<UploadRequirementCommand, RequirementDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IFileStorage _fileStorage;
    private readonly IDocumentTextExtractor _extractor;

    public UploadRequirementCommandHandler(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        IFileStorage fileStorage,
        IDocumentTextExtractor extractor)
    {
        _db = db;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
        _extractor = extractor;
    }

    public async Task<RequirementDto> Handle(UploadRequirementCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new ForbiddenAccessException("No tenant context is available for the current user.");

        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Project), request.ProjectId);

        var sourceType = RequirementSourceTypeResolver.Resolve(request.FileName, request.ContentType);

        var storagePath = await _fileStorage.SaveAsync(
            $"requirements/{project.Id}",
            request.FileName,
            new MemoryStream(request.Content),
            cancellationToken);

        string extractedText;
        try
        {
            using var stream = new MemoryStream(request.Content);
            extractedText = await _extractor.ExtractAsync(stream, sourceType, cancellationToken);
        }
        catch (Exception ex)
        {
            extractedText = string.Empty;
            // Text extraction failure is non-fatal: the file is still stored and can be re-processed.
            _ = ex;
        }

        var requirement = new Requirement
        {
            TenantId = tenantId,
            ProjectId = project.Id,
            Name = request.Name.Trim(),
            SourceType = sourceType,
            Status = string.IsNullOrWhiteSpace(extractedText)
                ? RequirementStatus.Failed
                : RequirementStatus.Uploaded,
            StoragePath = storagePath,
            ExtractedText = extractedText,
            ErrorMessage = string.IsNullOrWhiteSpace(extractedText)
                ? "No text could be extracted from the document."
                : null,
        };

        _db.Requirements.Add(requirement);
        await _db.SaveChangesAsync(cancellationToken);

        return RequirementDto.FromEntity(requirement);
    }
}
