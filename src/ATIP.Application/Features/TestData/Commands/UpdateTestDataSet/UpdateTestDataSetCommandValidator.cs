using FluentValidation;

namespace ATIP.Application.Features.TestData.Commands.UpdateTestDataSet;

public sealed class UpdateTestDataSetCommandValidator : AbstractValidator<UpdateTestDataSetCommand>
{
    public UpdateTestDataSetCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.EnvironmentId).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.Description)
            .MaximumLength(1000);

        RuleFor(x => x.Columns)
            .NotEmpty()
            .WithMessage("At least one column is required.");

        RuleForEach(x => x.Columns)
            .NotEmpty()
            .WithMessage("Column names cannot be empty.")
            .MaximumLength(100);
    }
}
