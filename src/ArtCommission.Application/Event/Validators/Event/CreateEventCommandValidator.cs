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

        RuleFor(x => x.MaxVote)
            .GreaterThan(0).WithMessage("Max vote must be greater than 0.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid event status.");

        RuleFor(x => x.SubmissionStartAt)
            .NotEmpty().WithMessage("Submission start date is required.");

        RuleFor(x => x.SubmissionEndAt)
            .NotEmpty().WithMessage("Submission end date is required.")
            .GreaterThan(x => x.SubmissionStartAt).WithMessage("Submission end date must be after submission start date.");

        RuleFor(x => x.JudgingStartAt)
            .NotEmpty().WithMessage("Judging start date is required.")
            .GreaterThan(x => x.SubmissionEndAt).WithMessage("Judging start date must be after submission end date.");

        RuleFor(x => x.JudgingEndAt)
            .NotEmpty().WithMessage("Judging end date is required.")
            .GreaterThan(x => x.JudgingStartAt).WithMessage("Judging end date must be after judging start date.");

        RuleFor(x => x.VotingStartAt)
            .NotEmpty().WithMessage("Voting start date is required.")
            .GreaterThan(x => x.JudgingEndAt).WithMessage("Voting start date must be after judging end date.");

        RuleFor(x => x.VotingEndAt)
            .NotEmpty().WithMessage("Voting end date is required.")
            .GreaterThan(x => x.VotingStartAt).WithMessage("Voting end date must be after voting start date.");

        RuleFor(x => x.ResultAnnouncementAt)
            .NotEmpty().WithMessage("Result announcement date is required.")
            .GreaterThan(x => x.VotingEndAt).WithMessage("Result announcement date must be after voting end date.");
    }
}
