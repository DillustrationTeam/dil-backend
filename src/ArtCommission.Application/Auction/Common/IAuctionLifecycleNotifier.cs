namespace ArtCommission.Application.Auction.Common;

public enum AuctionLifecycleEvent
{
    Started,
    Ended,
    Extended,

    /// <summary>
    /// Phiên bị huỷ (người bán hoặc quản trị viên huỷ trước khi có lượt đặt giá).
    /// Tách khỏi <see cref="Ended"/> vì câu thông báo khác nhau: "đã huỷ" ≠ "đã kết thúc",
    /// và dedupKey cần khác nhau để không chặn mất thông báo huỷ.
    /// </summary>
    Cancelled
}

public interface IAuctionLifecycleNotifier
{
    Task NotifyParticipantsAsync(
        Guid auctionId,
        AuctionLifecycleEvent lifecycleEvent,
        Guid? operationId = null,
        CancellationToken cancellationToken = default);
}
