using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Queries.GetCreatorStatistics;

public sealed record GetCreatorStatisticsQuery(Guid CreatorId)
    : IRequest<(bool Success, CreatorStatisticsDto? Data, string[] Errors)>;

public sealed record CreatorStatisticsDto(
    decimal CommissionCompletionRate,
    int SuccessfulAuctionCount,
    decimal AverageRating,
    int ReviewCount);

public sealed class GetCreatorStatisticsQueryHandler
    : IRequestHandler<GetCreatorStatisticsQuery, (bool, CreatorStatisticsDto?, string[])>
{
    private readonly IApplicationDbContext _db;

    public GetCreatorStatisticsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<(bool, CreatorStatisticsDto?, string[])> Handle(
        GetCreatorStatisticsQuery request, CancellationToken cancellationToken)
    {
        if (request.CreatorId == Guid.Empty)
        {
            return (false, null, ["Thiếu mã creator profile."]);
        }

        var creator = await _db.CreatorProfiles.AsNoTracking()
            .Where(x => x.Id == request.CreatorId && !x.IsDeleted)
            .Select(x => new { x.UserId, x.RatingAverage, x.RatingCount })
            .FirstOrDefaultAsync(cancellationToken);
        if (creator is null)
        {
            return (false, null, ["Không tìm thấy creator profile."]);
        }

        var completed = await _db.Commissions.CountAsync(
            x => x.CreatorId == request.CreatorId && !x.IsDeleted && x.Status == CommissionStatus.Completed,
            cancellationToken);
        var cancelled = await _db.Commissions.CountAsync(
            x => x.CreatorId == request.CreatorId && !x.IsDeleted && x.Status == CommissionStatus.Cancelled,
            cancellationToken);
        var denominator = completed + cancelled;
        var rate = denominator == 0 ? 0m : (decimal)completed / denominator;
        var successfulAuctions = await _db.Auctions.CountAsync(
            x => x.SellerId == creator.UserId && !x.IsDeleted
                && x.Status == AuctionStatus.Settled && x.WinnerId.HasValue,
            cancellationToken);

        return (true, new CreatorStatisticsDto(
            rate,
            successfulAuctions,
            creator.RatingAverage,
            creator.RatingCount), []);
    }
}
