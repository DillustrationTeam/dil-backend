using ArtCommission.Application.Event.Commands;
using FluentValidation;

namespace ArtCommission.Application.Event.Validators;

public class UpdateSubmissionCommandValidator : AbstractValidator<UpdateSubmissionCommand>
{
    public UpdateSubmissionCommandValidator()
    {
        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("Submission ID must not be empty.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID must not be empty.");

        When(x => !string.IsNullOrEmpty(x.Title), () =>
        {
            RuleFor(x => x.Title)
                .MaximumLength(200).WithMessage("Submission title must be 200 characters or fewer.");
        });

        When(x => !string.IsNullOrEmpty(x.Description), () =>
        {
            RuleFor(x => x.Description)
                .MaximumLength(2000).WithMessage("Submission description must be 2000 characters or fewer.");
        });

        RuleFor(x => x.Tags)
            .Must(tags => tags is null || tags.Count <= 10)
            .WithMessage("You can assign up to 10 tags.");
    }
}
