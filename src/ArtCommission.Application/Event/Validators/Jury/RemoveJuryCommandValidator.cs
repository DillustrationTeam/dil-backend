using ArtCommission.Application.Event.Commands;
using ArtCommission.Domain.Entities.Event;
using FluentValidation;

namespace ArtCommission.Application.Event.Validators;

public class RemoveJuryCommandValidator : AbstractValidator<RemoveJuryCommand>
{
    public RemoveJuryCommandValidator()
    {
        RuleFor(x => x.AdminId)
            .NotEmpty().WithMessage("Admin ID must not be empty.");

        RuleFor(x => x)
            .Must(x => x.JuryId.HasValue || (x.EventId.HasValue && x.CreatorId.HasValue))
            .WithMessage("Either Jury ID or both Event ID and Creator ID must be provided.");
    }

    public static List<string> ValidateCanRemoveJury(PlatformEvent platformEvent, DateTimeOffset now)
    {
        var errors = new List<string>();

        if (now >= platformEvent.JudgingStartAt)
        {
            errors.Add($"Cannot remove jury members once the judging phase has started (Judging start: {platformEvent.JudgingStartAt:u}).");
        }

        return errors;
    }
}

