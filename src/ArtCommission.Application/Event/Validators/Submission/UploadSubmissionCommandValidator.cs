using ArtCommission.Application.Event.Commands;
using FluentValidation;

namespace ArtCommission.Application.Event.Validators;

public class UploadSubmissionCommandValidator : AbstractValidator<UploadSubmissionCommand>
{
    public UploadSubmissionCommandValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty().WithMessage("Event ID must not be empty.");

        RuleFor(x => x.SubmitterId)
            .NotEmpty().WithMessage("Submitter ID must not be empty.");

        // Nếu không cung cấp ArtworkId có sẵn thì bắt buộc phải cung cấp Title và ImageUrl để tạo Artwork mới
        When(x => !x.ArtworkId.HasValue, () =>
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Artwork title is required when uploading a new artwork.")
                .MaximumLength(200).WithMessage("Artwork title must be 200 characters or fewer.");

            RuleFor(x => x.ImageUrl)
                .NotEmpty().WithMessage("Image URL is required when uploading a new artwork.");

            RuleFor(x => x.Tags)
                .Must(tags => tags is null || tags.Count <= 10)
                .WithMessage("You can assign up to 10 tags.");
        });

        // Nếu đã có ArtworkId thì Title (nếu truyền) không được vượt quá 200 ký tự
        When(x => x.ArtworkId.HasValue, () =>
        {
            RuleFor(x => x.Title)
                .MaximumLength(200).WithMessage("Submission title must be 200 characters or fewer.");
        });

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must be 2000 characters or fewer.");
    }
}
