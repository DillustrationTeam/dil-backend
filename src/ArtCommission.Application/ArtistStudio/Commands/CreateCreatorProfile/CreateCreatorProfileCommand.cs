using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Commands.CreateCreatorProfile;

public record CreateCreatorProfileCommand(
    Guid UserId,
    string DisplayName,
    string? Headline,
    string? Bio,
    string? Specialties,
    string? Location,
    string? WebsiteUrl,
    string? BannerUrl,
    bool IsAcceptingOrders = true,
    int AvailableSlots = 0
) : IRequest<(bool Success, CreatorProfileDto? Data, string[] Errors)>;

public class CreateCreatorProfileCommandValidator : AbstractValidator<CreateCreatorProfileCommand>
{
    public CreateCreatorProfileCommandValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MaximumLength(100).WithMessage("Display name must be 100 characters or fewer.");

        RuleFor(x => x.Headline)
            .MaximumLength(200).WithMessage("Headline must be 200 characters or fewer.");

        RuleFor(x => x.Bio)
            .MaximumLength(2000).WithMessage("Bio must be 2000 characters or fewer.");

        RuleFor(x => x.Specialties)
            .MaximumLength(500).WithMessage("Specialties must be 500 characters or fewer.");
        RuleFor(x => x.AvailableSlots).InclusiveBetween(0, 100);
    }
}

public class CreateCreatorProfileCommandHandler : IRequestHandler<CreateCreatorProfileCommand, (bool Success, CreatorProfileDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public CreateCreatorProfileCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, CreatorProfileDto? Data, string[] Errors)> Handle(CreateCreatorProfileCommand request, CancellationToken cancellationToken)
    {
        var validation = new CreateCreatorProfileCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var exists = await _db.Set<ArtCommission.Domain.Entities.ArtistStudio.CreatorProfile>()
            .AnyAsync(x => x.UserId == request.UserId && !x.IsDeleted, cancellationToken);
        if (exists)
        {
            return (false, null, new[] { "A creator profile already exists for this user." });
        }

        var profile = new ArtCommission.Domain.Entities.ArtistStudio.CreatorProfile
        {
            UserId = request.UserId,
            DisplayName = request.DisplayName.Trim(),
            Headline = request.Headline,
            Bio = request.Bio,
            Specialties = request.Specialties,
            Location = request.Location,
            WebsiteUrl = request.WebsiteUrl,
            BannerUrl = request.BannerUrl,
            IsAcceptingOrders = request.IsAcceptingOrders,
            IsApproved = false,
            RatingAverage = 0m,
            RatingCount = 0,
            FollowerCount = 0,
            AvailableSlots = request.AvailableSlots
        };

        _db.Set<ArtCommission.Domain.Entities.ArtistStudio.CreatorProfile>().Add(profile);
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
