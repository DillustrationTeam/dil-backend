using ArtCommission.Domain.Common;
using ArtCommission.Domain.Entities.Identity;

namespace ArtCommission.Domain.Entities.ArtistStudio;

public class ClientProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public string? Username { get; set; }
    public DateTimeOffset? UsernameChangedAt { get; set; }
    public List<string> InterestTags { get; set; } = new();
    public string? Country { get; set; }
    public string? Timezone { get; set; }
    public List<string> PreferredLanguages { get; set; } = new();

    /// <summary>
    /// Các tab nào trên trang hồ sơ công khai (client-profile-screen) được hiển thị cho người khác xem.
    /// Giá trị hợp lệ: "reputation", "saved", "liked" — "requested" (đơn đã đặt) luôn riêng tư, không nằm trong danh sách này.
    /// </summary>
    public List<string> PublicVisibleTabs { get; set; } = new();

    public ApplicationUser? User { get; set; }
}
