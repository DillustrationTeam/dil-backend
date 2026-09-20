using ArtCommission.Application.Admin.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Admin.Queries;

/// <summary>
/// Query lấy nhanh các chỉ số KPIs tài chính chính của sàn (GMV, Escrow locked, Revenue, Platform balance) (SCR-24 / UC32).
/// </summary>
public record GetAdminFinancialKpisQuery : IRequest<AdminFinancialKpiDto>;

public class GetAdminFinancialKpisQueryHandler : IRequestHandler<GetAdminFinancialKpisQuery, AdminFinancialKpiDto>
{
    private readonly IApplicationDbContext _db;

    public GetAdminFinancialKpisQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AdminFinancialKpiDto> Handle(GetAdminFinancialKpisQuery request, CancellationToken cancellationToken)
    {
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

        return new AdminFinancialKpiDto
        {
            TotalGmv = totalGmv,
            ActiveEscrowLocked = activeEscrowLocked,
            NetPlatformRevenue = netPlatformRevenue,
            TotalPlatformBalance = totalPlatformBalance,
            TotalPayoutsProcessed = totalPayoutsProcessed
        };
    }
}
