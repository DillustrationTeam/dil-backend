using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.CreatorApplication.Commands;

public record UpdateCreatorApplicationCommand(
    Guid ApplicantId,
    List<string> PortfolioLinks,
    List<string>? SocialLinks,
    string IdProofUrl,
    string IdProofBackUrl,
    string? PrimaryStyle = null,
    List<string>? SpeedpaintVideoUrls = null
) : IRequest<(bool Success, string[] Errors)>;

public class UpdateCreatorApplicationCommandValidator : AbstractValidator<UpdateCreatorApplicationCommand>
{
    public UpdateCreatorApplicationCommandValidator()
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

        When(c => c.SocialLinks != null && c.SocialLinks.Count > 0, () =>
        {
            RuleForEach(c => c.SocialLinks)
                .Must(IsValidUrl).WithMessage("Social link must be a valid URL.");
        });

        When(c => c.SpeedpaintVideoUrls != null && c.SpeedpaintVideoUrls.Count > 0, () =>
        {
            RuleForEach(c => c.SpeedpaintVideoUrls)
                .Must(IsValidUrl).WithMessage("Speedpaint video link must be a valid URL.");
        });
    }

    private static bool IsValidUrl(string? url)
    {
        return !string.IsNullOrWhiteSpace(url)
            && Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }
}

public class UpdateCreatorApplicationCommandHandler
    : IRequestHandler<UpdateCreatorApplicationCommand, (bool Success, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public UpdateCreatorApplicationCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, string[] Errors)> Handle(
        UpdateCreatorApplicationCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new UpdateCreatorApplicationCommandValidator();
        var validation = await validator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
        {
            return (false, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var application = await _db.CreatorApplications
            .Where(c => c.ApplicantId == request.ApplicantId
                && !c.IsDeleted
                && (c.Status == ApplicationStatus.Pending || c.Status == ApplicationStatus.AdditionalProofRequested))
            .OrderByDescending(c => c.SubmittedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (application == null)
        {
            return (false, new[] { "No editable application found." });
        }

        application.PortfolioLinks = request.PortfolioLinks;
        application.SocialLinks = request.SocialLinks ?? new List<string>();
        application.IdProofUrl = request.IdProofUrl;
        application.IdProofBackUrl = request.IdProofBackUrl;
        application.PrimaryStyle = request.PrimaryStyle;
        application.SpeedpaintVideoUrls = request.SpeedpaintVideoUrls ?? new List<string>();
        application.Status = ApplicationStatus.Pending;
        application.ReviewNote = null;
        application.ReviewedAt = null;
        application.ReviewedByModId = null;
        application.SubmittedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return (true, Array.Empty<string>());
    }
}
