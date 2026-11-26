namespace ArtCommission.Application.Auction.DTOs;

/// <summary>
/// One evidence row as shown in the moderation sheet.
/// <see cref="Code"/> stays available for clients that want to group, but the UI must render
/// <see cref="Label"/> - the enum name is never meant to reach end users.
/// </summary>
public sealed record RiskSignalDto(
    Guid SignalId,
    string Code,
    string Label,
    decimal Weight,
    string Evidence,
    Guid? BidId,
    Guid? UserId,
    DateTimeOffset DetectedAt);

/// <summary>Full risk sheet for one auction (moderator view).</summary>
public sealed record AuctionRiskAssessmentDto(
    Guid AssessmentId,
    Guid AuctionId,
    string AuctionTitle,
    string? ThumbnailUrl,
    decimal CurrentPrice,
    decimal Score,
    string RiskLevel,
    string RiskLevelLabel,
    string Status,
    int SignalCount,
    string Summary,
    DateTimeOffset DetectedAt,
    DateTimeOffset? LastRiskScanAt,
    Guid? ReviewedByUserId,
    DateTimeOffset? ReviewedAt,
    string? DecisionNote,
    IReadOnlyList<RiskSignalDto> Signals);

/// <summary>Row of the risk queue (list view - no evidence payload, that is fetched on open).</summary>
public sealed record RiskAlertListItemDto(
    Guid AssessmentId,
    Guid AuctionId,
    string AuctionTitle,
    string? ThumbnailUrl,
    decimal CurrentPrice,
    int BidCount,
    decimal Score,
    string RiskLevel,
    string RiskLevelLabel,
    string Status,
    int SignalCount,
    string Summary,
    DateTimeOffset DetectedAt,
    DateTimeOffset? LastBidAt);

/// <summary>Neutral seller-facing row: no score, no signal names.</summary>
public sealed record MyAuctionRiskFlagDto(
    Guid AuctionId,
    string AuctionTitle,
    string Status,
    DateTimeOffset DetectedAt);

/// <summary>Body of POST /auctions/{auctionId}/risk/review.</summary>
public sealed record ReviewAuctionRiskRequest(string Status, string? Note);
