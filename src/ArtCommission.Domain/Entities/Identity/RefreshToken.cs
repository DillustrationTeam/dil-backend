using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Identity;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? CreatedByIp { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive => RevokedAt == null && DateTimeOffset.UtcNow < ExpiresAt;

    // Navigation Property
    public virtual ApplicationUser User { get; set; } = null!;
}
