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
}
