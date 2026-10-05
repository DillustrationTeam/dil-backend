namespace ArtCommission.Application.Admin.DTOs;

/// <summary>
/// DTO biểu diễn tóm tắt thông tin tài khoản người dùng trong danh sách quản trị (SCR-23 / UC31).
/// </summary>
public sealed record UserAdminListItemDto
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = new();

    public bool IsVerified { get; set; }

    public bool IsLockedOut { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }

    public decimal WalletBalance { get; set; }

    public decimal WalletLockedBalance { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
