using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Payment.Queries.GetPaymentOrders;

/// <summary>Kết quả có phân trang cursor.</summary>
public record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);

/// <summary>
/// UC48 — GET /api/v1/payments/orders
/// Người dùng xem lịch sử các lần nạp tiền. Cursor theo CreatedAt giảm dần.
/// </summary>
public record GetPaymentOrdersQuery(
    Guid UserId,
    string? Status = null,
    string? Gateway = null,
    string? Cursor = null,
    int Limit = 20,
    /// <summary>Lọc đúng một đơn theo mã đơn hiển thị — dùng để đối soát nhanh.</summary>
    string? OrderRef = null
) : IRequest<(bool Success, CursorPage<PaymentOrderSummaryDto>? Data, string[] Errors)>;

public class GetPaymentOrdersQueryHandler
    : IRequestHandler<GetPaymentOrdersQuery, (bool, CursorPage<PaymentOrderSummaryDto>?, string[])>
{
    private const int MaxLimit = 100;

    private readonly IApplicationDbContext _db;

    public GetPaymentOrdersQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, CursorPage<PaymentOrderSummaryDto>?, string[])> Handle(
        GetPaymentOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var limit = request.Limit <= 0 ? 20 : Math.Min(request.Limit, MaxLimit);

        var query = _db.PaymentOrders
            .AsNoTracking()
            .Where(o => o.UserId == request.UserId && !o.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.OrderRef))
        {
            var orderRef = request.OrderRef.Trim();
            query = query.Where(o => o.OrderRef == orderRef);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<PaymentOrderStatus>(request.Status, ignoreCase: true, out var status))
            {
                return (false, null,
                    [$"Trạng thái không hợp lệ. Hợp lệ: {string.Join(", ", PaymentOrderStatusNames.All)}."]);
            }

            query = query.Where(o => o.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Gateway))
        {
            if (!Enum.TryParse<PaymentGateway>(request.Gateway, ignoreCase: true, out var gateway))
            {
                return (false, null,
                    [$"Cổng thanh toán không hợp lệ. Hợp lệ: {string.Join(", ", PaymentGatewayNames.All)}."]);
            }

            query = query.Where(o => o.Gateway == gateway);
        }

        // Cursor = CreatedAt (ISO 8601, round-trip) của dòng cuối trang trước
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

            query = query.Where(o => o.CreatedAt < cursorTime);
        }

        // Lấy dư 1 dòng để biết còn trang sau hay không
        var rows = await query
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Take(limit + 1)
            .Select(o => new PaymentOrderSummaryDto(
                o.Id,
                o.OrderRef,
                o.Gateway.ToString(),
                o.Amount,
                o.Status.ToString(),
                o.CreatedAt))
            .ToListAsync(cancellationToken);

        string? nextCursor = null;
        if (rows.Count > limit)
        {
            rows.RemoveAt(rows.Count - 1);
            nextCursor = rows[^1].CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        }

        return (true, new CursorPage<PaymentOrderSummaryDto>(rows, nextCursor), []);
    }
}
