using ArtCommission.Application.Event.Commands;
using FluentValidation;

namespace ArtCommission.Application.Event.Validators;

public class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(x => x.InvitationId)
            .NotEmpty().WithMessage("Invitation ID must not be empty.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID must not be empty.");
    }
}
