using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Security;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Explorer.Commands.AnswerExplorationQuestion;

/// <summary>
/// Answers the question a paused exploration is waiting on, releasing it to continue on the same
/// browser page it paused at.
/// </summary>
public sealed record AnswerExplorationQuestionCommand(Guid ProjectId, Guid Id, string Answer) : IRequest, IProjectScopedRequest
{
    public ProjectRole RequiredRole => ProjectRole.Editor;
}

public sealed class AnswerExplorationQuestionCommandHandler : IRequestHandler<AnswerExplorationQuestionCommand>
{
    private readonly IApplicationDbContext _db;

    public AnswerExplorationQuestionCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(AnswerExplorationQuestionCommand request, CancellationToken cancellationToken)
    {
        var session = await _db.ExplorationSessions
            .FirstOrDefaultAsync(s => s.Id == request.Id && s.ProjectId == request.ProjectId, cancellationToken)
            ?? throw new NotFoundException(nameof(ExplorationSession), request.Id);

        if (session.Status != ExplorationStatus.AwaitingInput || string.IsNullOrWhiteSpace(session.PendingQuestion))
        {
            throw new ValidationException(
            [
                new FluentValidation.Results.ValidationFailure("Id", "This session is not waiting on a question."),
            ]);
        }

        if (string.IsNullOrWhiteSpace(request.Answer))
        {
            throw new ValidationException(
            [
                new FluentValidation.Results.ValidationFailure("Answer", "An answer is required."),
            ]);
        }

        // The agent's polling loop picks this up and resumes; it also flips the status back to Running
        // once it wakes, but setting it here too means the UI reflects "resuming" immediately.
        session.PendingQuestionAnswer = request.Answer.Trim();
        session.Status = ExplorationStatus.Running;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
