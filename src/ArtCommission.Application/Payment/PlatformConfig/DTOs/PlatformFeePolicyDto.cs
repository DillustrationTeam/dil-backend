namespace ArtCommission.Application.Payment.PlatformConfig.DTOs;

/// <summary>
/// DTO chứa các thông số cấu hình phí sàn và chính sách vận hành (SCR-18 / UC33).
/// </summary>
public record PlatformFeePolicyDto
{
    /// <summary>
    /// Tỷ lệ phần trăm phí hoa hồng sàn (5.0% - 15.0%, mặc định 10.0%).
    /// </summary>
    public decimal PlatformFeePercent { get; init; } = 10.0m;

    /// <summary>
    /// Thời gian tự động duyệt cột mốc (ngày, mặc định 7 ngày theo BR-37).
    /// </summary>
    public int MilestoneAutoApprovalDays { get; init; } = 7;

    /// <summary>
    /// Giới hạn số lần yêu cầu chỉnh sửa miễn phí mặc định (mặc định 2).
    /// </summary>
    public int DefaultFreeRevisionLimit { get; init; } = 2;

    /// <summary>
    /// Thời gian hết hạn của AWS S3 / Cloudinary Presigned URL (phút, mặc định 15 phút theo BR-39).
    /// </summary>
    public int PresignedUrlExpirationMinutes { get; init; } = 15;

    /// <summary>
    /// Thời điểm cập nhật cấu hình lần cuối.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; init; }
}
