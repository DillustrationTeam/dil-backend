using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Revenue;

/// <summary>
/// Bản ghi chốt doanh thu của một Creator theo kỳ (UC51).
///
/// VÌ SAO cần snapshot thay vì tính lại từ sổ cái mỗi lần xem dashboard:
///   - Sổ cái có thể bị điều chỉnh hồi tố (hoàn tiền, admin adjustment) ⇒ số liệu
///     quá khứ tự đổi, không đối soát được với báo cáo đã xuất.
///   - Snapshot là ảnh chụp bất biến tại một thời điểm, có <see cref="RebuiltAt"/>
///     để biết bản ghi đã bị tính lại hay chưa.
///
/// Bất biến: unique theo (CreatorId, Scope, SnapshotDate) — mỗi kỳ chỉ một dòng.
/// </summary>
public class RevenueSnapshot : BaseEntity
{
    /// <summary>Creator được chốt doanh thu.</summary>
    public Guid CreatorId { get; set; }

    public RevenueSnapshotScope Scope { get; set; } = RevenueSnapshotScope.Daily;

    /// <summary>Ngày đại diện cho kỳ (ngày bắt đầu kỳ với weekly/monthly).</summary>
    public DateOnly SnapshotDate { get; set; }

    /// <summary>Tổng tiền giao dịch trước khi trừ phí.</summary>
    public decimal GrossAmount { get; set; }

    /// <summary>Tổng phí nền tảng đã trừ.</summary>
    public decimal FeeAmount { get; set; }

    /// <summary>Thực nhận = Gross − Fee.</summary>
    public decimal NetAmount { get; set; }

    /// <summary>Số đơn/giao dịch hoàn tất trong kỳ.</summary>
    public int CompletedOrderCount { get; set; }

    /// <summary>Phần doanh thu đến từ commission (null khi bản ghi là bản tổng hợp cũ).</summary>
    public decimal? CommissionGrossAmount { get; set; }

    /// <summary>Phần doanh thu đến từ đấu giá.</summary>
    public decimal? AuctionGrossAmount { get; set; }

    /// <summary>Thời điểm job/admin tính lại bản ghi này. Null = bản gốc chưa rebuild.</summary>
    public DateTimeOffset? RebuiltAt { get; set; }

    /// <summary>Số lần bản ghi đã bị tính lại — cảnh báo khi vượt ngưỡng bất thường.</summary>
    public int RebuildCount { get; set; }
}
