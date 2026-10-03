using ArtCommission.Application.Event.Commands;
using FluentValidation;

namespace ArtCommission.Application.Event.Validators;

public class DeclineInvitationCommandValidator : AbstractValidator<DeclineInvitationCommand>
{
    public DeclineInvitationCommandValidator()
    {
        RuleFor(x => x.InvitationId)
            .NotEmpty().WithMessage("Invitation ID must not be empty.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID must not be empty.");
    }
}
