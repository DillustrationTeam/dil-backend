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
    /// % giảm giá đơn áp dụng toàn sàn khi khách gắn ảnh AI phác thảo làm reference (0% - 30%, mặc định 0%).
    /// </summary>
    public decimal AiReferenceSketchDiscountPercent { get; init; } = 0m;

    /// <summary>
    /// Giới hạn số lượt sinh ảnh AI phác thảo bố cục mỗi (khách, creator) trong 24 giờ (1 - 10 lượt, mặc định 3).
    /// </summary>
    public int AiReferenceSketchMaxGenerationsPerWindow { get; init; } = 3;

    /// <summary>
    /// Thời điểm cập nhật cấu hình lần cuối.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; init; }
}
