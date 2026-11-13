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
/// ĐÃ ĐỒNG BỘ VỚI CODE HIỆN TẠI (cập nhật khi module Commission bắt đầu ghi ví):
/// Module Commission nay ghi sổ cái đầy đủ: nạp ký quỹ = <c>EscrowHold</c>, duyệt milestone =
/// <c>EscrowRelease</c> (phía client) + <c>EscrowReceive</c> (phía creator). Vì vậy doanh thu
/// của đơn đặt vẽ hoàn tất bình thường ĐÃ xuất hiện trong báo cáo — ghi chú cũ nói ngược lại
/// đã bị xoá để người sửa sau không thêm nguồn dữ liệu thứ hai và đếm trùng.
///
/// <c>CommissionEarning</c> là loại CŨ, không còn code nào ghi mới, nhưng dữ liệu lịch sử
/// vẫn nằm trong ví người bán. Vẫn tính là thu nhập để báo cáo khớp số dư thật của ví.
/// </summary>
public static class RevenueLedgerRules
{
    /// <summary>Các loại giao dịch được coi là thu nhập của Creator.</summary>
    public static readonly WalletTransactionType[] IncomeTypes =
    [
        WalletTransactionType.EscrowReceive,
        WalletTransactionType.EscrowRelease,
        WalletTransactionType.CommissionEarning
    ];

    /// <summary>Các loại giao dịch cần đọc từ sổ cái để tính doanh thu.</summary>
    public static readonly WalletTransactionType[] RelevantTypes =
    [
        WalletTransactionType.EscrowReceive,
        WalletTransactionType.EscrowRelease,
        WalletTransactionType.CommissionEarning,
        WalletTransactionType.PlatformFee,
        // Phụ phí do vi phạm giao hàng: vẫn là tiền bị trừ khỏi doanh thu, phải cộng vào
        // "phí" thì net mới khớp số tiền người bán thực nhận.
        WalletTransactionType.PenaltySurcharge
    ];

    /// <summary>Các loại giao dịch là PHÍ (trừ khỏi doanh thu gộp).</summary>
    public static readonly WalletTransactionType[] FeeTypes =
    [
        WalletTransactionType.PlatformFee,
        WalletTransactionType.PenaltySurcharge
    ];

    /// <summary>Kiểm tra một dòng sổ cái có phải thu nhập của Creator không.</summary>
    public static bool IsIncome(WalletTransactionType type, WalletTransactionDirection direction) =>
        direction == WalletTransactionDirection.In
        && Array.IndexOf(IncomeTypes, type) >= 0;

    /// <summary>Kiểm tra loại giao dịch có liên quan tới tính doanh thu không.</summary>
    public static bool IsRelevant(WalletTransactionType type) =>
        Array.IndexOf(RelevantTypes, type) >= 0;

    /// <summary>Kiểm tra một dòng sổ cái có phải PHÍ (trừ vào doanh thu) không.</summary>
    public static bool IsFee(WalletTransactionType type, WalletTransactionDirection direction) =>
        direction == WalletTransactionDirection.Out
        && Array.IndexOf(FeeTypes, type) >= 0;
}
