using ArtCommission.Domain.Common;
using ArtCommission.Domain.Entities.Identity;

namespace ArtCommission.Domain.Entities.ArtistStudio;

public class CreatorProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Headline { get; set; }
    public string? Bio { get; set; }
    public string? Specialties { get; set; }
    public string? Location { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? BannerUrl { get; set; }
    public bool IsAcceptingOrders { get; set; } = true;
    public int CommissionSlots { get; set; } // Số slot nhận vẽ (database.sql)
    public int CompletedOrdersCount { get; set; } // Số đơn hoàn thành (database.sql)
    public string? RateCard { get; set; } // JSON config bảng giá & dịch vụ (database.sql)
    public bool IsApproved { get; set; }
    public bool IsAiVerified { get; set; } // Huy hiệu xác thực vẽ tay không dùng AI (SCR-21)
    public decimal RatingAverage { get; set; }
    public int RatingCount { get; set; }
    public int FollowerCount { get; set; }
    public int AvailableSlots { get; set; }

    public ApplicationUser? User { get; set; }
    public ICollection<Artwork> Artworks { get; set; } = new List<Artwork>();
}
