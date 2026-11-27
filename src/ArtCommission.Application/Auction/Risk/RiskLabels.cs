using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Auction.Risk;

/// <summary>
/// Display labels for risk enums. Shared by the API payloads and the scorer summary so the
/// moderator never sees two different words for the same bucket.
/// </summary>
public static class RiskLabels
{
    public static string LevelLabel(AuctionRiskLevel level) => level switch
    {
        AuctionRiskLevel.Critical => "Nghiêm trọng",
        AuctionRiskLevel.High => "Cao",
        AuctionRiskLevel.Medium => "Vừa",
        _ => "Thấp"
    };

    public static string StatusLabel(AuctionRiskAssessmentStatus status) => status switch
    {
        AuctionRiskAssessmentStatus.Open => "Chờ xem",
        AuctionRiskAssessmentStatus.UnderReview => "Đang xem",
        AuctionRiskAssessmentStatus.Dismissed => "Đã bỏ qua",
        _ => "Đã xác nhận"
    };
}
