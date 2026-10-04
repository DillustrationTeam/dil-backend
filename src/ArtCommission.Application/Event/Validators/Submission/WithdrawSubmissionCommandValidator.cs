using ArtCommission.Application.Event.Commands;
using FluentValidation;

namespace ArtCommission.Application.Event.Validators;

public class WithdrawSubmissionCommandValidator : AbstractValidator<WithdrawSubmissionCommand>
{
    public WithdrawSubmissionCommandValidator()
    {
        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("Submission ID must not be empty.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID must not be empty.");
    }
}
