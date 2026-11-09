using System.Text.Json.Serialization;

namespace ArtCommission.Domain.Enums;

/// <summary>
/// Phân loại vi phạm khiếu nại trong giao dịch Art Trade.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TradeDisputeCategory
{
    AiArt = 1,
    Plagiarism = 2,
    AgreementBreach = 3,
    DeadlineMissed = 4,
    TosViolation = 5,
    Other = 6
}

public static class TradeDisputeCategoryExtensions
{
    /// <summary>
    /// Chuyển đổi linh hoạt chuỗi phân loại (hỗ trợ cả snake_case như AI_ART lẫn PascalCase như AiArt).
    /// </summary>
    public static TradeDisputeCategory? ParseFlexible(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var normalized = input.Trim().Replace("_", "").Replace("-", "").Replace(" ", "");
        if (Enum.TryParse<TradeDisputeCategory>(normalized, ignoreCase: true, out var result))
        {
            return result;
        }

        return TradeDisputeCategory.Other;
    }
}

