namespace ArtCommission.Domain.Enums;

/// <summary>
/// Loại biến động số dư trong sổ cái ví.
/// Sổ cái là append-only: mỗi lần số dư đổi PHẢI ghi 1 dòng.
///
/// QUAN TRỌNG — hai loại hoàn tiền phải tách riêng, nếu gộp sẽ không đối soát được:
///   - <see cref="Refund"/>         : tiền vào ví từ BÊN NGOÀI (Admin từ chối payout)
///   - <see cref="RefundFromHold"/> : tiền chuyển từ ĐANG GIỮ về số dư khả dụng
///
/// Nhờ tách riêng, công thức đối soát luôn đúng:
///   Balance       = Deposit + Refund − Payout − EscrowHold + RefundFromHold + Adjustment
///   LockedBalance = EscrowHold − EscrowRelease − RefundFromHold
/// </summary>
public enum WalletTransactionType
{
    /// <summary>Nạp tiền vào ví qua cổng thanh toán (UC48).</summary>
    Deposit,

    /// <summary>Khoá tiền vào escrow / khoá cọc khi đặt giá đấu giá.</summary>
    EscrowHold,

    /// <summary>Giải ngân tiền escrow cho Creator (tiền RỜI ví).</summary>
    EscrowRelease,

    /// <summary>Hoàn tiền vào ví từ bên ngoài (Admin từ chối payout).</summary>
    Refund,

    /// <summary>
    /// Hoàn tiền ĐANG GIỮ về lại số dư khả dụng (huỷ đơn, bidder thua, winner quá hạn).
    /// Tiền không đến từ ngoài mà chuyển từ LockedBalance sang Balance.
    /// </summary>
    RefundFromHold,

    /// <summary>Rút tiền về ngân hàng (UC49).</summary>
    Payout,

    /// <summary>Phí nền tảng trừ trên giao dịch.</summary>
    PlatformFee,

    /// <summary>Điều chỉnh thủ công bởi Admin khi đối soát lệch số.</summary>
    Adjustment
}

public static class WalletTransactionTypeNames
{
    public const string Deposit = nameof(WalletTransactionType.Deposit);
    public const string EscrowHold = nameof(WalletTransactionType.EscrowHold);
    public const string EscrowRelease = nameof(WalletTransactionType.EscrowRelease);
    public const string Refund = nameof(WalletTransactionType.Refund);
    public const string RefundFromHold = nameof(WalletTransactionType.RefundFromHold);
    public const string Payout = nameof(WalletTransactionType.Payout);
    public const string PlatformFee = nameof(WalletTransactionType.PlatformFee);
    public const string Adjustment = nameof(WalletTransactionType.Adjustment);

    public static readonly string[] All =
        [Deposit, EscrowHold, EscrowRelease, Refund, RefundFromHold, Payout, PlatformFee, Adjustment];
}
