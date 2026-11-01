namespace ArtCommission.Application.ArtistStudio.Moderation;

/// <summary>
/// Kết quả sau khi phân tích/quét AI cho tác phẩm nghệ thuật (SCR-20 / UC28).
/// </summary>
public sealed record ArtworkScanResult(
    bool Success,
    decimal? SafeScore,
    decimal? AdultScore,
    decimal? ViolenceScore,
    bool IsAiGenerated,
    decimal? AiDetectionScore,
    IReadOnlyList<string> SuggestedTags,
    string? FlagReason,
    string? Error = null)
{
    public static ArtworkScanResult Ok(
        decimal safeScore,
        decimal adultScore,
        decimal violenceScore,
        bool isAiGenerated,
        decimal aiDetectionScore,
        IReadOnlyList<string>? suggestedTags = null,
        string? flagReason = null) =>
        new(
            Success: true,
            SafeScore: safeScore,
            AdultScore: adultScore,
            ViolenceScore: violenceScore,
            IsAiGenerated: isAiGenerated,
            AiDetectionScore: aiDetectionScore,
            SuggestedTags: suggestedTags ?? Array.Empty<string>(),
            FlagReason: flagReason,
            Error: null);

    public static ArtworkScanResult Fail(string error) =>
        new(
            Success: false,
            SafeScore: null,
            AdultScore: null,
            ViolenceScore: null,
            IsAiGenerated: false,
            AiDetectionScore: null,
            SuggestedTags: Array.Empty<string>(),
            FlagReason: null,
            Error: error);
}

/// <summary>
/// Cổng dịch vụ AI quét nội dung và kiểm duyệt tác phẩm nghệ thuật (SCR-20 / UC28).
/// </summary>
public interface IArtworkScanService
{
    /// <summary>
    /// Phân tích ảnh tác phẩm từ URL công khai để đánh giá độ an toàn (Safe/Adult/Violence)
    /// và xác suất tranh do AI tạo (IsAiGenerated / AiDetectionScore).
    /// </summary>
    /// <param name="imageUrl">URL ảnh công khai của tác phẩm cần quét.</param>
    /// <param name="cancellationToken">CancellationToken.</param>
    /// <returns><see cref="ArtworkScanResult"/> chứa các chỉ số confidence và lý do gắn cờ (nếu có).</returns>
    Task<ArtworkScanResult> ScanArtworkAsync(string imageUrl, CancellationToken cancellationToken = default);
}

