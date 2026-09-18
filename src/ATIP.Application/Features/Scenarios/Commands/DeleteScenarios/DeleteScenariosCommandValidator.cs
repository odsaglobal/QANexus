using FluentValidation;

namespace ATIP.Application.Features.Scenarios.Commands.DeleteScenarios;

public sealed class DeleteScenariosCommandValidator : AbstractValidator<DeleteScenariosCommand>
{
    /// <summary>Ceiling on one request so a malformed client cannot ask for an unbounded delete.</summary>
    public const int MaxIds = 500;

    public DeleteScenariosCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Ids).NotEmpty();
        RuleFor(x => x.Ids).Must(ids => ids.Count <= MaxIds)
            .WithMessage($"Cannot delete more than {MaxIds} test cases in one request.")
            .When(x => x.Ids is not null);
        RuleForEach(x => x.Ids).NotEmpty();
    }
}
