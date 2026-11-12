using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Auction;

/// <summary>
/// Bản ghi giao nhận tranh vật lý và lưu trữ biên lai vận đơn (UC35).
/// </summary>
public class AuctionDelivery : BaseEntity
{
    public Guid AuctionId { get; set; }

    /// <summary>Người nhận tranh vật lý — người thắng đấu giá.</summary>
    public Guid RecipientId { get; set; }

    /// <summary>Họ tên người nhận.</summary>
    public string RecipientName { get; set; } = string.Empty;

    /// <summary>Số điện thoại liên hệ nhận hàng.</summary>
    public string RecipientPhone { get; set; } = string.Empty;

    /// <summary>Địa chỉ chi tiết nhận tranh (số nhà, đường, phường/xã, quận/huyện, tỉnh/thành).</summary>
    public string ShippingAddress { get; set; } = string.Empty;

    /// <summary>Ghi chú cho người bán khi đóng gói và vận chuyển.</summary>
    public string? NoteToSeller { get; set; }

    /// <summary>Đơn vị vận chuyển (GHTK, GHN, Viettel Post, VNPost, Khác...).</summary>
    public string? CarrierName { get; set; }

    /// <summary>Mã vận đơn bưu cục (Tracking Number).</summary>
    public string? TrackingCode { get; set; }

    /// <summary>Đường dẫn tra cứu bưu phẩm trực tiếp.</summary>
    public string? TrackingUrl { get; set; }

    /// <summary>Ảnh chụp biên lai gửi hàng bưu cục / phiếu gửi có dấu mộc.</summary>
    public string? ShippingReceiptUrl { get; set; }

    /// <summary>Ảnh chụp kiện hàng đã đóng gói và dán phiếu gửi.</summary>
    public string? PackageImageUrl { get; set; }

    /// <summary>Ảnh biên lai phát hàng / chữ ký nhận hoặc ảnh bưu tá giao tận tay (PoD).</summary>
    public string? DeliveryReceiptUrl { get; set; }

    /// <summary>Trạng thái giao nhận.</summary>
    public DeliveryStatus Status { get; set; } = DeliveryStatus.AwaitingShipment;

    /// <summary>Thời điểm người bán cập nhật mã vận đơn và biên lai gửi.</summary>
    public DateTimeOffset? ShippedAt { get; set; }

    /// <summary>Thời điểm người mua nhận được hàng hoặc bưu cục báo phát thành công.</summary>
    public DateTimeOffset? DeliveredAt { get; set; }

    /// <summary>Thời điểm người mua bấm xác nhận hài lòng hoặc hệ thống auto-confirm.</summary>
    public DateTimeOffset? ConfirmedAt { get; set; }

    /// <summary>Lý do khiếu nại (nếu có).</summary>
    public string? DisputeReason { get; set; }

    /// <summary>Bằng chứng khiếu nại (ảnh/video mở kiện hàng).</summary>
    public string? DisputeEvidenceUrl { get; set; }

    /// <summary>
    /// Khiếu nại được gửi SAU khi ký quỹ đã giải ngân cho người bán (người mua đã nhận bản điện
    /// tử nhưng vẫn yêu cầu giao tranh vật lý). Loại khiếu nại này không đóng băng được tiền,
    /// chỉ ghi nhận để Admin đối soát và để người mua đánh giá người bán.
    /// </summary>
    public bool IsPostPayoutDispute { get; set; }

    /// <summary>
    /// Kết quả phân xử: <c>release</c> (bác khiếu nại), <c>refund</c> (hoàn tiền),
    /// <c>paid-out</c> (đóng khiếu nại vì tiền đã về ví người bán, không thu hồi được).
    /// Null khi khiếu nại chưa được xử lý.
    /// </summary>
    public string? DisputeResolution { get; set; }

    /// <summary>Thời điểm Admin đóng khiếu nại.</summary>
    public DateTimeOffset? DisputeResolvedAt { get; set; }

    /// <summary>Ghi chú phân xử của Admin (lưu vào nhật ký đơn giao nhận).</summary>
    public string? DisputeResolvedNote { get; set; }

    // Navigation
    public Auction? Auction { get; set; }
}
