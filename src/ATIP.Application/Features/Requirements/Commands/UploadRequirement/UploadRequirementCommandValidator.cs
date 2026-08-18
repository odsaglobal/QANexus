using FluentValidation;

namespace ATIP.Application.Features.Requirements.Commands.UploadRequirement;

public sealed class UploadRequirementCommandValidator : AbstractValidator<UploadRequirementCommand>
{
    private const int MaxBytes = 20 * 1024 * 1024; // 20 MB

    public UploadRequirementCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(260);
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("The uploaded file is empty.")
            .Must(c => c.Length <= MaxBytes).WithMessage("The file exceeds the 20 MB limit.");
    }
}
