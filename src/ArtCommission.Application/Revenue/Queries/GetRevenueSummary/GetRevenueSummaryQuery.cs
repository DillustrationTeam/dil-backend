using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Revenue.Common;
using MediatR;

namespace ArtCommission.Application.Revenue.Queries.GetRevenueSummary;

/// <summary>
/// UC51 — GET /api/v1/creator/revenue/summary
/// Thẻ số liệu tổng quan cho Creator Dashboard.
///
/// Mặc định 30 ngày gần nhất khi client không truyền khoảng thời gian — dashboard
/// luôn cần một khoảng, và 30 ngày là mặc định hợp lý nhất với người dùng.
/// </summary>
public record GetRevenueSummaryQuery(
    Guid UserId,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null
) : IRequest<(bool Success, RevenueSummaryDto? Data, object? Meta, string[] Errors)>;

public class GetRevenueSummaryQueryHandler
    : IRequestHandler<GetRevenueSummaryQuery, (bool, RevenueSummaryDto?, object?, string[])>
{
    private readonly IRevenueQueryService _revenueService;

    public GetRevenueSummaryQueryHandler(IRevenueQueryService revenueService)
    {
        _revenueService = revenueService;
    }

    public async Task<(bool, RevenueSummaryDto?, object?, string[])> Handle(
        GetRevenueSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var (from, to, error) = RevenueRangeResolver.Resolve(request.From, request.To);
        if (error is not null)
        {
            return (false, null, null, [error]);
        }

        var summary = await _revenueService.GetSummaryAsync(
            request.UserId, from, to, cancellationToken);

        return (true, summary, new { from, to }, []);
    }
}
