using System.Data;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.CreatorApplication.Commands;

public record ReviewCreatorApplicationCommand(
    Guid ApplicationId,
    Guid ModeratorId,
    ApplicationStatus Status,
    string? ReviewNote
) : IRequest<(bool Success, string[] Errors)>;

public class ReviewCreatorApplicationCommandValidator : 
             AbstractValidator<ReviewCreatorApplicationCommand>
{
    public ReviewCreatorApplicationCommandValidator()
    {
        RuleFor(c => c.ApplicationId)
            .NotEmpty().WithMessage("Invalid Application ID.");

        RuleFor(c => c.ModeratorId)
            .NotEmpty().WithMessage("Invalid Moderator ID.");   

        RuleFor(c => c.Status)
            .Must(s => s == ApplicationStatus.Approved || s == ApplicationStatus.Rejected)
            .WithMessage("Application status must only be Approved or Rejected.");

        When (c => c.Status == ApplicationStatus.Rejected, () =>
        {
            RuleFor(c => c.ReviewNote)
                .NotEmpty().WithMessage("Please enter reason for rejection.")
                .MaximumLength(1000).WithMessage("Review note cannot exceed 1000 characters.");
        });  
    }
}

public class ReviewCreatorApplicationCommandHandler 
    : IRequestHandler<ReviewCreatorApplicationCommand, 
    (bool Success, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly IIdentityService _identityService;
    public ReviewCreatorApplicationCommandHandler(
        IApplicationDbContext db,
        IIdentityService identityService)
    {
        _db = db;
        _identityService = identityService;
    }
    public async Task<(bool Success, string[] Errors)> Handle(
        ReviewCreatorApplicationCommand request, 
        CancellationToken cancellationToken
    )
    {
        var validator = new ReviewCreatorApplicationCommandValidator();
        var validation = await validator.ValidateAsync(request, cancellationToken);
        
        if (!validation.IsValid)
        {
            return (false, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }
  
        var application = await _db.CreatorApplications
            .Include(c => c.Applicant)
            .FirstOrDefaultAsync(c => c.Id == request.ApplicationId 
            && !c.IsDeleted, cancellationToken);

        if (application == null)
        {
            return (false, new[] {"Creator application not found."});
        }

        if (application.Status != ApplicationStatus.Pending)
        {
            return (false, new[] {$"This creator application has already been processed with status {application.Status}"});
        }

        if (request.Status == ApplicationStatus.Approved)
        {
            var (roleSuccess, roleErrors) = await _identityService.AddCreatorRoleAsync(
                application.ApplicantId,
                cancellationToken
            );

            if (!roleSuccess)
            {
                return (false, roleErrors);
            }

            var hasProfile = await _db.CreatorProfiles
                .AnyAsync(c => c.UserId == application.ApplicantId 
                && !c.IsDeleted, cancellationToken);

            if (!hasProfile)
            {
                var newProfile = new ArtCommission.Domain.Entities.ArtistStudio.CreatorProfile
                {
                    UserId = application.ApplicantId,
                    DisplayName = application.Applicant?.FullName ?? "Creator",
                    IsApproved = true,
                    IsAcceptingOrders = true
                };

                _db.CreatorProfiles.Add(newProfile);
            }
        }

        application.Status = request.Status;
        application.ReviewedByModId = request.ModeratorId;
        application.ReviewedAt = DateTimeOffset.UtcNow;
        application.ReviewNote = request.ReviewNote;

        await _db.SaveChangesAsync(cancellationToken);

        return (true, Array.Empty<string>());
    }
}