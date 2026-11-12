using ArtCommission.Application.ArtTrade.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Trade;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtTrade.Common;

public interface ITradeSlotService
{
    Task<(bool CanTrade, int AvailableSlots, string? Reason)> CheckTradeSlotAvailableAsync(Guid userId, CancellationToken ct = default);
    Task<TradeSlotStatusDto> GetUserSlotStatusAsync(Guid userId, CancellationToken ct = default);
    Task FreezeSlotAsync(Guid userId, Guid tradeId, int days, string reason, CancellationToken ct = default);
}

public class TradeSlotService : ITradeSlotService
{
    public const int DefaultMaxSlots = 2;
    private readonly IApplicationDbContext _db;

    public TradeSlotService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool CanTrade, int AvailableSlots, string? Reason)> CheckTradeSlotAvailableAsync(Guid userId, CancellationToken ct = default)
    {
        var status = await GetUserSlotStatusAsync(userId, ct);
        if (status.AvailableSlots <= 0)
        {
            var reason = status.FrozenSlotsCount > 0
                ? $"Bạn không còn Slot khả dụng ({status.ActiveTradesCount} kèo đang thực hiện, {status.FrozenSlotsCount} slot đang bị đóng băng do vi phạm)."
                : $"Bạn đã sử dụng tối đa {DefaultMaxSlots} Slot giao dịch đồng thời.";
            return (false, 0, reason);
        }

        return (true, status.AvailableSlots, null);
    }

    public async Task<TradeSlotStatusDto> GetUserSlotStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        try
        {
            // 1. Đếm số kèo đang trong giai đoạn thực hiện (bận)
            var activeTradesCount = await _db.ArtTrades
                .AsNoTracking()
                .CountAsync(t => !t.IsDeleted &&
                    (t.InitiatorId == userId || t.ReceiverId == userId) &&
                    (t.Status == TradeStatus.Accepted || t.Status == TradeStatus.BothSubmitted), ct);

            // 2. Lấy danh sách các slot đang bị đóng băng còn hiệu lực
            var activeFreezes = await _db.TradeSlotFreezes
                .AsNoTracking()
                .Where(f => !f.IsDeleted && !f.IsRevoked && f.UserId == userId && f.FrozenUntil > now)
                .OrderBy(f => f.FrozenUntil)
                .ToListAsync(ct);

            var frozenCount = activeFreezes.Count;
            var availableSlots = Math.Max(0, DefaultMaxSlots - (activeTradesCount + frozenCount));

            var frozenDetails = activeFreezes.Select(f => new FrozenSlotDetailDto(
                FreezeId: f.Id,
                ArtTradeId: f.ArtTradeId,
                FrozenFrom: f.FrozenFrom,
                FrozenUntil: f.FrozenUntil,
                Reason: f.Reason,
                DaysRemaining: Math.Max(1, (int)Math.Ceiling((f.FrozenUntil - now).TotalDays))
            )).ToList();

            return new TradeSlotStatusDto(
                UserId: userId,
                TotalSlots: DefaultMaxSlots,
                ActiveTradesCount: activeTradesCount,
                FrozenSlotsCount: frozenCount,
                AvailableSlots: availableSlots,
                CanTrade: availableSlots > 0,
                FrozenDetails: frozenDetails
            );
        }
        catch
        {
            // Fallback an toàn khi database đang migrate hoặc bảng chưa khởi tạo
            return new TradeSlotStatusDto(
                UserId: userId,
                TotalSlots: DefaultMaxSlots,
                ActiveTradesCount: 0,
                FrozenSlotsCount: 0,
                AvailableSlots: DefaultMaxSlots,
                CanTrade: true,
                FrozenDetails: new List<FrozenSlotDetailDto>()
            );
        }
    }

    public async Task FreezeSlotAsync(Guid userId, Guid tradeId, int days, string reason, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var freeze = new TradeSlotFreeze
        {
            UserId = userId,
            ArtTradeId = tradeId,
            FrozenFrom = now,
            FrozenUntil = now.AddDays(days),
            Reason = reason
        };

        _db.TradeSlotFreezes.Add(freeze);
        await _db.SaveChangesAsync(ct);
    }
}

