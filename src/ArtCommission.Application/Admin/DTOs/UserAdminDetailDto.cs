namespace ArtCommission.Application.Admin.DTOs;

/// <summary>
/// DTO chứa thông tin chi tiết hồ sơ tài khoản, vai trò, số dư ví và lịch sử xử phạt (SCR-23 / UC31).
/// </summary>
public sealed record UserAdminDetailDto
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public List<string> Roles { get; set; } = new();

    public bool IsVerified { get; set; }

    public bool IsLockedOut { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public UserAdminWalletDto? Wallet { get; set; }

    public List<UserSanctionHistoryDto> SanctionsHistory { get; set; } = new();
}

public sealed record UserAdminWalletDto
{
    public decimal Balance { get; set; }

    public decimal LockedBalance { get; set; }

    public string Currency { get; set; } = "VND";
}

public sealed record UserSanctionHistoryDto
{
    public Guid Id { get; set; }

    public string ActionType { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public int? DurationDays { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public Guid ActionByAdminId { get; set; }

    public string ActionByAdminName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
