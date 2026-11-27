using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Auction.Risk.Rules;

/// <summary>
/// Two or more accounts bidding in the same auction from the same IP - the primary collusion
/// signal. IP alone is never proof (office NAT, campus wifi, 4G carrier NAT), which is why the
/// weight stays under the alert threshold: it needs a second signal to become actionable.
/// </summary>
public sealed class SharedDeviceRule : IRiskRule
{
    public RiskSignalCode Code => RiskSignalCode.SharedDevice;

    public bool IsHotPathSafe => false;

    public IReadOnlyList<RiskSignalDraft> Evaluate(RiskContext context, RiskSettings settings)
    {
        var bidders = context.Bids.Select(b => b.BidderId).Distinct().ToList();
        if (bidders.Count < 2)
        {
            return [];
        }

        var drafts = new List<RiskSignalDraft>();
        var reported = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < bidders.Count; i++)
        {
            var left = context.BidderDevices.GetValueOrDefault(bidders[i], DeviceFingerprint.Empty);
            if (left.Ips.Count == 0)
            {
                continue;
            }

            for (var j = i + 1; j < bidders.Count; j++)
            {
                var right = context.BidderDevices.GetValueOrDefault(bidders[j], DeviceFingerprint.Empty);
                var sharedIp = left.SharedIp(right);
                if (sharedIp is null)
                {
                    continue;
                }

                var pairKey = $"{Math.Min(i, j)}-{Math.Max(i, j)}";
                if (!reported.Add(pairKey))
                {
                    continue;
                }

                var corroborated = left.SharesUserAgent(right);
                drafts.Add(RiskSignalDraft.ForUser(
                    Code,
                    bidders[j],
                    $"Dùng chung IP {MaskIp(sharedIp)} giữa {context.AliasOf(bidders[i])} và {context.AliasOf(bidders[j])}"
                    + (corroborated ? " (cùng trình duyệt)" : string.Empty)));
            }
        }

        return drafts;
    }

    /// <summary>113.161.32.7 -> 113.161.32.x: enough to correlate, not enough to identify a person.</summary>
    internal static string MaskIp(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return "unknown";
        }

        // Local development and loopback logins: name it instead of printing "::1" as a fake prefix.
        if (ip is "::1" or "127.0.0.1" or "0.0.0.1")
        {
            return "loopback";
        }

        var parts = ip.Split('.');
        if (parts.Length == 4)
        {
            return $"{parts[0]}.{parts[1]}.{parts[2]}.x";
        }

        // IPv6: keep the first three groups (network prefix only), never the interface identifier.
        if (ip.Contains(':'))
        {
            var groups = ip.Split(':', StringSplitOptions.RemoveEmptyEntries);
            return groups.Length >= 3 ? string.Join(':', groups.Take(3)) + ":…" : "ipv6:…";
        }

        return "ip:" + ip[..Math.Min(6, ip.Length)] + "x";
    }
}

/// <summary>
/// A bidder sharing an IP with the seller: the textbook "seller bids on their own artwork with a
/// second account" pattern.
/// </summary>
public sealed class SellerLinkedBidderRule : IRiskRule
{
    public RiskSignalCode Code => RiskSignalCode.SellerLinkedBidder;

    public bool IsHotPathSafe => false;

    public IReadOnlyList<RiskSignalDraft> Evaluate(RiskContext context, RiskSettings settings)
    {
        if (context.SellerDevices.Ips.Count == 0)
        {
            return [];
        }

        var drafts = new List<RiskSignalDraft>();
        foreach (var bidderId in context.Bids.Select(b => b.BidderId).Distinct())
        {
            if (bidderId == context.Auction.SellerId)
            {
                continue;
            }

            var devices = context.BidderDevices.GetValueOrDefault(bidderId, DeviceFingerprint.Empty);
            var sharedIp = devices.SharedIp(context.SellerDevices);
            if (sharedIp is null)
            {
                continue;
            }

            drafts.Add(RiskSignalDraft.ForUser(
                Code,
                bidderId,
                $"{context.AliasOf(bidderId)} dùng chung IP {SharedDeviceRule.MaskIp(sharedIp)} với người bán"));
        }

        return drafts;
    }
}
