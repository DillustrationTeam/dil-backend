using ArtCommission.Application.Event.Commands;
using FluentValidation;

namespace ArtCommission.Application.Event.Validators;

public class DeleteEventCommandValidator : AbstractValidator<DeleteEventCommand>
{
    public DeleteEventCommandValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty().WithMessage("Event ID must not be empty.");

        RuleFor(x => x.AdminId)
            .NotEmpty().WithMessage("Admin ID must not be empty.");
    }
}
