using ArtCommission.Application.Admin.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Admin.Queries;

/// <summary>
/// Query lấy báo cáo tổng quan hệ thống, KPIs tài chính, vận hành và dữ liệu biểu đồ (SCR-24 / UC32).
/// </summary>
public record GetAdminDashboardOverviewQuery(int Days = 30) : IRequest<AdminDashboardOverviewDto>;

public class GetAdminDashboardOverviewQueryHandler : IRequestHandler<GetAdminDashboardOverviewQuery, AdminDashboardOverviewDto>
{
    private readonly IApplicationDbContext _db;

    public GetAdminDashboardOverviewQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AdminDashboardOverviewDto> Handle(GetAdminDashboardOverviewQuery request, CancellationToken cancellationToken)
    {
        var days = request.Days is > 0 and <= 90 ? request.Days : 30;
        var now = DateTimeOffset.UtcNow;
        var todayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var sevenDaysAgo = now.AddDays(-7);
        var thirtyDaysAgo = now.AddDays(-30);
        var trendStartDate = todayStart.AddDays(-days);

        // 1. CHỈ SỐ TÀI CHÍNH (Financial KPIs)
        var totalGmv = await _db.Commissions
            .Where(c => !c.IsDeleted && c.Status != CommissionStatus.PendingAcceptance)
            .SumAsync(c => (decimal?)c.FinalPrice, cancellationToken) ?? 0m;

        var activeEscrowLocked = await _db.Commissions
            .Where(c => !c.IsDeleted && (c.Status == CommissionStatus.InProgress || c.Status == CommissionStatus.Disputed))
            .SumAsync(c => (decimal?)c.EscrowHeldAmount, cancellationToken) ?? 0m;

        var netPlatformRevenue = await _db.WalletTransactions
            .Where(t => !t.IsDeleted && t.Type == WalletTransactionType.PlatformFee)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        if (netPlatformRevenue == 0m)
        {
            // Dự toán nếu sàn thu 10% trên các đơn hoàn thành
            var completedDisbursed = await _db.Commissions
                .Where(c => !c.IsDeleted && c.Status == CommissionStatus.Completed)
                .SumAsync(c => (decimal?)c.DisbursedAmount, cancellationToken) ?? 0m;
            netPlatformRevenue = Math.Round(completedDisbursed * 0.10m, 2);
        }

        var totalPlatformBalance = await _db.Wallets
            .Where(w => !w.IsDeleted)
            .SumAsync(w => (decimal?)(w.Balance + w.LockedBalance), cancellationToken) ?? 0m;

        var totalPayoutsProcessed = await _db.PayoutRequests
            .Where(p => !p.IsDeleted && p.Status == PayoutStatus.Processed)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var financialKpis = new AdminFinancialKpiDto
        {
            TotalGmv = totalGmv,
            ActiveEscrowLocked = activeEscrowLocked,
            NetPlatformRevenue = netPlatformRevenue,
            TotalPlatformBalance = totalPlatformBalance,
            TotalPayoutsProcessed = totalPayoutsProcessed
        };

        // 2. CHỈ SỐ NGƯỜI DÙNG (User Metrics)
        var totalUsers = await _db.Users
            .CountAsync(u => !u.IsDeleted, cancellationToken);

        var newUsersToday = await _db.Users
            .CountAsync(u => !u.IsDeleted && u.CreatedAt >= todayStart, cancellationToken);

        var newUsersLast7Days = await _db.Users
            .CountAsync(u => !u.IsDeleted && u.CreatedAt >= sevenDaysAgo, cancellationToken);

        var newUsersLast30Days = await _db.Users
            .CountAsync(u => !u.IsDeleted && u.CreatedAt >= thirtyDaysAgo, cancellationToken);

        var clientRoleId = await _db.Set<IdentityRole<Guid>>()
            .Where(r => r.Name == UserRoleNames.Client)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var creatorRoleId = await _db.Set<IdentityRole<Guid>>()
            .Where(r => r.Name == UserRoleNames.Creator)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var modRoleId = await _db.Set<IdentityRole<Guid>>()
            .Where(r => r.Name == UserRoleNames.Moderator)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var totalClients = clientRoleId != Guid.Empty
            ? await _db.Set<IdentityUserRole<Guid>>().CountAsync(ur => ur.RoleId == clientRoleId, cancellationToken)
            : 0;

        var totalCreators = creatorRoleId != Guid.Empty
            ? await _db.Set<IdentityUserRole<Guid>>().CountAsync(ur => ur.RoleId == creatorRoleId, cancellationToken)
            : 0;

        var totalModerators = modRoleId != Guid.Empty
            ? await _db.Set<IdentityUserRole<Guid>>().CountAsync(ur => ur.RoleId == modRoleId, cancellationToken)
            : 0;

        var userMetrics = new AdminUserMetricsDto
        {
            TotalUsers = totalUsers,
            NewUsersToday = newUsersToday,
            NewUsersLast7Days = newUsersLast7Days,
            NewUsersLast30Days = newUsersLast30Days,
            TotalClients = totalClients,
            TotalCreators = totalCreators,
            TotalModerators = totalModerators
        };

        // 3. CHỈ SỐ ĐƠN COMMISSION (Commission Metrics)
        var commissionStatusCounts = await _db.Commissions
            .Where(c => !c.IsDeleted)
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var pendingAcceptanceCount = commissionStatusCounts.FirstOrDefault(x => x.Status == CommissionStatus.PendingAcceptance)?.Count ?? 0;
        var inProgressCount = commissionStatusCounts.FirstOrDefault(x => x.Status == CommissionStatus.InProgress)?.Count ?? 0;
        var completedCount = commissionStatusCounts.FirstOrDefault(x => x.Status == CommissionStatus.Completed)?.Count ?? 0;
        var cancelledCount = commissionStatusCounts.FirstOrDefault(x => x.Status == CommissionStatus.Cancelled)?.Count ?? 0;
        var disputedCount = commissionStatusCounts.FirstOrDefault(x => x.Status == CommissionStatus.Disputed)?.Count ?? 0;
        var totalCommissions = commissionStatusCounts.Sum(x => x.Count);

        var commissionMetrics = new AdminCommissionMetricsDto
        {
            TotalCommissions = totalCommissions,
            PendingAcceptanceCount = pendingAcceptanceCount,
            InProgressCount = inProgressCount,
            CompletedCount = completedCount,
            CancelledCount = cancelledCount,
            DisputedCount = disputedCount
        };

        // 4. HÀNG ĐỢI XỬ LÝ (Action Required Queue)
        var pendingDisputesCount = await _db.Disputes
            .CountAsync(d => !d.IsDeleted && d.Status == "Pending", cancellationToken);

        var pendingCreatorApplicationsCount = await _db.CreatorApplications
            .CountAsync(a => !a.IsDeleted && a.Status == ApplicationStatus.Pending, cancellationToken);

        var pendingPayoutsCount = await _db.PayoutRequests
            .CountAsync(p => !p.IsDeleted && p.Status == PayoutStatus.Pending, cancellationToken);

        var flaggedArtworksCount = await _db.Artworks
            .CountAsync(a => !a.IsDeleted && (a.ModerationStatus == "Flagged" || a.ModerationStatus == "Pending"), cancellationToken);

        var actionRequiredQueue = new AdminActionRequiredQueueDto
        {
            PendingDisputesCount = pendingDisputesCount,
            PendingCreatorApplicationsCount = pendingCreatorApplicationsCount,
            PendingPayoutsCount = pendingPayoutsCount,
            FlaggedArtworksCount = flaggedArtworksCount
        };

        // 5. BIỂU ĐỒ DÒNG TIỀN THEO NGÀY (Revenue Trend)
        var recentCommissions = await _db.Commissions
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.CreatedAt >= trendStartDate)
            .Select(c => new { c.CreatedAt, c.FinalPrice, c.EscrowHeldAmount, c.Status })
            .ToListAsync(cancellationToken);

