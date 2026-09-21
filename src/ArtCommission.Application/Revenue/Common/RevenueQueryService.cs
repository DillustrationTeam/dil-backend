using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Revenue;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Revenue.Common;

/// <summary>
/// Nghiệp vụ doanh thu Creator (UC51).
///
/// NGUỒN SỐ LIỆU — quyết định quan trọng nhất của module:
/// Doanh thu được tính từ SỔ CÁI VÍ (<c>WalletTransaction</c>), không phải từ bảng đơn hàng.
/// Lý do:
///   - Sổ cái là nguồn sự thật về TIỀN ĐÃ VÀO VÍ. Đơn hàng có thể ở trạng thái "hoàn tất"
///     nhưng tiền chưa giải ngân, hoặc đã giải ngân rồi bị điều chỉnh.
///   - Cùng một cách tính dùng được cho cả commission và đấu giá ⇒ số liệu trên
///     dashboard cộng lại đúng bằng số dư thật của Creator.
///   - Đối soát được: mọi con số đều truy ngược được về một dòng sổ cái cụ thể.
///
/// Quy ước phân loại:
///   - <c>EscrowReceive</c> = tiền Creator thực nhận (gross) theo cách ghi đúng chiều.
///   - <c>EscrowRelease</c> chiều vào = dòng thu nhập CŨ do module Commission ghi trước
///     khi được sửa; vẫn phải đọc để báo cáo các kỳ đã qua không bị mất số.
///     Xem <see cref="RevenueLedgerRules"/> để biết khi nào bỏ được.
///   - <c>PlatformFee</c> chiều ra = phí nền tảng đã trừ.
///   - <c>Payout</c> KHÔNG phải doanh thu — đó là tiền rời ví, không tính vào đây.
/// </summary>
public interface IRevenueQueryService
{
    Task<RevenueSummaryDto> GetSummaryAsync(
        Guid creatorId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RevenueTimeseriesPointDto>> GetTimeseriesAsync(
        Guid creatorId,
        string granularity,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RevenueBreakdownItemDto>> GetBreakdownAsync(
        Guid creatorId,
        string groupBy,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tính lại (hoặc tạo mới) bản chốt doanh thu cho một kỳ.
    /// Idempotent theo (CreatorId, Scope, SnapshotDate) — chốt lại là cập nhật, không thêm dòng.
    /// </summary>
    Task<RevenueSnapshot> RebuildSnapshotAsync(
        Guid creatorId,
        RevenueSnapshotScope scope,
        DateOnly snapshotDate,
        CancellationToken cancellationToken = default);
}

public class RevenueQueryService : IRevenueQueryService
{
    private readonly IApplicationDbContext _db;

    public RevenueQueryService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<RevenueSummaryDto> GetSummaryAsync(
        Guid creatorId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var walletIds = await GetWalletIdsAsync(creatorId, cancellationToken);

        if (walletIds.Count == 0)
        {
            return new RevenueSummaryDto(0m, 0m, 0m, 0, 0m);
        }

        var data = await QueryLedgerAsync(walletIds, from, to, cancellationToken);

        var gross = data
            .Where(t => RevenueLedgerRules.IsIncome(t.Type, t.Direction))
            .Sum(t => t.Amount);

        var fee = data
            .Where(t => t.Type == WalletTransactionType.PlatformFee)
            .Sum(t => t.Amount);

        var net = gross - fee;

        // Đếm số giao dịch hoàn tất theo RefId phân biệt — tránh đếm trùng khi một
        // đơn sinh nhiều dòng sổ cái (ví dụ: mốc 1, mốc 2 của cùng một commission).
        var completedOrders = data
            .Where(t => RevenueLedgerRules.IsIncome(t.Type, t.Direction)
                        && t.RefId.HasValue)
            .Select(t => t.RefId!.Value)
            .Distinct()
            .Count();

        var average = completedOrders > 0
            ? Math.Round(gross / completedOrders, 2, MidpointRounding.AwayFromZero)
            : 0m;

        return new RevenueSummaryDto(gross, fee, net, completedOrders, average);
    }

    public async Task<IReadOnlyList<RevenueTimeseriesPointDto>> GetTimeseriesAsync(
        Guid creatorId,
        string granularity,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var walletIds = await GetWalletIdsAsync(creatorId, cancellationToken);

        if (walletIds.Count == 0)
        {
            return [];
        }

        var data = await QueryLedgerAsync(walletIds, from, to, cancellationToken);

        var incomeRows = data
            .Where(t => RevenueLedgerRules.IsIncome(t.Type, t.Direction))
            .ToList();

        var feeRows = data
            .Where(t => t.Type == WalletTransactionType.PlatformFee)
            .ToList();

        var incomeBuckets = Bucket(incomeRows, granularity);
        var feeBuckets = Bucket(feeRows, granularity);

        // Dùng hợp của hai tập khoá để kỳ chỉ có phí mà không có thu nhập vẫn hiện ra
        // (nếu chỉ lấy khoá từ income, kỳ đó sẽ mất khỏi biểu đồ).
        var keys = incomeBuckets.Keys
            .Union(feeBuckets.Keys)
            .OrderBy(k => k.SortKey)
            .ToList();

        return keys
            .Select(key =>
            {
                var gross = incomeBuckets.TryGetValue(key, out var income) ? income : 0m;
                var fee = feeBuckets.TryGetValue(key, out var feeValue) ? feeValue : 0m;

                return new RevenueTimeseriesPointDto(
                    key.Label,
                    gross,
                    fee,
                    gross - fee);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<RevenueBreakdownItemDto>> GetBreakdownAsync(
        Guid creatorId,
        string groupBy,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var walletIds = await GetWalletIdsAsync(creatorId, cancellationToken);

        if (walletIds.Count == 0)
        {
            return [];
        }

        var data = await QueryLedgerAsync(walletIds, from, to, cancellationToken);

        var incomeRows = data
            .Where(t => RevenueLedgerRules.IsIncome(t.Type, t.Direction))
            .ToList();

        var feeRows = data
            .Where(t => t.Type == WalletTransactionType.PlatformFee)
            .ToList();

        var totalGross = incomeRows.Sum(t => t.Amount);
        var totalFee = feeRows.Sum(t => t.Amount);

        // ------------------------------------------------------------------
        // GIỚI HẠN CẦN BIẾT: schema hiện tại KHÔNG có chiều "dịch vụ" (service) cho
        // giao dịch — sổ cái chỉ phân biệt được Commission và Auction qua RefType.
        // Vì vậy cả ba giá trị groupBy (source / service / auction) đều nhóm theo
        // NGUỒN. Giữ nguyên ba tên để FE không phải đổi, nhưng không giả vờ có dữ
        // liệu chi tiết hơn thực tế.
        // Khi nào có bảng dịch vụ: thay nhánh này bằng join sang dịch vụ.
        // ------------------------------------------------------------------
        _ = groupBy;

        var groups = incomeRows
            .GroupBy(t => ResolveSource(t.RefType))
            .Select(g => g.Key)
            .ToList();

        var result = new List<RevenueBreakdownItemDto>();

        foreach (var groupKey in groups.OrderBy(k => k))
        {
            var groupIncome = incomeRows
                .Where(t => string.Equals(ResolveSource(t.RefType), groupKey, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var gross = groupIncome.Sum(t => t.Amount);

            // Phân bổ phí theo tỉ lệ doanh thu của nhóm: sổ cái ghi phí ở dòng riêng,
            // không gắn trực tiếp vào từng đơn, nên đây là cách giữ tổng khớp.
            var share = totalGross > 0m ? gross / totalGross : 0m;
            var fee = Math.Round(totalFee * share, 2, MidpointRounding.AwayFromZero);

            var orderCount = groupIncome
                .Where(t => t.RefId.HasValue)
                .Select(t => t.RefId!.Value)
                .Distinct()
                .Count();

            result.Add(new RevenueBreakdownItemDto(
                Key: groupKey,
                Label: LabelFor(groupKey),
                GrossAmount: gross,
                FeeAmount: fee,
                NetAmount: gross - fee,
                OrderCount: orderCount));
        }

        return result;
    }

    public async Task<RevenueSnapshot> RebuildSnapshotAsync(
        Guid creatorId,
        RevenueSnapshotScope scope,
        DateOnly snapshotDate,
        CancellationToken cancellationToken = default)
    {
        // Chuẩn hoá về ngày bắt đầu kỳ để khoá unique (CreatorId, Scope, SnapshotDate)
        // không bị phá khi client gửi giữa tuần/giữa tháng.
        var periodStart = NormalizePeriodStart(scope, snapshotDate);
        var (from, to) = PeriodRange(scope, periodStart);

        var summary = await GetSummaryAsync(creatorId, from, to, cancellationToken);

        var walletIds = await GetWalletIdsAsync(creatorId, cancellationToken);

        decimal commissionGross = 0m;
        decimal auctionGross = 0m;

        if (walletIds.Count > 0)
        {
            var data = await QueryLedgerAsync(walletIds, from, to, cancellationToken);

            var income = data
                .Where(t => RevenueLedgerRules.IsIncome(t.Type, t.Direction))
                .ToList();

            commissionGross = income
                .Where(t => !AuctionRefTypes.IsAuction(t.RefType))
                .Sum(t => t.Amount);

            auctionGross = income
                .Where(t => AuctionRefTypes.IsAuction(t.RefType))
                .Sum(t => t.Amount);
        }

        var now = DateTimeOffset.UtcNow;

        var snapshot = await _db.RevenueSnapshots
            .FirstOrDefaultAsync(
                s => s.CreatorId == creatorId
                     && s.Scope == scope
                     && s.SnapshotDate == periodStart
                     && !s.IsDeleted,
                cancellationToken);

        if (snapshot is null)
        {
            snapshot = new RevenueSnapshot
            {
                CreatorId = creatorId,
                Scope = scope,
                SnapshotDate = periodStart,
                RebuiltAt = now,
                RebuildCount = 0
            };

            _db.RevenueSnapshots.Add(snapshot);
        }
        else
        {
            // Bản ghi đã có: cập nhật số liệu và TĂNG số lần rebuild để phát hiện
            // trường hợp bị tính lại bất thường nhiều lần.
            snapshot.RebuiltAt = now;
            snapshot.RebuildCount += 1;
            snapshot.UpdatedAt = now;
        }

        snapshot.GrossAmount = summary.GrossAmount;
        snapshot.FeeAmount = summary.FeeAmount;
        snapshot.NetAmount = summary.NetAmount;
        snapshot.CompletedOrderCount = summary.CompletedOrderCount;
        snapshot.CommissionGrossAmount = commissionGross;
        snapshot.AuctionGrossAmount = auctionGross;

        await _db.SaveChangesAsync(cancellationToken);

        return snapshot;
    }

    // ------------------------------------------------------------------
    // Truy vấn nền
    // ------------------------------------------------------------------

    private async Task<List<Guid>> GetWalletIdsAsync(
        Guid creatorId,
        CancellationToken cancellationToken) =>
        await _db.Wallets
            .AsNoTracking()
            .Where(w => w.UserId == creatorId && !w.IsDeleted)
            .Select(w => w.Id)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Đọc các dòng sổ cái liên quan trong khoảng thời gian.
    ///
    /// Lọc theo <c>CreatedAt</c> — thời điểm ghi sổ, đúng với định nghĩa
    /// "doanh thu kỳ này". Dùng <c>BalanceAfter</c> để tính doanh thu là sai vì
    /// đó là số dư luỹ kế, không phải phát sinh trong kỳ.
    /// </summary>
    private async Task<List<Domain.Entities.Payment.WalletTransaction>> QueryLedgerAsync(
        List<Guid> walletIds,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken) =>
        await _db.WalletTransactions
            .AsNoTracking()
            .Where(t => walletIds.Contains(t.WalletId)
                        && !t.IsDeleted
                        && t.CreatedAt >= from
                        && t.CreatedAt <= to
                        && RevenueLedgerRules.IsRelevant(t.Type))
            .Select(t => new Domain.Entities.Payment.WalletTransaction
            {
                Id = t.Id,
                WalletId = t.WalletId,
                Type = t.Type,
                Direction = t.Direction,
                Amount = t.Amount,
                RefType = t.RefType,
                RefId = t.RefId,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(cancellationToken);

    // ------------------------------------------------------------------
    // Nhóm theo thời gian
    // ------------------------------------------------------------------

    private static Dictionary<PeriodKey, decimal> Bucket(
        List<Domain.Entities.Payment.WalletTransaction> rows,
        string granularity)
    {
        var normalized = (granularity ?? "day").Trim().ToLowerInvariant();

        var buckets = new Dictionary<PeriodKey, decimal>();

        foreach (var row in rows)
        {
            var key = BuildPeriodKey(row.CreatedAt, normalized);

            buckets[key] = buckets.TryGetValue(key, out var current)
                ? current + row.Amount
                : row.Amount;
        }

        return buckets;
    }

    private static PeriodKey BuildPeriodKey(DateTimeOffset moment, string granularity)
    {
        // Dùng UTC nhất quán: nếu trộn múi giờ thì một giao dịch có thể rơi vào
        // hai kỳ khác nhau giữa hai lần gọi và số liệu sẽ nhảy.
        var utc = moment.ToUniversalTime();

        return granularity switch
        {
            "week" => new PeriodKey(
                StartOfWeek(utc),
                $"{StartOfWeek(utc):yyyy-MM-dd} (tuần)"),

            "month" => new PeriodKey(
                new DateTimeOffset(utc.Year, utc.Month, 1, 0, 0, 0, TimeSpan.Zero),
                $"{utc.Year:D4}-{utc.Month:D2}"),

            _ => new PeriodKey(
                new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero),
                $"{utc.Year:D4}-{utc.Month:D2}-{utc.Day:D2}")
        };
    }

    /// <summary>Tuần bắt đầu từ thứ Hai (chuẩn Việt Nam).</summary>
    private static DateTimeOffset StartOfWeek(DateTimeOffset moment)
    {
        var offset = ((int)moment.DayOfWeek + 6) % 7;
        var date = moment.Date.AddDays(-offset);
        return new DateTimeOffset(date, TimeSpan.Zero);
    }

    private readonly record struct PeriodKey(DateTimeOffset SortKey, string Label);

    // ------------------------------------------------------------------
    // Nhóm theo nguồn
    // ------------------------------------------------------------------

    private static string ResolveSource(string? refType)
    {
        // Dùng AuctionRefTypes.IsAuction thay vì so sánh với một chuỗi cố định:
        // module đấu giá ghi sổ cái bằng NHIỀU RefType khác nhau
        // (AuctionBidDeposit, AuctionBuyNowPayment, AuctionBidShortfall, AuctionSettlement)
        // vì unique index UX_WalletTransaction_Ref buộc mỗi loại nghiệp vụ tiền phải
        // có RefType riêng. Chỉ so sánh với "Auction" sẽ phân loại SAI toàn bộ doanh
        // thu đấu giá thành doanh thu commission.
        if (AuctionRefTypes.IsAuction(refType))
        {
            return "auction";
        }

        // RefType "Commission" và mọi giá trị khác đều quy về nguồn commission:
        // sổ cái chỉ có hai nguồn doanh thu, và gộp nhóm lạ vào commission an toàn hơn
        // là tạo nhóm "other" mà FE không có nhãn hiển thị.
        return "commission";
    }

    private static string LabelFor(string key) => key switch
    {
        "auction" => "Đấu giá",
        "commission" => "Đơn đặt vẽ",
        "other" => "Khác",
        _ => key
    };

    // ------------------------------------------------------------------
    // Kỳ của snapshot
    // ------------------------------------------------------------------

    private static DateOnly NormalizePeriodStart(RevenueSnapshotScope scope, DateOnly date) =>
        scope switch
        {
            RevenueSnapshotScope.Weekly => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
            RevenueSnapshotScope.Monthly => new DateOnly(date.Year, date.Month, 1),
            _ => date
        };

    private static (DateTimeOffset From, DateTimeOffset To) PeriodRange(
        RevenueSnapshotScope scope,
        DateOnly periodStart)
    {
        var from = new DateTimeOffset(
            periodStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var exclusiveEnd = scope switch
        {
            RevenueSnapshotScope.Weekly => from.AddDays(7),
            RevenueSnapshotScope.Monthly => from.AddMonths(1),
            _ => from.AddDays(1)
        };

        // Trừ 1 tick để kỳ là khoảng ĐÓNG [from, to] — tránh giao dịch đúng mốc
        // nửa đêm bị tính vào cả hai kỳ.
        return (from, exclusiveEnd.AddTicks(-1));
    }
}
