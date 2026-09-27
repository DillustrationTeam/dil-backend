namespace ArtCommission.Application.Revenue.Common;

using ArtCommission.Domain.Enums;

/// <summary>
/// Quy ước dùng chung của module doanh thu về việc dòng sổ cái nào là DOANH THU.
///
/// VÌ SAO cần một chỗ khai báo duy nhất: doanh thu được suy ra từ sổ cái ví, và
/// cùng một câu hỏi "dòng nào là tiền Creator thực nhận" được hỏi ở 5 endpoint.
/// Nếu mỗi endpoint tự liệt kê loại giao dịch, chỉ cần thêm một loại mới là số liệu
/// giữa các thẻ trên dashboard lệch nhau.
///
/// CÓ HAI LOẠI ĐƯỢC TÍNH LÀ THU NHẬP, không phải một:
///   - <see cref="WalletTransactionType.EscrowReceive"/> : cách ghi ĐÚNG — tiền VÀO
///     số dư khả dụng của người nhận. Module Auction và module Commission (đường
///     giải quyết tranh chấp) đều đã dùng loại này.
///   - <see cref="WalletTransactionType.EscrowRelease"/> : giữ lại CHỈ ĐỂ ĐỌC DỮ LIỆU CŨ.
///     Trước khi sửa, module Commission ghi chiều thu bằng loại này
///     (ResolveDisputeArbitrationCommand). Các dòng sổ cái đã sinh ra khi đó vẫn nằm
///     trong DB. Bỏ loại này khỏi danh sách sẽ làm doanh thu của những kỳ đã qua
///     biến mất khỏi báo cáo — sai lệch số liệu tệ hơn việc chấp nhận cả hai loại.
///
/// Hệ quả cần biết: một giao dịch chỉ sinh MỘT trong hai loại trên, nên cộng gộp
/// KHÔNG đếm trùng. Khi nào chắc chắn không còn dòng EscrowRelease chiều vào nào
/// (đã dọn dữ liệu cũ), có thể bỏ nó khỏi <see cref="IncomeTypes"/>.
///
/// ---------------------------------------------------------------------------
/// PHỤ THUỘC ĐÃ BIẾT — ĐỌC TRƯỚC KHI KẾT LUẬN "DOANH THU SAI"
/// ---------------------------------------------------------------------------
/// Module doanh thu này lấy SỔ CÁI VÍ làm nguồn sự thật. Nhưng module Commission
/// (`CommissionService.DepositEscrowAsync` / `ApproveMilestoneAsync`) hiện CHƯA gọi
/// ví: nạp escrow chỉ đổi `Commission.EscrowHeldAmount`, duyệt mốc chỉ cộng
/// `Commission.DisbursedAmount` — không có dòng `WalletTransaction` nào được ghi.
///
/// Vì vậy cho tới khi module Commission ghi tiền vào ví, doanh thu của một đơn đặt vẽ
/// hoàn tất bình thường sẽ KHÔNG xuất hiện ở đây (chỉ đơn có tranh chấp được phân xử
/// mới có, qua `ResolveDisputeArbitrationCommand`).
///
/// Đây là hệ quả của module khác, KHÔNG phải lỗi tính toán trong file này.
/// Sửa đúng chỗ là ở module Commission — không thêm nguồn dữ liệu thứ hai ở đây,
/// vì làm vậy sẽ đếm trùng ngay khi module kia được sửa.
/// </summary>
public static class RevenueLedgerRules
{
    /// <summary>Các loại giao dịch được coi là thu nhập của Creator.</summary>
    public static readonly WalletTransactionType[] IncomeTypes =
    [
        WalletTransactionType.EscrowReceive,
        WalletTransactionType.EscrowRelease
    ];

    /// <summary>Các loại giao dịch cần đọc từ sổ cái để tính doanh thu.</summary>
    public static readonly WalletTransactionType[] RelevantTypes =
    [
        WalletTransactionType.EscrowReceive,
        WalletTransactionType.EscrowRelease,
        WalletTransactionType.PlatformFee
    ];

    /// <summary>Kiểm tra một dòng sổ cái có phải thu nhập của Creator không.</summary>
    public static bool IsIncome(WalletTransactionType type, WalletTransactionDirection direction) =>
        direction == WalletTransactionDirection.In
        && Array.IndexOf(IncomeTypes, type) >= 0;

    /// <summary>Kiểm tra loại giao dịch có liên quan tới tính doanh thu không.</summary>
    public static bool IsRelevant(WalletTransactionType type) =>
        Array.IndexOf(RelevantTypes, type) >= 0;
}
