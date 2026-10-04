namespace ArtCommission.Application.Admin.DTOs;

public sealed record AdminDashboardOverviewDto
{
    public AdminFinancialKpiDto FinancialKpis { get; set; } = null!;

    public AdminUserMetricsDto UserMetrics { get; set; } = null!;

    public AdminCommissionMetricsDto CommissionMetrics { get; set; } = null!;

    public AdminActionRequiredQueueDto ActionRequiredQueue { get; set; } = null!;

    public List<AdminDailyRevenueDto> RevenueTrend { get; set; } = new();

    public List<AdminRecentTransactionDto> RecentTransactions { get; set; } = new();
}

public sealed record AdminFinancialKpiDto
{
    /// <summary>Tổng giá trị giao dịch đơn vẽ (Gross Merchandise Value - VND).</summary>
    public decimal TotalGmv { get; set; }

    /// <summary>Tổng tiền cọc Escrow đang bị khóa trong hệ thống (VND).</summary>
    public decimal ActiveEscrowLocked { get; set; }

    /// <summary>Doanh thu ròng của nền tảng từ phí sàn (VND).</summary>
    public decimal NetPlatformRevenue { get; set; }

    /// <summary>Tổng số dư tiền trong ví người dùng (VND).</summary>
    public decimal TotalPlatformBalance { get; set; }

    /// <summary>Tổng số tiền đã rút về ngân hàng thành công (VND).</summary>
    public decimal TotalPayoutsProcessed { get; set; }
}

public sealed record AdminUserMetricsDto
{
    public int TotalUsers { get; set; }

    public int NewUsersToday { get; set; }

    public int NewUsersLast7Days { get; set; }

    public int NewUsersLast30Days { get; set; }

    public int TotalClients { get; set; }

    public int TotalCreators { get; set; }

    public int TotalModerators { get; set; }
}

public sealed record AdminCommissionMetricsDto
{
    public int TotalCommissions { get; set; }

    public int PendingAcceptanceCount { get; set; }

    public int InProgressCount { get; set; }

    public int CompletedCount { get; set; }

    public int CancelledCount { get; set; }

    public int DisputedCount { get; set; }
}

public sealed record AdminActionRequiredQueueDto
{
    public int PendingDisputesCount { get; set; }

    public int PendingCreatorApplicationsCount { get; set; }

    public int PendingPayoutsCount { get; set; }

    public int FlaggedArtworksCount { get; set; }
}

public sealed record AdminDailyRevenueDto
{
    public string Date { get; set; } = string.Empty; // "yyyy-MM-dd"

    public decimal Gmv { get; set; }

    public decimal PlatformRevenue { get; set; }

    public decimal EscrowDeposited { get; set; }
}

public sealed record AdminRecentTransactionDto
{
    public Guid Id { get; set; }

    public Guid WalletId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string UserEmail { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Direction { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public decimal BalanceAfter { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
