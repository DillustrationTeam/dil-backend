using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Commands.UpdateCreatorProfile;

public record UpdateCreatorProfileCommand(
    Guid UserId,
    string? DisplayName,
    string? Headline,
    string? Bio,
    string? Specialties,
    string? Location,
    string? WebsiteUrl,
    string? BannerUrl,
    bool? IsAcceptingOrders = null,
    int? AvailableSlots = null
) : IRequest<(bool Success, CreatorProfileDto? Data, string[] Errors)>;

public class UpdateCreatorProfileCommandValidator : AbstractValidator<UpdateCreatorProfileCommand>
{
    public UpdateCreatorProfileCommandValidator()
    {
        RuleFor(x => x.DisplayName)
            .MaximumLength(100).WithMessage("Display name must be 100 characters or fewer.")
            .When(x => !string.IsNullOrWhiteSpace(x.DisplayName));

        RuleFor(x => x.Headline)
            .MaximumLength(200).WithMessage("Headline must be 200 characters or fewer.")
            .When(x => !string.IsNullOrWhiteSpace(x.Headline));

        RuleFor(x => x.Bio)
            .MaximumLength(2000).WithMessage("Bio must be 2000 characters or fewer.")
            .When(x => !string.IsNullOrWhiteSpace(x.Bio));

        RuleFor(x => x.Specialties)
            .MaximumLength(500).WithMessage("Specialties must be 500 characters or fewer.")
            .When(x => !string.IsNullOrWhiteSpace(x.Specialties));

        RuleFor(x => x.AvailableSlots)
            .InclusiveBetween(0, 100).WithMessage("Available slots must be between 0 and 100.")
            .When(x => x.AvailableSlots.HasValue);
    }
}

public class UpdateCreatorProfileCommandHandler : IRequestHandler<UpdateCreatorProfileCommand, (bool Success, CreatorProfileDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public UpdateCreatorProfileCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, CreatorProfileDto? Data, string[] Errors)> Handle(UpdateCreatorProfileCommand request, CancellationToken cancellationToken)
    {
        var validation = new UpdateCreatorProfileCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var profile = await _db.Set<ArtCommission.Domain.Entities.ArtistStudio.CreatorProfile>()
            .FirstOrDefaultAsync(x => x.UserId == request.UserId && !x.IsDeleted, cancellationToken);

        if (profile is null)
        {
            return (false, null, new[] { "Create a creator profile before updating it." });
        }

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            profile.DisplayName = request.DisplayName.Trim();
        }

        if (request.Headline is not null)
        {
            profile.Headline = string.IsNullOrWhiteSpace(request.Headline) ? null : request.Headline.Trim();
        }

        if (request.Bio is not null)
        {
            profile.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
        }

        if (request.Specialties is not null)
        {
            profile.Specialties = string.IsNullOrWhiteSpace(request.Specialties) ? null : request.Specialties.Trim();
        }

        if (request.Location is not null)
        {
            profile.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        }

        if (request.WebsiteUrl is not null)
        {
            profile.WebsiteUrl = string.IsNullOrWhiteSpace(request.WebsiteUrl) ? null : request.WebsiteUrl.Trim();
        }

        if (request.BannerUrl is not null)
        {
            profile.BannerUrl = string.IsNullOrWhiteSpace(request.BannerUrl) ? null : request.BannerUrl.Trim();
        }

        if (request.IsAcceptingOrders.HasValue)
        {
            profile.IsAcceptingOrders = request.IsAcceptingOrders.Value;
        }

        if (request.AvailableSlots.HasValue)
        {
            profile.AvailableSlots = request.AvailableSlots.Value;
        }

        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return (true, Map(profile), Array.Empty<string>());
    }

    private static CreatorProfileDto Map(ArtCommission.Domain.Entities.ArtistStudio.CreatorProfile profile)
        => new(
            profile.Id,
            profile.UserId,
            profile.DisplayName,
            profile.Headline,
            profile.Bio,
            profile.Specialties,
            profile.Location,
            profile.WebsiteUrl,
            profile.BannerUrl,
            profile.IsAcceptingOrders,
            profile.IsApproved,
            profile.RatingAverage,
            profile.RatingCount,
            profile.FollowerCount,
            profile.CreatedAt,
            profile.AvailableSlots
        );
}
