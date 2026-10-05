using Microsoft.AspNetCore.Identity;

namespace ArtCommission.Domain.Entities.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public bool IsVerified { get; set; } = false;

    /// <summary>
    /// Username đăng nhập do user chọn (vd lúc hoàn tất đăng ký qua Google). Null với các tài khoản
    /// tạo trước khi field này tồn tại. ĐẶT TÊN KHÁC "UserName"/"NormalizedUserName" của Identity
    /// (không chỉ khác hoa/thường) — SQL Server collation mặc định case-insensitive nên 2 cột chỉ
    /// khác hoa/thường sẽ bị coi là trùng tên.
    /// </summary>
    public string? LoginUsername { get; set; }
    public string? NormalizedLoginUsername { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }

    // Profile Settings
    public string? AvatarUrl { get; set; }
    public string? CoverUrl { get; set; }
    public string? Bio { get; set; }
    public List<string> SocialLinks { get; set; } = new();

    // Navigation Properties
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
