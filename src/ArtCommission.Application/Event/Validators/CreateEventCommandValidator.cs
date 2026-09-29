using ArtCommission.Application.Event.Commands;
using FluentValidation;

namespace ArtCommission.Application.Event.Validators;

public class CreateEventCommandValidator : AbstractValidator<CreateEventCommand>
{
    public CreateEventCommandValidator()
    {
        RuleFor(x => x.AdminId)
            .NotEmpty().WithMessage("Admin ID must not be empty.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Event title is required.")
            .MaximumLength(200).WithMessage("Event title cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Event description is required.");

        RuleFor(x => x.BannerUrl)
            .MaximumLength(500).WithMessage("Banner URL cannot exceed 500 characters.")
            .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.BannerUrl))
            .WithMessage("Banner URL must be a valid URL.");

        RuleFor(x => x.Prize)
            .MaximumLength(1000).WithMessage("Prize description cannot exceed 1000 characters.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid event status.");

        RuleFor(x => x.StartAt)
            .NotEmpty().WithMessage("Event start date is required.");

        RuleFor(x => x.EndsAt)
            .NotEmpty().WithMessage("Event end date is required.")
            .GreaterThan(x => x.StartAt).WithMessage("Event end date must be after start date.");
    }
}
