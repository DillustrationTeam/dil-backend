namespace ArtCommission.Domain.Enums;

/// <summary>
/// Chiều biến động số dư. Amount luôn lưu dương, Direction quyết định cộng hay trừ.
/// </summary>
public enum WalletTransactionDirection
{
    /// <summary>Tiền vào ví (nạp, hoàn tiền, giải ngân).</summary>
    In,

    /// <summary>Tiền ra khỏi ví (rút, khoá escrow, phí).</summary>
    Out
}

public static class WalletTransactionDirectionNames
{
    public const string In = nameof(WalletTransactionDirection.In);
    public const string Out = nameof(WalletTransactionDirection.Out);

    public static readonly string[] All = [In, Out];
}
