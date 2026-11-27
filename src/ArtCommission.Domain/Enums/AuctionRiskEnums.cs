namespace ArtCommission.Domain.Enums;

/// <summary>
/// Severity bucket derived from <c>AuctionRiskAssessment.Score</c> (0-100).
///
/// Thresholds live in PlatformConfig (<c>auction.risk.threshold.*</c>) so the operator can
/// retune them without a deploy. The enum only names the buckets; it never stores the numbers.
/// </summary>
public enum AuctionRiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Review state of one assessment.
///
/// Invariant: at most ONE assessment per auction may sit in
/// <see cref="Open"/> or <see cref="UnderReview"/>; enforced by a filtered unique index.
/// Dismissed/Confirmed rows are history and may repeat.
/// </summary>
public enum AuctionRiskAssessmentStatus
{
    /// <summary>Detector scored the auction above the noise floor; nobody looked yet.</summary>
    Open = 1,

    /// <summary>A moderator opened the evidence sheet.</summary>
    UnderReview = 2,

    /// <summary>Reviewed and cleared - the pattern was innocent (shared NAT, kiosk IP, ...).</summary>
    Dismissed = 3,

    /// <summary>Reviewed and judged fraudulent; downstream punishment flow owns the rest.</summary>
    Confirmed = 4
}

/// <summary>
/// One detectable fraud pattern. Codes are stable identifiers persisted in
/// <c>AuctionRiskSignals.SignalCode</c>; the Vietnamese label shown to a moderator lives in the
/// frontend, never in the API payload.
/// </summary>
public enum RiskSignalCode
{
    /// <summary>Two or more distinct bidders of the same auction share an IP or user agent.</summary>
    SharedDevice = 1,

    /// <summary>A bidder shares an IP/user agent with the auction's seller.</summary>
    SellerLinkedBidder = 2,

    /// <summary>Bid rate inside a short window is far above normal.</summary>
    BidVelocity = 3,

    /// <summary>A single bid jumps far beyond the configured step.</summary>
    PriceJump = 4,

    /// <summary>Several consecutive bids move by exactly one step, pushed by the same bidder.</summary>
    PriceLadder = 5,

    /// <summary>The bidder previously let auctions expire unpaid.</summary>
    NonPaymentHistory = 6,

    /// <summary>The bidder wins often but pays rarely.</summary>
    WinNoPaymentRate = 7,

    /// <summary>Two accounts outbid each other back and forth inside one auction.</summary>
    SelfOutbidLoop = 8,

    /// <summary>Several auto-bid ceilings stacked almost on top of each other.</summary>
    AutoBidWall = 9,

    /// <summary>A very new account bids aggressively.</summary>
    NewAccountBurst = 10,

    /// <summary>
    /// A cluster of bids inside a short window pushed by SEVERAL accounts with almost no idle time
    /// between them. Distinct from <see cref="BidVelocity"/> (raw speed, one or many accounts) and
    /// from <see cref="PriceLadder"/> (one account, one step each): this is the "dàn xếp" pattern -
    /// two accounts alternating a war nobody real is fighting.
    /// </summary>
    BidBurstCluster = 11
}
