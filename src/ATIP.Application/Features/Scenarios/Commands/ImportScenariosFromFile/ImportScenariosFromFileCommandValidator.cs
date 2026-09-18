using FluentValidation;

namespace ATIP.Application.Features.Scenarios.Commands.ImportScenariosFromFile;

public sealed class ImportScenariosFromFileCommandValidator : AbstractValidator<ImportScenariosFromFileCommand>
{
    public ImportScenariosFromFileCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();

        RuleFor(x => x.SuiteId)
            .NotEmpty()
            .When(x => x.SuiteMode == ImportSuiteMode.Existing)
            .WithMessage("Choose the suite to add the imported test cases to.");

        RuleFor(x => x.NewSuiteName)
            .NotEmpty()
            .MaximumLength(200)
            .When(x => x.SuiteMode == ImportSuiteMode.New)
            .WithMessage("Give the new suite a name.");

        RuleFor(x => x.FileName)
            .NotEmpty()
            .Must(name =>
            {
                var ext = Path.GetExtension(name).ToLowerInvariant();
                return ext is ".csv" or ".xlsx";
            })
            .WithMessage("Only .csv and .xlsx files are supported.");
        RuleFor(x => x.Content)
            .NotNull()
            .Must(content => content.Length > 0)
            .WithMessage("Uploaded file content is empty.");
    }
}
