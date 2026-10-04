using ArtCommission.Application.Event.Commands;
using FluentValidation;

namespace ArtCommission.Application.Event.Validators;

public class DeleteVoteCommandValidator : AbstractValidator<DeleteVoteCommand>
{
    public DeleteVoteCommandValidator()
    {
        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("Submission ID must not be empty.");

        RuleFor(x => x.VoterId)
            .NotEmpty().WithMessage("Voter ID must not be empty.");
    }
}
