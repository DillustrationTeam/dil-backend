using ArtCommission.Application.Event.Commands;
using ArtCommission.Domain.Entities.Event;
using ArtCommission.Domain.Enums;
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

    public static List<string> ValidateCanDelete(PlatformEvent platformEvent, DateTimeOffset now)
    {
        var errors = new List<string>();

        if (platformEvent.Status != EventStatus.Draft)
        {
            errors.Add("Only events in Draft status can be deleted.");
        }

        if (platformEvent.SubmissionStartAt <= now)
        {
            errors.Add("Cannot delete an event whose submission start time has already arrived or passed.");
        }

        return errors;
    }
}

