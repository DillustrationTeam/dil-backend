using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Revenue.Common;
using FluentValidation;
using MediatR;

namespace ArtCommission.Application.Revenue.Queries.GetRevenueBreakdown;

/// <summary>
/// UC51 — GET /api/v1/creator/revenue/breakdown
/// Phân rã doanh thu theo nguồn (đơn đặt vẽ / đấu giá) và theo dịch vụ.
/// </summary>
public record GetRevenueBreakdownQuery(
    Guid UserId,
    string GroupBy = "source",
    DateTimeOffset? From = null,
    DateTimeOffset? To = null
) : IRequest<(bool Success, IReadOnlyList<RevenueBreakdownItemDto>? Data, object? Meta, string[] Errors)>;

public class GetRevenueBreakdownQueryValidator : AbstractValidator<GetRevenueBreakdownQuery>
{
    /// <summary>Các cách nhóm được hỗ trợ.</summary>
    public static readonly string[] SupportedGroupBy = ["source", "service", "auction"];

    public GetRevenueBreakdownQueryValidator()
    {
        RuleFor(x => x.GroupBy)
            .Must(g => SupportedGroupBy.Contains((g ?? string.Empty).Trim().ToLowerInvariant()))
            .WithMessage($"Kiểu nhóm không hợp lệ. Chỉ nhận: {string.Join(", ", SupportedGroupBy)}.");
    }
}

public class GetRevenueBreakdownQueryHandler
    : IRequestHandler<GetRevenueBreakdownQuery, (bool, IReadOnlyList<RevenueBreakdownItemDto>?, object?, string[])>
{
    private readonly IRevenueQueryService _revenueService;

    public GetRevenueBreakdownQueryHandler(IRevenueQueryService revenueService)
    {
        _revenueService = revenueService;
    }

    public async Task<(bool, IReadOnlyList<RevenueBreakdownItemDto>?, object?, string[])>
        Handle(GetRevenueBreakdownQuery request, CancellationToken cancellationToken)
    {
        var validation = new GetRevenueBreakdownQueryValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var (from, to, error) = RevenueRangeResolver.Resolve(request.From, request.To);
        if (error is not null)
        {
            return (false, null, null, [error]);
        }

        var groupBy = request.GroupBy.Trim().ToLowerInvariant();

        var items = await _revenueService.GetBreakdownAsync(
            request.UserId, groupBy, from, to, cancellationToken);

        return (true, items, new { from, to, groupBy, count = items.Count }, []);
    }
}
