using ArtCommission.Application.ArtTrade.DTOs;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtTrade.Queries;

public sealed record GetAdminArtTradesQuery(
    TradeStatus? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 10
) : IRequest<AdminArtTradeListResult>;

public sealed record AdminArtTradeListResult(
    bool Success,
    List<ArtTradeDto> Items,
    int TotalCount,
    int ActiveCount,
    int CompletedCount,
    int DisputedCount,
    int CancelledCount
);

public class GetAdminArtTradesQueryHandler : IRequestHandler<GetAdminArtTradesQuery, AdminArtTradeListResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IStorageService _storageService;

    public GetAdminArtTradesQueryHandler(IApplicationDbContext db, IStorageService storageService)
    {
        _db = db;
        _storageService = storageService;
    }

    public async Task<AdminArtTradeListResult> Handle(GetAdminArtTradesQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 10 : request.PageSize;

        var baseQuery = _db.ArtTrades
            .AsNoTracking()
            .Where(t => !t.IsDeleted);

        // Pre-calculate counters across all trades
        var activeCount = await baseQuery.CountAsync(t =>
            t.Status == TradeStatus.Proposed ||
            t.Status == TradeStatus.Accepted ||
            t.Status == TradeStatus.BothSubmitted, cancellationToken);

        var completedCount = await baseQuery.CountAsync(t => t.Status == TradeStatus.Completed, cancellationToken);
        var disputedCount = await baseQuery.CountAsync(t => t.Status == TradeStatus.Disputed, cancellationToken);
        var cancelledCount = await baseQuery.CountAsync(t => t.Status == TradeStatus.Cancelled || t.Status == TradeStatus.Rejected, cancellationToken);

        var query = baseQuery
            .Include(t => t.Initiator)
            .Include(t => t.Receiver)
            .Include(t => t.InitiatorArtwork)
            .Include(t => t.ReceiverArtwork)
            .AsQueryable();

        if (request.Status.HasValue)
        {
            query = query.Where(t => t.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(t =>
                t.Title.ToLower().Contains(search) ||
                (t.Initiator != null && t.Initiator.FullName.ToLower().Contains(search)) ||
                (t.Initiator != null && t.Initiator.Email != null && t.Initiator.Email.ToLower().Contains(search)) ||
                (t.Receiver != null && t.Receiver.FullName.ToLower().Contains(search)) ||
                (t.Receiver != null && t.Receiver.Email != null && t.Receiver.Email.ToLower().Contains(search)) ||
                t.Id.ToString().ToLower().Contains(search)
            );
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var trades = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = trades.Select(t => ArtTradeDto.FromEntity(t, _storageService)).ToList();

        return new AdminArtTradeListResult(
            Success: true,
            Items: items,
            TotalCount: totalCount,
            ActiveCount: activeCount,
            CompletedCount: completedCount,
            DisputedCount: disputedCount,
            CancelledCount: cancelledCount
        );
    }
}
