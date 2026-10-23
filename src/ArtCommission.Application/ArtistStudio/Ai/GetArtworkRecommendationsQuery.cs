using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace ArtCommission.Application.ArtistStudio.Ai;
public record GetArtworkRecommendationsQuery(Guid UserId, int Take = 12) : IRequest<IReadOnlyList<ArtworkRecommendation>>;
public class GetArtworkRecommendationsHandler(IApplicationDbContext db) : IRequestHandler<GetArtworkRecommendationsQuery, IReadOnlyList<ArtworkRecommendation>>
{
    public async Task<IReadOnlyList<ArtworkRecommendation>> Handle(GetArtworkRecommendationsQuery q, CancellationToken ct)
    {
        var liked = await db.ArtworkFavorites.AsNoTracking().Where(x => x.UserId == q.UserId).Select(x => x.ArtworkId).ToListAsync(ct);
        var saved = await db.CollectionArtworks.AsNoTracking().Where(x => x.Collection!.OwnerUserId == q.UserId && !x.Collection.IsDeleted).Select(x => x.ArtworkId).ToListAsync(ct);
        var followed = await db.Follows.AsNoTracking().Where(x => x.FollowerUserId == q.UserId).Select(x => x.CreatorProfileId).ToListAsync(ct);
        var seeds = liked.Concat(saved).Distinct().ToArray();
        var tagWeights = await db.Artworks.AsNoTracking().Where(x => seeds.Contains(x.Id) && !x.IsDeleted && x.ModerationStatus == "Approved" && !x.CreatorProfile!.IsDeleted)
            .SelectMany(x => x.ArtworkTags.Select(t => t.Tag!.Name)).ToListAsync(ct);
        var weights = tagWeights.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        var tagNames = weights.Keys.ToArray();
        var visible = db.Artworks.AsNoTracking().Where(x => !x.IsDeleted && x.ModerationStatus == "Approved" && !x.CreatorProfile!.IsDeleted && x.CreatorProfile.UserId != q.UserId && !seeds.Contains(x.Id));
        var relevant = visible.Where(x => followed.Contains(x.CreatorProfileId) || x.ArtworkTags.Any(t => tagNames.Contains(t.Tag!.Name)));
        var candidateIds = await relevant.OrderByDescending(x => x.CreatedAt).Take(300).Select(x => x.Id).ToListAsync(ct);
        var popularIds = await visible.OrderByDescending(x => x.LikeCount).ThenByDescending(x => x.CreatedAt).Take(100).Select(x => x.Id).ToListAsync(ct);
        var ids = candidateIds.Concat(popularIds).Distinct().ToArray();
        var candidates = await visible.Where(x => ids.Contains(x.Id)).Include(x => x.CreatorProfile).Include(x => x.ArtworkTags).ThenInclude(x => x.Tag).ToListAsync(ct);
        return candidates.Select(a => {
            var tags = a.ArtworkTags.Where(t => t.Tag != null).Select(t => t.Tag!.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var matches = tags.Where(weights.ContainsKey).ToArray();
            var follows = followed.Contains(a.CreatorProfileId);
            var score = matches.Sum(t => Math.Min(weights[t], 5) * 3d) + (follows ? 4 : 0) + Math.Min(a.LikeCount, 100) / 100d + 1 / (1 + Math.Max(0, (DateTimeOffset.UtcNow - a.CreatedAt).TotalDays));
            var dto = new ArtworkDto(a.Id, a.CreatorProfileId, a.Title, a.Description, a.ImageUrl, a.ThumbnailUrl, a.IsAiGenerated, a.AiDetectionScore, a.ModerationStatus, a.ViewCount, a.CreatedAt, tags) { CreatorName = a.CreatorProfile!.DisplayName, Style = a.Style };
            var reason = matches.Length > 0 ? $"Phù hợp với tag bạn quan tâm: {string.Join(", ", matches.Take(2))}" : follows ? "Từ Creator bạn đang theo dõi" : "Khám phá tác phẩm đã được duyệt";
            return new { Value = new ArtworkRecommendation(dto, reason), Score = score };
        }).OrderByDescending(x => x.Score).ThenBy(x => x.Value.Artwork.Id).Take(Math.Clamp(q.Take, 1, 30)).Select(x => x.Value).ToArray();
    }
}
