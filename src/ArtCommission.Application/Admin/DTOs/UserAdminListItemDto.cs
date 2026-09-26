namespace ArtCommission.Application.Admin.DTOs;

/// <summary>
/// DTO biểu diễn tóm tắt thông tin tài khoản người dùng trong danh sách quản trị (SCR-23 / UC31).
/// </summary>
public record UserAdminListItemDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public List<string> Roles { get; init; } = new();
    public bool IsVerified { get; init; }
    public bool IsLockedOut { get; init; }
    public DateTimeOffset? LockoutEnd { get; init; }
    public decimal WalletBalance { get; init; }
    public decimal WalletLockedBalance { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
