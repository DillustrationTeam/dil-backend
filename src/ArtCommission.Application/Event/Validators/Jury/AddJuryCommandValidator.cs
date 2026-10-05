using ArtCommission.Application.Event.Commands;
using ArtCommission.Domain.Entities.Event;
using FluentValidation;

namespace ArtCommission.Application.Event.Validators;

public class AddJuryCommandValidator : AbstractValidator<AddJuryCommand>
{
    public AddJuryCommandValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty().WithMessage("Event ID must not be empty.");

        RuleFor(x => x.CreatorId)
            .NotEmpty().WithMessage("Creator ID must not be empty.");

        RuleFor(x => x.AdminId)
            .NotEmpty().WithMessage("Admin ID must not be empty.");
    }

    public static List<string> ValidateCanAddJury(PlatformEvent platformEvent, DateTimeOffset now)
    {
        var errors = new List<string>();

        if (now >= platformEvent.JudgingStartAt)
        {
            errors.Add($"Cannot add jury members once the judging phase has started (Judging start: {platformEvent.JudgingStartAt:u}).");
        }

        return errors;
    }
}

