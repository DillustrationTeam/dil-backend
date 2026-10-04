using ArtCommission.Application.CreatorApplication.Commands;
using ArtCommission.Domain.Enums;
using FluentValidation;

namespace ArtCommission.Application.CreatorApplication.Validators;

public class ReviewCreatorApplicationCommandValidator : AbstractValidator<ReviewCreatorApplicationCommand>
{
    public ReviewCreatorApplicationCommandValidator()
    {
        RuleFor(c => c.ApplicationId)
            .NotEmpty().WithMessage("Invalid Application ID.");

        RuleFor(c => c.ModeratorId)
            .NotEmpty().WithMessage("Invalid Moderator ID.");   

        RuleFor(c => c.Status)
            .Must(s => s == ApplicationStatus.Approved || 
                       s == ApplicationStatus.Rejected || 
                       s == ApplicationStatus.AdditionalProofRequested)
            .WithMessage("Application status must only be Approved, Rejected, or AdditionalProofRequested.");

        When(c => c.Status == ApplicationStatus.Rejected || c.Status == ApplicationStatus.AdditionalProofRequested, () =>
        {
            RuleFor(c => c.ReviewNote)
                .NotEmpty().WithMessage("Please enter reason/note for this decision.")
                .MaximumLength(1000).WithMessage("Review note cannot exceed 1000 characters.");
        });  
    }
}
