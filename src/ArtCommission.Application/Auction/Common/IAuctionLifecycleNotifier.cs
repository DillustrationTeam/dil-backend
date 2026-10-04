namespace ArtCommission.Application.Auction.Common;

public enum AuctionLifecycleEvent
{
    Started,
    Ended,
    Extended
}

public interface IAuctionLifecycleNotifier
{
    Task NotifyParticipantsAsync(
        Guid auctionId,
        AuctionLifecycleEvent lifecycleEvent,
        Guid? operationId = null,
        CancellationToken cancellationToken = default);
}
