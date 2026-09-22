using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Revenue.Common;
using FluentValidation;
using MediatR;

namespace ArtCommission.Application.Revenue.Queries.GetRevenueTimeseries;

/// <summary>
/// UC51 — GET /api/v1/creator/revenue/timeseries
/// Chuỗi doanh thu theo ngày / tuần / tháng để vẽ biểu đồ.
/// </summary>
public record GetRevenueTimeseriesQuery(
    Guid UserId,
    string Granularity = "day",
    DateTimeOffset? From = null,
    DateTimeOffset? To = null
) : IRequest<(bool Success, IReadOnlyList<RevenueTimeseriesPointDto>? Data, object? Meta, string[] Errors)>;

public class GetRevenueTimeseriesQueryValidator : AbstractValidator<GetRevenueTimeseriesQuery>
{
    /// <summary>Các mức gộp thời gian được hỗ trợ.</summary>
    public static readonly string[] SupportedGranularities = ["day", "week", "month"];

    public GetRevenueTimeseriesQueryValidator()
    {
        RuleFor(x => x.Granularity)
            .Must(g => SupportedGranularities.Contains((g ?? string.Empty).Trim().ToLowerInvariant()))
            .WithMessage($"Mức gộp thời gian không hợp lệ. Chỉ nhận: {string.Join(", ", SupportedGranularities)}.");
    }
}

public class GetRevenueTimeseriesQueryHandler
    : IRequestHandler<GetRevenueTimeseriesQuery, (bool, IReadOnlyList<RevenueTimeseriesPointDto>?, object?, string[])>
{
    private readonly IRevenueQueryService _revenueService;

    public GetRevenueTimeseriesQueryHandler(IRevenueQueryService revenueService)
    {
        _revenueService = revenueService;
    }

    public async Task<(bool, IReadOnlyList<RevenueTimeseriesPointDto>?, object?, string[])> Handle(
        GetRevenueTimeseriesQuery request,
        CancellationToken cancellationToken)
    {
        var validation = new GetRevenueTimeseriesQueryValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var (from, to, error) = RevenueRangeResolver.Resolve(request.From, request.To);
        if (error is not null)
        {
            return (false, null, null, [error]);
        }

        var granularity = request.Granularity.Trim().ToLowerInvariant();

        var points = await _revenueService.GetTimeseriesAsync(
            request.UserId, granularity, from, to, cancellationToken);

        return (true, points, new { from, to, granularity, count = points.Count }, []);
    }
}
