using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Commands.CreatePayoutRequest;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Application.Payment.Queries.GetPaymentOrders;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Payment.Queries.GetPayoutRequests;

/// <summary>
/// UC49 — GET /api/v1/payout-requests
/// Creator xem lịch sử yêu cầu rút tiền của mình.
/// Admin truyền <paramref name="UserId"/> để xem hàng chờ duyệt của một Creator,
/// hoặc để trống để xem toàn bộ hàng chờ.
/// </summary>
/// <param name="RequesterId">Người gọi API.</param>
/// <param name="IsAdmin">Người gọi có vai trò Administrator không.</param>
/// <param name="UserId">Chỉ Admin dùng — lọc theo Creator.</param>
public record GetPayoutRequestsQuery(
    Guid RequesterId,
    bool IsAdmin,
    string? Status = null,
    Guid? UserId = null,
    string? Cursor = null,
    int Limit = 20
) : IRequest<(bool Success, CursorPage<PayoutRequestDto>? Data, string[] Errors)>;

public class GetPayoutRequestsQueryHandler
    : IRequestHandler<GetPayoutRequestsQuery, (bool, CursorPage<PayoutRequestDto>?, string[])>
{
    private const int MaxLimit = 100;

    private readonly IApplicationDbContext _db;

    public GetPayoutRequestsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, CursorPage<PayoutRequestDto>?, string[])> Handle(
        GetPayoutRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var limit = request.Limit <= 0 ? 20 : Math.Min(request.Limit, MaxLimit);

        var query = _db.PayoutRequests
            .AsNoTracking()
            .Where(p => !p.IsDeleted);

        // Creator chỉ thấy đơn của mình. Admin thấy tất cả, hoặc lọc theo 1 Creator.
        if (!request.IsAdmin)
        {
            query = query.Where(p => p.UserId == request.RequesterId);
        }
        else if (request.UserId.HasValue)
        {
            query = query.Where(p => p.UserId == request.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<PayoutStatus>(request.Status, ignoreCase: true, out var status))
            {
                return (false, null,
                    [$"Trạng thái không hợp lệ. Hợp lệ: {string.Join(", ", PayoutStatusNames.All)}."]);
            }

            query = query.Where(p => p.Status == status);
        }

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

            query = query.Where(p => p.CreatedAt < cursorTime);
        }

        var rows = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Take(limit + 1)
            .Select(p => new
            {
                Payout = p,
                BankAccount = _db.BankAccounts.FirstOrDefault(b => b.Id == p.BankAccountId)
            })
            .ToListAsync(cancellationToken);

        string? nextCursor = null;
        if (rows.Count > limit)
        {
            rows.RemoveAt(rows.Count - 1);
            nextCursor = rows[^1].Payout.CreatedAt.ToString(
                "O", System.Globalization.CultureInfo.InvariantCulture);
        }

        var items = rows
            .Select(r => CreatePayoutRequestCommandHandler.MapToDto(r.Payout, r.BankAccount))
            .ToList();

        return (true, new CursorPage<PayoutRequestDto>(items, nextCursor), []);
    }
}
