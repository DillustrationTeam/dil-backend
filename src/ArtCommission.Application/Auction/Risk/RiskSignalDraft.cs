using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Auction.Risk;

/// <summary>
/// One detected pattern, before it is scored and persisted.
///
/// <see cref="Evidence"/> holds already-masked, human-readable facts (partial IP, alias, counters);
/// the engine serialises it to JSON and never stores raw emails, phones or full addresses.
/// </summary>
public sealed record RiskSignalDraft(
    RiskSignalCode Code,
    Guid? BidId,
    Guid? UserId,
    string Evidence)
{
    public static RiskSignalDraft ForBid(RiskSignalCode code, Guid bidId, Guid bidderId, string evidence)
        => new(code, bidId, bidderId, evidence);

    public static RiskSignalDraft ForUser(RiskSignalCode code, Guid userId, string evidence)
        => new(code, null, userId, evidence);

    public static RiskSignalDraft ForAuction(RiskSignalCode code, string evidence)
        => new(code, null, null, evidence);
}

/// <summary>
/// A single fraud-detection rule.
///
/// <see cref="IsHotPathSafe"/> flags rules cheap enough to run inside the bid transaction: they may
/// only look at data already loaded for the bid (no device or cross-auction queries). Everything
/// else runs on the background sweep.
/// </summary>
public interface IRiskRule
{
    RiskSignalCode Code { get; }

    bool IsHotPathSafe { get; }

    /// <summary>
    /// Settings are a parameter (not a ctor dependency) because they are loaded from PlatformConfig
    /// per run; injecting them would need a DB read inside DI construction.
    /// </summary>
    IReadOnlyList<RiskSignalDraft> Evaluate(RiskContext context, RiskSettings settings);
}
