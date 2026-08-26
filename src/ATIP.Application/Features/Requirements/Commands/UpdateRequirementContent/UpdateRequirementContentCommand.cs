using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Requirements.Dtos;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Requirements.Commands.UpdateRequirementContent;

/// <summary>Updates the extracted text (content) of a business-context document.</summary>
public sealed record UpdateRequirementContentCommand(Guid Id, string Content)
    : IRequest<RequirementDetailDto>;

public sealed class UpdateRequirementContentCommandHandler
    : IRequestHandler<UpdateRequirementContentCommand, RequirementDetailDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateRequirementContentCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<RequirementDetailDto> Handle(
        UpdateRequirementContentCommand request,
        CancellationToken cancellationToken)
    {
        var requirement = await _db.Requirements
            .Include(r => r.Modules).ThenInclude(m => m.Features).ThenInclude(f => f.UserStories)
            .Include(r => r.Modules).ThenInclude(m => m.Features).ThenInclude(f => f.Scenarios)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Requirement), request.Id);

        requirement.ExtractedText = request.Content ?? string.Empty;
        requirement.Status = string.IsNullOrWhiteSpace(requirement.ExtractedText)
            ? RequirementStatus.Failed
            : RequirementStatus.Analyzed;
        requirement.ErrorMessage = string.IsNullOrWhiteSpace(requirement.ExtractedText)
            ? "Document has no content."
            : null;

        await _db.SaveChangesAsync(cancellationToken);

        return RequirementDetailDto.FromEntity(requirement);
    }
}
