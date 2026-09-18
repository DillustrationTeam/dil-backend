namespace ArtCommission.Domain.Enums;

/// <summary>
/// Loại biến động số dư trong sổ cái ví.
/// Sổ cái là append-only: mỗi lần số dư đổi PHẢI ghi 1 dòng.
/// </summary>
public enum WalletTransactionType
{
    /// <summary>Nạp tiền vào ví qua cổng thanh toán (UC48).</summary>
    Deposit,

    /// <summary>Khoá tiền vào escrow / khoá cọc khi đặt giá đấu giá.</summary>
    EscrowHold,

    /// <summary>Giải ngân tiền escrow cho Creator.</summary>
    EscrowRelease,

    /// <summary>Hoàn tiền (huỷ đơn, từ chối payout, winner quá hạn thanh toán...).</summary>
    Refund,

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
    public const string Payout = nameof(WalletTransactionType.Payout);
    public const string PlatformFee = nameof(WalletTransactionType.PlatformFee);
    public const string Adjustment = nameof(WalletTransactionType.Adjustment);

    public static readonly string[] All =
        [Deposit, EscrowHold, EscrowRelease, Refund, Payout, PlatformFee, Adjustment];
}
