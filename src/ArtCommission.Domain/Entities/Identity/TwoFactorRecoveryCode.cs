using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Identity;

public class TwoFactorRecoveryCode : BaseEntity
{
    public Guid UserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public bool IsUsed { get; set; }
    public DateTimeOffset? UsedAt { get; set; }

    public ApplicationUser? User { get; set; }
}
