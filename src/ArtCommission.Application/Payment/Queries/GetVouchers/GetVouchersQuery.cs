using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Application.Payment.Queries.GetPaymentOrders;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Payment.Queries.GetVouchers;

/// <summary>
/// UC50 — GET /api/v1/vouchers
/// Admin xem toàn bộ voucher; Creator chỉ xem voucher do mình tạo.
/// Input: query voucherStatus, discountType, cursor, limit.
/// </summary>
public record GetVouchersQuery(
    Guid UserId,
    bool IsAdmin,
    /// <summary>Scheduled / Active / Expired / Disabled / UsedUp.</summary>
    string? VoucherStatus = null,
    /// <summary>Percent / Fixed.</summary>
    string? DiscountType = null,
    string? Cursor = null,
    int Limit = 20
) : IRequest<(bool Success, CursorPage<VoucherDto>? Data, string[] Errors)>;

public class GetVouchersQueryHandler
    : IRequestHandler<GetVouchersQuery, (bool, CursorPage<VoucherDto>?, string[])>
{
    private const int MaxLimit = 100;
    private const int DefaultLimit = 20;

    private readonly IApplicationDbContext _db;

    public GetVouchersQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, CursorPage<VoucherDto>?, string[])> Handle(
        GetVouchersQuery request,
        CancellationToken cancellationToken)
    {
        var limit = request.Limit <= 0 ? DefaultLimit : Math.Min(request.Limit, MaxLimit);

        var query = _db.Vouchers
            .AsNoTracking()
            .Where(v => !v.IsDeleted);

        // Creator chỉ thấy voucher của mình; Admin thấy tất cả.
        if (!request.IsAdmin)
        {
            query = query.Where(v => v.CreatedByUserId == request.UserId);
        }

        if (!string.IsNullOrWhiteSpace(request.DiscountType))
        {
            if (!Enum.TryParse<DiscountType>(request.DiscountType, ignoreCase: true, out var discountType))
            {
                return (false, null,
                    [$"Loại giảm giá không hợp lệ. Hợp lệ: {string.Join(", ", DiscountTypeNames.All)}."]);
            }

            query = query.Where(v => v.DiscountType == discountType);
        }

        // Trạng thái là giá trị SUY RA (không có cột trong DB) nên phải dịch thành điều kiện.
        var today = VoucherCheckService.TodayInVietnam();

        if (!string.IsNullOrWhiteSpace(request.VoucherStatus))
        {
            var wantedStatus = request.VoucherStatus.Trim().ToLowerInvariant();

            var isValidStatus = wantedStatus is "active" or "scheduled" or "expired" or "disabled" or "usedup";
            if (!isValidStatus)
            {
                return (false, null,
                    [$"Trạng thái voucher không hợp lệ. Hợp lệ: {string.Join(", ", VoucherStatusNames.All)}."]);
            }

            query = wantedStatus switch
            {
                "active" => query.Where(v => v.IsActive
                                             && v.StartDate <= today
                                             && v.EndDate >= today
                                             && (v.UsageLimit == null || v.UsedCount < v.UsageLimit.Value)),
                "scheduled" => query.Where(v => v.IsActive && v.StartDate > today),
                "expired" => query.Where(v => v.EndDate < today),
                "disabled" => query.Where(v => !v.IsActive),
                _ => query.Where(v => v.IsActive
                                      && v.UsageLimit != null
                                      && v.UsedCount >= v.UsageLimit.Value)
            };
        }

        // Cursor: CreatedAt (ISO 8601) của dòng cuối trang trước.
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            if (!DateTimeOffset.TryParse(
                    request.Cursor,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var cursorTime))
            {
                return (false, null, ["Cursor không hợp lệ."]);
            }

            query = query.Where(v => v.CreatedAt < cursorTime);
        }

        var rows = await query
            .OrderByDescending(v => v.CreatedAt)
            .ThenByDescending(v => v.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        string? nextCursor = null;
        if (rows.Count > limit)
        {
            rows.RemoveAt(rows.Count - 1);
            nextCursor = rows[^1].CreatedAt.ToString(
                "O", System.Globalization.CultureInfo.InvariantCulture);
        }

        var items = rows
            .Select(v => VoucherResponseMapper.ToDto(v, today, request.UserId))
            .ToList();

        return (true, new CursorPage<VoucherDto>(items, nextCursor), []);
    }
}
