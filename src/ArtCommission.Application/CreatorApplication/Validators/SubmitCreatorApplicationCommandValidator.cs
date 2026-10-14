using ArtCommission.Application.CreatorApplication.Commands;
using FluentValidation;

namespace ArtCommission.Application.CreatorApplication.Validators;

public class SubmitCreatorApplicationCommandValidator : AbstractValidator<SubmitCreatorApplicationCommand>
{
    public SubmitCreatorApplicationCommandValidator()
    {
        RuleFor(c => c.ApplicantId)
            .NotEmpty().WithMessage("Invalid Applicant ID.");

        RuleFor(c => c.PortfolioLinks)
            .NotEmpty().WithMessage("Please provide at least one portfolio link.");

        RuleForEach(c => c.PortfolioLinks)
            .NotEmpty().WithMessage("Portfolio cannot be blank.")
            .Must(IsValidUrl).WithMessage("Social links must be a valid URL.");

        RuleFor(c => c.IdProofUrl)
            .NotEmpty().WithMessage("Front-side identification card image is required.")
            .Must(IsValidUrl).WithMessage("Front-side identification card image must be a valid URL.");

        RuleFor(c => c.IdProofBackUrl)
            .NotEmpty().WithMessage("Back-side identification card image is required.")
            .Must(IsValidUrl).WithMessage("Back-side identification card image must be a valid URL.");

        RuleFor(c => c.PrimaryStyle)
            .NotEmpty().WithMessage("Primary art style is required.")
            .MaximumLength(100).WithMessage("Primary art style must not exceed 100 characters.");

        RuleFor(c => c.SpeedpaintVideoUrls)
            .NotNull().WithMessage("Please provide at least one speedpaint video link.")
            .Must(urls => urls != null && urls.Any(u => !string.IsNullOrWhiteSpace(u)))
            .WithMessage("Please provide at least one speedpaint video link.");

        RuleForEach(c => c.SpeedpaintVideoUrls)
            .NotEmpty().WithMessage("Speedpaint video link cannot be blank.")
            .Must(IsValidUrl).WithMessage("Speedpaint video link must be a valid URL.");

        When(c => c.SocialLinks != null && c.SocialLinks.Count > 0, () =>
        {
            RuleForEach(c => c.SocialLinks)
                .Must(IsValidUrl).WithMessage("Social link must be a valid URL.");
        });
    }

    private static bool IsValidUrl(string? url)
    {
        return !string.IsNullOrWhiteSpace(url)
            && Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }
}
