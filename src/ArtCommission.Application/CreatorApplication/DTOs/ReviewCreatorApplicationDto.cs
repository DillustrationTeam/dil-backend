using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.CreatorApplication.DTOs;
public class ReviewCreatorApplicationDto 
{
    public ApplicationStatus Status { get; set; }
    public string? ReviewNote { get; set; }

    /// <summary>
    /// Cấp huy hiệu xác thực vẽ tay không dùng AI (IsAiVerified = true) khi duyệt đơn (SCR-21).
    /// Mặc định: true.
    /// </summary>
    public bool GrantAiVerifiedBadge { get; set; } = true;
}