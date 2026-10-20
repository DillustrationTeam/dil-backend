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
            .NotEmpty().WithMessage("Identification card image is required.")
            .Must(IsValidUrl).WithMessage("Identification card image must be a valid URL.");

        RuleFor(c => c.IdProofBackUrl)
            .NotEmpty().WithMessage("The back-side identification image is required.")
            .Must(IsValidUrl).WithMessage("The back-side identification image must be a valid URL.");

        RuleFor(c => c.SpeedpaintVideoUrls)
            .NotEmpty().WithMessage("Please provide at least one speedpaint video URL.");

        RuleForEach(c => c.SpeedpaintVideoUrls)
            .Must(IsValidUrl).WithMessage("Speedpaint links must be a valid URL.")
            .When(c => c.SpeedpaintVideoUrls is not null);

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
