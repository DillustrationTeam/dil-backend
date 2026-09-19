using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Queries.GetPaymentOrders;
using ArtCommission.Application.Wallets.Common;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Wallets.Queries.GetMyWalletTransactions;

/// <summary>
/// UC47 — GET /api/v1/wallets/me/transactions
/// Người dùng xem bảng lịch sử biến động số dư có lọc, phục vụ đối soát.
///
/// Input: query walletTxType, direction (In/Out), from, to, cursor, limit
/// Output: data[], meta { nextCursor, total }
/// </summary>
public record GetMyWalletTransactionsQuery(
    Guid UserId,
    /// <summary>Lọc theo loại biến động: Deposit / EscrowHold / EscrowRelease / Refund / RefundFromHold / Payout / PlatformFee / Adjustment.</summary>
    string? WalletTxType = null,
    /// <summary>"In" hoặc "Out".</summary>
    string? Direction = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Cursor = null,
    int Limit = 20
) : IRequest<(bool Success, CursorPage<WalletTransactionDto>? Data, int Total, string[] Errors)>;

public class GetMyWalletTransactionsQueryHandler
    : IRequestHandler<GetMyWalletTransactionsQuery,
        (bool, CursorPage<WalletTransactionDto>?, int, string[])>
{
    private const int MaxLimit = 100;
    private const int DefaultLimit = 20;

    private readonly IApplicationDbContext _db;

    public GetMyWalletTransactionsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, CursorPage<WalletTransactionDto>?, int, string[])> Handle(
        GetMyWalletTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var wallet = await _db.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == request.UserId && !w.IsDeleted, cancellationToken);

        // Chưa có ví => không có giao dịch nào. Trả trang rỗng, không phải lỗi.
        if (wallet is null)
        {
            return (true, new CursorPage<WalletTransactionDto>([], null), 0, []);
        }

        var limit = request.Limit <= 0 ? DefaultLimit : Math.Min(request.Limit, MaxLimit);

        var query = _db.WalletTransactions
            .AsNoTracking()
            .Where(t => t.WalletId == wallet.Id && !t.IsDeleted);

        // --- Lọc theo loại biến động ---
        if (!string.IsNullOrWhiteSpace(request.WalletTxType))
        {
            if (!Enum.TryParse<WalletTransactionType>(request.WalletTxType, ignoreCase: true, out var txType))
            {
                return (false, null, 0,
                    [$"Loại giao dịch không hợp lệ. Hợp lệ: {string.Join(", ", WalletTransactionTypeNames.All)}."]);
            }

            query = query.Where(t => t.Type == txType);
        }

        // --- Lọc theo chiều tiền vào/ra ---
        if (!string.IsNullOrWhiteSpace(request.Direction))
        {
            if (!Enum.TryParse<WalletTransactionDirection>(request.Direction, ignoreCase: true, out var direction))
            {
                return (false, null, 0,
                    [$"Chiều giao dịch không hợp lệ. Hợp lệ: {string.Join(", ", WalletTransactionDirectionNames.All)}."]);
            }

            query = query.Where(t => t.Direction == direction);
        }

        // --- Lọc theo khoảng thời gian (bao gồm cả mốc From/To) ---
        if (request.From.HasValue && request.To.HasValue && request.From > request.To)
        {
            return (false, null, 0, ["'from' phải nhỏ hơn hoặc bằng 'to'."]);
        }

        if (request.From.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(t => t.CreatedAt <= request.To.Value);
        }

        // total đếm trên CÙNG bộ lọc, trước khi phân trang
        var total = await query.CountAsync(cancellationToken);

        // --- Cursor: CreatedAt (ISO 8601) của dòng cuối trang trước ---
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            if (!DateTimeOffset.TryParse(
                    request.Cursor,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var cursorTime))
            {
                return (false, null, 0, ["Cursor không hợp lệ."]);
            }

            query = query.Where(t => t.CreatedAt < cursorTime);
        }

        // Lấy dư 1 dòng để biết còn trang sau hay không
        var rows = await query
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .Take(limit + 1)
            .Select(t => new WalletTransactionDto(
                t.Id,
                t.Type.ToString(),
                t.Direction.ToString(),
                t.Amount,
                t.BalanceAfter,
                t.RefType,
                t.RefId,
                t.CreatedAt,
                t.Note))
            .ToListAsync(cancellationToken);

        string? nextCursor = null;
        if (rows.Count > limit)
        {
            rows.RemoveAt(rows.Count - 1);
            nextCursor = rows[^1].CreatedAt.ToString(
                "O", System.Globalization.CultureInfo.InvariantCulture);
        }

        return (true, new CursorPage<WalletTransactionDto>(rows, nextCursor), total, []);
    }
}
