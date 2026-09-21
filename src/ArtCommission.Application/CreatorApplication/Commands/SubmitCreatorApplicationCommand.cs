using System.Data;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.CreatorApplication.Commands;

public record SubmitCreatorApplicationCommand(
    Guid ApplicantId,
    List<string> PortfolioLinks,
    List<string>? SocialLinks,
    string IdProofUrl,
    string IdProofBackUrl,
    string? PrimaryStyle = null,
    string? SpeedpaintVideoUrl = null
) : IRequest<(bool Success, Guid? ApplicationId, string[] Errors)>;

public class SubmitCreatorApplicationCommandValidator : 
             AbstractValidator<SubmitCreatorApplicationCommand>
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

        When (c => c.SocialLinks != null && c.SocialLinks.Count > 0, () =>
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

public class SubmitCreatorApplicationCommandHandler 
    : IRequestHandler<SubmitCreatorApplicationCommand, 
    (bool Success, Guid? ApplicationId, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    public SubmitCreatorApplicationCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, Guid? ApplicationId, string[] Errors)> Handle(
        SubmitCreatorApplicationCommand request, 
        CancellationToken cancellationToken
    )
    {
        var validator = new SubmitCreatorApplicationCommandValidator();
        var validation = await validator.ValidateAsync(request, cancellationToken);
        
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var alreadyApproved = await _db.CreatorApplications
            .AnyAsync(c => c.ApplicantId == request.ApplicantId
            && c.Status == ApplicationStatus.Approved
            && !c.IsDeleted, cancellationToken);

        if (alreadyApproved)
        {
            return (false, null, new[] {"Your account has already been approved as a Creator."});
        }

        var hasPending = await _db.CreatorApplications
            .AnyAsync(c => c.ApplicantId == request.ApplicantId
            && c.Status == ApplicationStatus.Pending
            && !c.IsDeleted, cancellationToken);

        if (hasPending)
        {
            return (false, null, new[] {"Your already have a pending application awaiting review."});
        }

        var application = new ArtCommission.Domain.Entities.CreatorApplication.CreatorApplication
        {
            ApplicantId = request.ApplicantId,
            PortfolioLinks = request.PortfolioLinks,
            SocialLinks = request.SocialLinks ?? new List<string>(),
            IdProofUrl = request.IdProofUrl,
            IdProofBackUrl = request.IdProofBackUrl,
            PrimaryStyle = request.PrimaryStyle,
            SpeedpaintVideoUrl = request.SpeedpaintVideoUrl,
            Status = ApplicationStatus.Pending,
            SubmittedAt = DateTimeOffset.UtcNow
        };

        _db.CreatorApplications.Add(application);
        await _db.SaveChangesAsync(cancellationToken);

        return (true, application.Id, Array.Empty<string>());
    }
}