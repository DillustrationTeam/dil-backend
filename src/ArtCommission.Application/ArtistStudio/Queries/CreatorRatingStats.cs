using ArtCommission.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Queries;

public static class CreatorRatingStats
{
    public static async Task<Dictionary<Guid, (decimal Average, int Count)>> LoadAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var direct = await db.CreatorReviews.AsNoTracking().Where(r => !r.IsDeleted)
            .Select(r => new { CreatorId = r.CreatorProfileId, r.Rating }).ToListAsync(ct);
        var commissions = await db.Reviews.AsNoTracking().Where(r => !r.IsDeleted && r.IsVisible && !r.Commission.IsDeleted)
            .Select(r => new { CreatorId = r.Commission.CreatorId, r.Rating }).ToListAsync(ct);
        return direct.Concat(commissions).GroupBy(r => r.CreatorId)
            .ToDictionary(g => g.Key, g => (g.Average(r => (decimal)r.Rating), g.Count()));
    }
}
