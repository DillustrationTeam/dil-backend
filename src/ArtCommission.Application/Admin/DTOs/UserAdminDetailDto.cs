namespace ArtCommission.Application.Admin.DTOs;

/// <summary>
/// DTO chứa thông tin chi tiết hồ sơ tài khoản, vai trò, số dư ví và lịch sử xử phạt (SCR-23 / UC31).
/// </summary>
public record UserAdminDetailDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public List<string> Roles { get; init; } = new();
    public bool IsVerified { get; init; }
    public bool IsLockedOut { get; init; }
    public DateTimeOffset? LockoutEnd { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }

    public UserAdminWalletDto? Wallet { get; init; }
    public List<UserSanctionHistoryDto> SanctionsHistory { get; init; } = new();
}

public record UserAdminWalletDto
{
    public decimal Balance { get; init; }
    public decimal LockedBalance { get; init; }
    public string Currency { get; init; } = "VND";
}

public record UserSanctionHistoryDto
{
    public Guid Id { get; init; }
    public string ActionType { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public int? DurationDays { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public Guid ActionByAdminId { get; init; }
    public string ActionByAdminName { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}
