namespace ArtCommission.Domain.Enums;

/// <summary>
/// Trạng thái ví người dùng.
/// Active: dùng bình thường · Locked: tạm khoá (đang điều tra) · Closed: đã đóng.
/// </summary>
public enum WalletStatus
{
    Active,
    Locked,
    Closed
}

public static class WalletStatusNames
{
    public const string Active = nameof(WalletStatus.Active);
    public const string Locked = nameof(WalletStatus.Locked);
    public const string Closed = nameof(WalletStatus.Closed);

    public static readonly string[] All = [Active, Locked, Closed];
}
