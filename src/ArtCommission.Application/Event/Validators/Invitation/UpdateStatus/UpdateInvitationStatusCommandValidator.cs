using ArtCommission.Application.Event.Commands;
using ArtCommission.Domain.Enums;
using FluentValidation;

namespace ArtCommission.Application.Event.Validators;

public class UpdateInvitationStatusCommandValidator : AbstractValidator<UpdateInvitationStatusCommand>
{
    public UpdateInvitationStatusCommandValidator()
    {
        RuleFor(x => x.InvitationId)
            .NotEmpty().WithMessage("Invitation ID must not be empty.");

        RuleFor(x => x.CurrentUserId)
            .NotEmpty().WithMessage("Current user ID must not be empty.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid invitation status.")
            .Must(s => s == InvitationStatus.Accepted || s == InvitationStatus.Declined || s == InvitationStatus.Canceled)
            .WithMessage("Status update must only be Accepted, Declined, or Canceled.");
    }
}
