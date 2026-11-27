using ArtCommission.Domain.Entities.Auction;

namespace ArtCommission.Application.Auction.Risk;

/// <summary>
/// Immutable snapshot handed to every rule: the auction, its recent bids, and the derived
/// device/history evidence. Rules are pure functions over this object so they are trivially
/// unit-testable and never issue queries themselves.
/// </summary>
public sealed class RiskContext
{
    public required Domain.Entities.Auction.Auction Auction { get; init; }

    /// <summary>Bids of the auction ordered by <c>PlacedAt</c> ascending, oldest first.</summary>
    public required IReadOnlyList<Bid> Bids { get; init; }

    /// <summary>Device fingerprints per distinct bidder id.</summary>
    public required IReadOnlyDictionary<Guid, DeviceFingerprint> BidderDevices { get; init; }

    /// <summary>Device fingerprint of the seller, rebuilt from the last 30 days of refresh tokens.</summary>
    public required DeviceFingerprint SellerDevices { get; init; }

    /// <summary>Won/expired counters per bidder, used by the payment-history rules.</summary>
    public required IReadOnlyDictionary<Guid, BidderHistory> BidderHistory { get; init; }

    /// <summary>Account creation time per bidder, used by the new-account rule.</summary>
    public required IReadOnlyDictionary<Guid, DateTimeOffset> BidderCreatedAt { get; init; }

    public required DateTimeOffset Now { get; init; }

    /// <summary>1-based alias shown instead of a real identity in evidence payloads.</summary>
    public string AliasOf(Guid userId)
    {
        var index = 1;
        foreach (var bid in Bids)
        {
            if (bid.BidderId == userId)
            {
                return $"người đấu giá #{index}";
            }

            index++;
        }

        return userId == Auction.SellerId ? "người bán" : "người đấu giá #?";
    }
}

/// <summary>
/// Network/browser evidence for one user.
///
/// Only the IP set is decisive: a shared user agent alone (the same Chrome build on millions of
/// machines) is corroboration, never proof. That keeps false positives on kiosk/office networks
/// bounded and lets the moderator see exactly which IP matched.
/// </summary>
public sealed record DeviceFingerprint(IReadOnlySet<string> Ips, IReadOnlySet<string> UserAgents)
{
    public static readonly DeviceFingerprint Empty = new(new HashSet<string>(), new HashSet<string>());

    public string? SharedIp(DeviceFingerprint other)
        => Ips.FirstOrDefault(ip => other.Ips.Contains(ip));

    public bool SharesUserAgent(DeviceFingerprint other)
        => UserAgents.Overlaps(other.UserAgents);
}

/// <summary>Payment-reliability counters for one bidder.</summary>
public sealed record BidderHistory(int WonCount, int ExpiredUnpaidCount);