        var trendList = new List<AdminDailyRevenueDto>();
        for (int i = 0; i <= days; i++)
        {
            var date = trendStartDate.AddDays(i).Date;
            var nextDate = date.AddDays(1);

            var dayComms = recentCommissions
                .Where(c => c.CreatedAt.Date == date)
                .ToList();

            var dayGmv = dayComms
                .Where(c => c.Status != CommissionStatus.PendingAcceptance)
                .Sum(c => c.FinalPrice);

            var dayEscrow = dayComms.Sum(c => c.EscrowHeldAmount);
            var dayRevenue = Math.Round(dayGmv * 0.10m, 2);

            trendList.Add(new AdminDailyRevenueDto
            {
                Date = date.ToString("yyyy-MM-dd"),
                Gmv = dayGmv,
                PlatformRevenue = dayRevenue,
                EscrowDeposited = dayEscrow
            });
        }

        // 6. GIAO DỊCH GẦN NHẤT (Recent Transactions)
        var rawTransactions = await _db.WalletTransactions
            .AsNoTracking()
            .Where(t => !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        var recentTxList = new List<AdminRecentTransactionDto>();
        if (rawTransactions.Count > 0)
        {
            var walletIds = rawTransactions.Select(t => t.WalletId).Distinct().ToList();
            var wallets = await _db.Wallets
                .AsNoTracking()
                .Where(w => walletIds.Contains(w.Id))
                .ToDictionaryAsync(w => w.Id, w => w.UserId, cancellationToken);

            var userIds = wallets.Values.Distinct().ToList();
            var users = await _db.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u, cancellationToken);

            recentTxList = rawTransactions.Select(t =>
            {
                wallets.TryGetValue(t.WalletId, out var userId);
                users.TryGetValue(userId, out var user);

                return new AdminRecentTransactionDto
                {
                    Id = t.Id,
                    WalletId = t.WalletId,
                    UserName = user?.FullName ?? user?.UserName ?? "User",
                    UserEmail = user?.Email ?? string.Empty,
                    Type = t.Type.ToString(),
                    Direction = t.Direction.ToString(),
                    Amount = t.Amount,
                    BalanceAfter = t.BalanceAfter,
                    Note = t.Note,
                    CreatedAt = t.CreatedAt
                };
            }).ToList();
        }

        return new AdminDashboardOverviewDto
        {
            FinancialKpis = financialKpis,
            UserMetrics = userMetrics,
            CommissionMetrics = commissionMetrics,
            ActionRequiredQueue = actionRequiredQueue,
            RevenueTrend = trendList,
            RecentTransactions = recentTxList
        };
    }
}
