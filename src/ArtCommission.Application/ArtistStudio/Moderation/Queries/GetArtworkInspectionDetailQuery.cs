using ArtCommission.Application.ArtistStudio.Moderation.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Moderation.Queries;

/// <summary>
/// Query lấy chi tiết thanh tra tác phẩm phục vụ kiểm duyệt nội dung (SCR-20).
/// </summary>
public record GetArtworkInspectionDetailQuery(Guid ArtworkId) : IRequest<ArtworkInspectionDetailDto?>;

public class GetArtworkInspectionDetailQueryHandler : IRequestHandler<GetArtworkInspectionDetailQuery, ArtworkInspectionDetailDto?>
{
    private readonly IApplicationDbContext _db;

    public GetArtworkInspectionDetailQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ArtworkInspectionDetailDto?> Handle(
        GetArtworkInspectionDetailQuery request,
        CancellationToken cancellationToken)
    {
        var artwork = await _db.Artworks
            .AsNoTracking()
            .Include(x => x.CreatorProfile)
                .ThenInclude(cp => cp!.User)
            .Include(x => x.ArtworkTags)
                .ThenInclude(at => at.Tag)
            .FirstOrDefaultAsync(x => x.Id == request.ArtworkId && !x.IsDeleted, cancellationToken);

        if (artwork == null)
        {
            return null;
        }

        string? moderatorName = null;
        if (artwork.ModeratorId.HasValue)
        {
            var moderator = await _db.Set<ApplicationUser>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == artwork.ModeratorId.Value, cancellationToken);

            moderatorName = moderator?.FullName ?? moderator?.UserName;
        }

        var tags = artwork.ArtworkTags
            .Where(at => at.Tag != null)
            .Select(at => new ArtworkTagItemDto(at.Tag!.Id, at.Tag.Name, at.Tag.IsAiGenerated))
            .ToList();

        return new ArtworkInspectionDetailDto
        {
            ArtworkId = artwork.Id,
            Title = artwork.Title,
            Description = artwork.Description,
            ImageUrl = artwork.ImageUrl,
            ThumbnailUrl = artwork.ThumbnailUrl,
            WatermarkedUrl = artwork.WatermarkedUrl,
            Style = artwork.Style,
            LikeCount = artwork.LikeCount,
            Resolution = artwork.Resolution,
            FileSizeBytes = artwork.FileSizeBytes,
            CreatedAt = artwork.CreatedAt,
            ViewCount = artwork.ViewCount,

            SafeScore = artwork.SafeScore,
            AdultScore = artwork.AdultScore,
            ViolenceScore = artwork.ViolenceScore,
            IsAiGenerated = artwork.IsAiGenerated,
            AiDetectionScore = artwork.AiDetectionScore,

            FlagReason = artwork.FlagReason,
            ModerationStatus = artwork.ModerationStatus,
            ModeratorId = artwork.ModeratorId,
            ModeratorName = moderatorName,
            ModerationNote = artwork.ModerationNote,
            ModeratedAt = artwork.ModeratedAt,

            CreatorProfileId = artwork.CreatorProfileId,
            CreatorName = artwork.CreatorProfile != null ? artwork.CreatorProfile.DisplayName : "Unknown",
            CreatorUsername = artwork.CreatorProfile != null && artwork.CreatorProfile.User != null 
                ? artwork.CreatorProfile.User.UserName ?? string.Empty 
                : string.Empty,
            CreatorAvatarUrl = artwork.CreatorProfile != null ? artwork.CreatorProfile.BannerUrl : null,

            Tags = tags
        };
    }
}
