namespace ArtCommission.Application.Ai.ReferenceSketch;

/// <summary>
/// Hằng số giới hạn cho tính năng AI phác thảo bố cục reference — dùng chung giữa
/// command khởi tạo, query quota và job sinh ảnh để không lệch số giữa các nơi.
/// </summary>
public static class ReferenceSketchLimits
{
    public const int MinBriefLength = 30;
    public const int MaxLayoutHintLength = 300;
    public const int MaxGenerationsPerWindow = 3;
    public static readonly TimeSpan QuotaWindow = TimeSpan.FromHours(24);
}
