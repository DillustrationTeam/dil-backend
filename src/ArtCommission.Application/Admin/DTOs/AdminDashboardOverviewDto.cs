namespace ArtCommission.Application.Admin.DTOs;

/// <summary>
/// DTO chứa tổng quan số liệu phân tích, KPIs tài chính và hàng đợi vận hành (SCR-24 / UC32).
/// </summary>
public record AdminDashboardOverviewDto
{
    public AdminFinancialKpiDto FinancialKpis { get; init; } = null!;
    public AdminUserMetricsDto UserMetrics { get; init; } = null!;
    public AdminCommissionMetricsDto CommissionMetrics { get; init; } = null!;
    public AdminActionRequiredQueueDto ActionRequiredQueue { get; init; } = null!;
    public List<AdminDailyRevenueDto> RevenueTrend { get; init; } = new();
    public List<AdminRecentTransactionDto> RecentTransactions { get; init; } = new();
}

public record AdminFinancialKpiDto
{
    /// <summary>Tổng giá trị giao dịch đơn vẽ (Gross Merchandise Value - VND).</summary>
    public decimal TotalGmv { get; init; }

    /// <summary>Tổng tiền cọc Escrow đang bị khóa trong hệ thống (VND).</summary>
    public decimal ActiveEscrowLocked { get; init; }

    /// <summary>Doanh thu ròng của nền tảng từ phí sàn (VND).</summary>
    public decimal NetPlatformRevenue { get; init; }

    /// <summary>Tổng số dư tiền trong ví người dùng (VND).</summary>
    public decimal TotalPlatformBalance { get; init; }

    /// <summary>Tổng số tiền đã rút về ngân hàng thành công (VND).</summary>
    public decimal TotalPayoutsProcessed { get; init; }
}

public record AdminUserMetricsDto
{
    public int TotalUsers { get; init; }
    public int NewUsersToday { get; init; }
    public int NewUsersLast7Days { get; init; }
    public int NewUsersLast30Days { get; init; }
    public int TotalClients { get; init; }
    public int TotalCreators { get; init; }
    public int TotalModerators { get; init; }
}

public record AdminCommissionMetricsDto
{
    public int TotalCommissions { get; init; }
    public int PendingAcceptanceCount { get; init; }
    public int InProgressCount { get; init; }
    public int CompletedCount { get; init; }
    public int CancelledCount { get; init; }
    public int DisputedCount { get; init; }
}

public record AdminActionRequiredQueueDto
{
    public int PendingDisputesCount { get; init; }
    public int PendingCreatorApplicationsCount { get; init; }
    public int PendingPayoutsCount { get; init; }
    public int FlaggedArtworksCount { get; init; }
}

public record AdminDailyRevenueDto
{
    public string Date { get; init; } = string.Empty; // "yyyy-MM-dd"
    public decimal Gmv { get; init; }
    public decimal PlatformRevenue { get; init; }
    public decimal EscrowDeposited { get; init; }
}

public record AdminRecentTransactionDto
{
    public Guid Id { get; init; }
    public Guid WalletId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string UserEmail { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal BalanceAfter { get; init; }
    public string? Note { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
