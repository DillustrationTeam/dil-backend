using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Commands.UploadArtwork;

public record UploadArtworkCommand(
    Guid UserId,
    string Title,
    string? Description,
    string ImageUrl,
    string? ThumbnailUrl,
    bool IsAiGenerated = false,
    decimal? AiDetectionScore = null,
    IReadOnlyList<string>? Tags = null
) : IRequest<(bool Success, ArtworkDto? Data, string[] Errors)>;

public class UploadArtworkCommandValidator : AbstractValidator<UploadArtworkCommand>
{
    public UploadArtworkCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Artwork title is required.")
            .MaximumLength(200).WithMessage("Artwork title must be 200 characters or fewer.");

        RuleFor(x => x.ImageUrl)
            .NotEmpty().WithMessage("Image URL is required.");

        RuleFor(x => x.Tags)
            .Must(tags => tags is null || tags.Count <= 10).WithMessage("You can assign up to 10 tags.");
    }
}

public class UploadArtworkCommandHandler : IRequestHandler<UploadArtworkCommand, (bool Success, ArtworkDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public UploadArtworkCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, ArtworkDto? Data, string[] Errors)> Handle(UploadArtworkCommand request, CancellationToken cancellationToken)
    {
        var validation = new UploadArtworkCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var profile = await _db.Set<ArtCommission.Domain.Entities.ArtistStudio.CreatorProfile>()
            .FirstOrDefaultAsync(x => x.UserId == request.UserId && !x.IsDeleted, cancellationToken);

        if (profile is null)
        {
            return (false, null, new[] { "Create a creator profile before uploading artworks." });
        }

        var artwork = new ArtCommission.Domain.Entities.ArtistStudio.Artwork
        {
            CreatorProfileId = profile.Id,
            Title = request.Title.Trim(),
            Description = request.Description,
            ImageUrl = request.ImageUrl,
            ThumbnailUrl = request.ThumbnailUrl,
            IsAiGenerated = request.IsAiGenerated,
            AiDetectionScore = request.AiDetectionScore,
            ModerationStatus = "Pending",
            ViewCount = 0
        };

        _db.Set<ArtCommission.Domain.Entities.ArtistStudio.Artwork>().Add(artwork);
        await _db.SaveChangesAsync(cancellationToken);

        if (request.Tags is { Count: > 0 })
        {
            foreach (var tagName in request.Tags.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var tag = await _db.Set<ArtCommission.Domain.Entities.ArtistStudio.Tag>()
                    .FirstOrDefaultAsync(x => x.Name == tagName, cancellationToken);

                if (tag is null)
                {
                    tag = new ArtCommission.Domain.Entities.ArtistStudio.Tag
                    {
                        Name = tagName.Trim(),
                        IsAiGenerated = request.IsAiGenerated,
                    };
                    _db.Set<ArtCommission.Domain.Entities.ArtistStudio.Tag>().Add(tag);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                _db.Set<ArtCommission.Domain.Entities.ArtistStudio.ArtworkTag>().Add(new ArtCommission.Domain.Entities.ArtistStudio.ArtworkTag
                {
                    ArtworkId = artwork.Id,
                    TagId = tag.Id
                });
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        return (true, new ArtworkDto(
            artwork.Id,
            artwork.CreatorProfileId,
            artwork.Title,
            artwork.Description,
            artwork.ImageUrl,
            artwork.ThumbnailUrl,
            artwork.IsAiGenerated,
            artwork.AiDetectionScore,
            artwork.ModerationStatus,
            artwork.ViewCount,
            artwork.CreatedAt,
            (request.Tags ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        ), Array.Empty<string>());
    }
}
