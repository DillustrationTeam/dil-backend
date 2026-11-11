namespace ArtCommission.Application.Auction.Common;

/// <summary>
/// Hằng số dùng chung cho nghiệp vụ giao nhận tranh vật lý.
/// </summary>
public static class AuctionDeliveryRules
{
    /// <summary>
    /// Sau khi người bán gửi hàng, nếu người mua không bấm xác nhận đã nhận thì hệ thống
    /// tự động hoàn tất đơn và giải ngân ký quỹ sau số giờ này.
    /// </summary>
    public const int AutoConfirmHours = 48;
}
