namespace ArtCommission.Domain.Enums;

/// <summary>
/// Vòng đời phiên đấu giá (UC32–UC35).
/// Luồng hợp lệ: Scheduled → Active → Ended → Settled.
/// Nhánh thoát: Scheduled/Active → Cancelled; Ended → Expired (winner quá hạn thanh toán).
/// </summary>
public enum AuctionStatus
{
    /// <summary>Đã tạo nhưng chưa tới start_at. Còn sửa/xoá được, chưa nhận bid.</summary>
    Scheduled,

    /// <summary>Đang diễn ra, nhận bid.</summary>
    Active,

    /// <summary>Đã hết end_at nhưng chưa chốt. Vẫn giữ tiền cọc của bidder dẫn đầu.</summary>
    Ended,

    /// <summary>Đã chốt: đã xác định winner và ghi nhận chuyển quyền sở hữu.</summary>
    Settled,

    /// <summary>Người bán huỷ khi chưa phát sinh bid.</summary>
    Cancelled,

    /// <summary>Winner quá payment_deadline không thanh toán — huỷ kết quả.</summary>
    Expired
}

/// <summary>Loại phiên đấu giá.</summary>
public enum AuctionType
{
    /// <summary>Đấu giá thường — bán cho người trả giá cao nhất.</summary>
    Standard,

    /// <summary>Đấu giá có giá mua ngay (buy-now) kết thúc sớm.</summary>
    BuyNow
}

/// <summary>
/// Trạng thái một lượt đặt giá.
/// Bất biến: tối đa MỘT bid <see cref="Leading"/> cho mỗi phiên tại mọi thời điểm.
/// </summary>
public enum BidStatus
{
    /// <summary>Đang dẫn đầu — đang giữ tiền cọc.</summary>
    Leading,

    /// <summary>Bị đè giá bởi bid cao hơn — tiền cọc đã được giải phóng.</summary>
    Outbid,

    /// <summary>Thắng phiên — chờ thanh toán / đã chốt.</summary>
    Won,

    /// <summary>Thua khi phiên chốt.</summary>
    Lost,

    /// <summary>Người dùng tự huỷ lượt bid (chỉ khi còn Leading và phiên chưa Ended).</summary>
    Cancelled,

    /// <summary>Winner quá hạn thanh toán — lượt bid bị vô hiệu, cọc đã hoàn.</summary>
    Expired
}

/// <summary>Trạng thái tiền cọc của một lượt bid.</summary>
public enum HoldStatus
{
    /// <summary>Chưa khoá tiền (bid vừa khởi tạo, chưa vào transaction).</summary>
    None,

    /// <summary>Đang giữ trong ví (LockedBalance).</summary>
    Held,

    /// <summary>Đã giải ngân cho người bán khi chốt phiên.</summary>
    Released,

    /// <summary>Đã hoàn về số dư khả dụng (bị đè giá / thua / quá hạn).</summary>
    Refunded
}

/// <summary>Lý do ghi nhận một dòng chuyển quyền sở hữu tranh.</summary>
public enum OwnershipTransferReason
{
    /// <summary>Chuyển do thắng đấu giá.</summary>
    AuctionWin,

    /// <summary>Chuyển do mua trực tiếp (buy-now).</summary>
    BuyNow,

    /// <summary>Chuyển do đơn đặt vẽ hoàn tất (commission).</summary>
    CommissionCompleted,

    /// <summary>Admin gán tay để sửa sai lệch.</summary>
    AdminAdjustment
}
