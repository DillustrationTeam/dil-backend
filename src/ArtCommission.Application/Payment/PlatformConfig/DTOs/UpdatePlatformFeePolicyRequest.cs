namespace ArtCommission.Application.Payment.PlatformConfig.DTOs;

/// <summary>
/// Request payload cập nhật cấu hình phí sàn và chính sách (SCR-18).
/// </summary>
public record UpdatePlatformFeePolicyRequest
{
    /// <summary>
    /// Tỷ lệ phần trăm phí hoa hồng sàn (5.0% - 15.0%).
    /// </summary>
    public decimal PlatformFeePercent { get; init; } = 10.0m;

    /// <summary>
    /// Thời gian tự động duyệt cột mốc (ngày, 1 - 30 ngày).
    /// </summary>
    public int MilestoneAutoApprovalDays { get; init; } = 7;

    /// <summary>
    /// Số lần yêu cầu chỉnh sửa miễn phí mặc định (0 - 20 lần).
    /// </summary>
    public int DefaultFreeRevisionLimit { get; init; } = 2;

    /// <summary>
    /// Thời gian hết hạn của AWS S3 / Cloudinary Presigned URL (phút, 5 - 1440 phút).
    /// </summary>
    public int PresignedUrlExpirationMinutes { get; init; } = 15;

    /// <summary>
    /// % giảm giá đơn áp dụng toàn sàn khi khách gắn ảnh AI phác thảo làm reference (0% - 30%).
    /// </summary>
    public decimal AiReferenceSketchDiscountPercent { get; init; } = 0m;

    /// <summary>
    /// Giới hạn số lượt sinh ảnh AI phác thảo bố cục mỗi (khách, creator) trong 24 giờ (1 - 10 lượt).
    /// </summary>
    public int AiReferenceSketchMaxGenerationsPerWindow { get; init; } = 3;
}
