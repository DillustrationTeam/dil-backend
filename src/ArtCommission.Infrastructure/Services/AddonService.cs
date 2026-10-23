using System.Data;
using System.Text.Json;
using ArtCommission.Application.Commission.DTOs;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.Commission;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Infrastructure.Services;

public class AddonService(AppDbContext db, IWalletService wallets) : IAddonService
{
    public async Task<List<CreatorAddon>> ListCreatorAsync(Guid id, CancellationToken ct)
    {
        var profile = await db.CreatorProfiles.AsNoTracking().FirstOrDefaultAsync(x => (x.Id == id || x.UserId == id) && !x.IsDeleted, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy Creator.");
        return await db.Set<CreatorAddon>().AsNoTracking().Where(x => x.CreatorId == profile.Id && !x.IsDeleted).OrderBy(x => x.CreatedAt).ToListAsync(ct);
    }

    public async Task<CreatorAddon> SaveAsync(SaveCreatorAddonRequest r, Guid userId, CancellationToken ct)
    {
        var profile = await db.CreatorProfiles.FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, ct)
            ?? throw new UnauthorizedAccessException("Cần hồ sơ Creator.");
        var preset = r.PresetKey is null ? null : AddonRules.Presets.FirstOrDefault(x => x.Key == r.PresetKey)
            ?? throw new ArgumentException("Preset không hợp lệ.");
        var title = preset?.Title ?? r.Title;
        var stage = AddonRules.Normalize(preset?.AllowedMilestone ?? r.AllowedMilestone);
        AddonRules.Validate(title, r.Price, stage, r.ExtendDays, r.Description);
        var addon = r.Id is null ? new CreatorAddon { CreatorId = profile.Id } :
            await db.Set<CreatorAddon>().SingleOrDefaultAsync(x => x.Id == r.Id && x.CreatorId == profile.Id && !x.IsDeleted, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy phụ phí.");
        if (r.PackageId is not null)
        {
            var packages = JsonSerializer.Deserialize<List<RateCardPackageDto>>(profile.RateCardJson ?? "[]",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new RateCardIdConverter() } });
            // An owned offer may retain its original scope after that package is removed.
            // Never convert it into a global offer or allow a new invalid package assignment.
            if (packages?.Any(x => x.Id == r.PackageId) != true && !(r.Id is not null && addon.PackageId == r.PackageId)) throw new ArgumentException("Gói không thuộc Creator.");
        }
        addon.Title = title.Trim(); addon.Price = r.Price; addon.Description = r.Description;
        addon.AllowedMilestone = stage; addon.ExtendDays = r.ExtendDays; addon.PresetKey = r.PresetKey;
        addon.PackageId = r.PackageId; addon.IsActive = r.IsActive; addon.UpdatedAt = DateTimeOffset.UtcNow;
        if (r.Id is null) db.Add(addon);
        await db.SaveChangesAsync(ct);
        return addon;
    }

    private async Task<Commission> CommissionAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var c = await db.Commissions.Include(x => x.Milestones).SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy đơn.");
        if (c.ClientId != userId && !await db.CreatorProfiles.AnyAsync(x => x.Id == c.CreatorId && x.UserId == userId && !x.IsDeleted, ct))
            throw new UnauthorizedAccessException("Bạn không thuộc đơn này.");
        return c;
    }

    public async Task<List<CommissionAddon>> ListAsync(Guid id, Guid userId, CancellationToken ct)
    {
        await CommissionAsync(id, userId, ct);
        return await db.Set<CommissionAddon>().AsNoTracking().Where(x => x.CommissionId == id && !x.IsDeleted).OrderBy(x => x.CreatedAt).ToListAsync(ct);
    }

    private static Milestone Current(Commission c, string allowed, int? proposedSequence = null)
    {
        if (c.Status != CommissionStatus.InProgress || c.EscrowHeldAmount <= 0 ||
            c.EscrowStatus is not (EscrowStatus.Deposited or EscrowStatus.PartialReleased))
            throw new InvalidOperationException("Đơn cần đang thực hiện và đã ký quỹ.");
        var m = c.Milestones.SingleOrDefault(x => x.Sequence == c.CurrentStage && !x.IsDeleted && x.Status != MilestoneStatus.Approved)
            ?? throw new InvalidOperationException("Mốc đã kết thúc.");
        if ((proposedSequence is not null && proposedSequence != m.Sequence) ||
            !AddonRules.Allows(allowed, AddonRules.Stage(m.Sequence, c.Milestones.Count(x => !x.IsDeleted))))
            throw new InvalidOperationException("Phụ phí không được phép tại mốc hiện tại. Hãy gửi đề xuất mới phù hợp.");
        return m;
    }

    public async Task<CommissionAddon> ProposeAsync(Guid id, ProposeAddonRequest r, Guid userId, CancellationToken ct)
    {
        var c = await CommissionAsync(id, userId, ct);
        // Only the creator may set ad-hoc prices; clients may request configured options.
        var isCreator = await db.CreatorProfiles.AnyAsync(x => x.Id == c.CreatorId && x.UserId == userId && !x.IsDeleted, ct);
        if (!isCreator && r.CreatorAddonId is null) throw new UnauthorizedAccessException("Chỉ Creator được báo giá phát sinh.");
        CreatorAddon? configured = null;
        if (r.CreatorAddonId is not null)
        {
            configured = await db.Set<CreatorAddon>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.CreatorAddonId && x.CreatorId == c.CreatorId && !x.IsDeleted && x.IsActive && (x.PackageId == null || x.PackageId == c.PackageId), ct)
                ?? throw new ArgumentException("Phụ phí không khả dụng cho đơn này.");
        }
        var a = new CommissionAddon
        {
            CommissionId = id, ProposedById = userId,
            Title = configured?.Title ?? r.Title?.Trim() ?? "", Description = configured?.Description ?? r.Description,
            Price = configured?.Price ?? r.Price, AllowedMilestone = configured?.AllowedMilestone ?? r.AllowedMilestone,
            ExtendDays = configured?.ExtendDays ?? r.ExtendDays, IsPreset = configured?.PresetKey is not null
        };
        AddonRules.Validate(a.Title, a.Price, a.AllowedMilestone, a.ExtendDays, a.Description);
        a.AllowedMilestone = AddonRules.Normalize(a.AllowedMilestone);
        a.MilestoneSequence = Current(c, a.AllowedMilestone).Sequence;
        db.Add(a);
        await db.SaveChangesAsync(ct);
        return a;
    }

    public async Task<CommissionAddon> RespondAsync(Guid id, Guid addonId, RespondAddonRequest r, Guid userId, CancellationToken ct)
    {
        if (r.Action is not ("Accept" or "Reject")) throw new ArgumentException("Action phải là Accept hoặc Reject.");
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : null;
        // Serialize decisions on this order before reading status. UPDLOCK avoids
        // two shared readers deadlocking when both attempt the wallet update.
        if (db.Database.IsSqlServer())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT [Id] FROM [Commissions] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {id}", ct);
        var c = await CommissionAsync(id, userId, ct);
        if (c.ClientId != userId) throw new UnauthorizedAccessException("Chỉ khách hàng được duyệt và ký quỹ.");
        var a = await db.Set<CommissionAddon>().SingleOrDefaultAsync(x => x.Id == addonId && x.CommissionId == id && !x.IsDeleted, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy đề xuất.");
        var target = r.Action == "Accept" ? "Accepted" : "Rejected";
        if (a.Status == target) return a; // Retry never debits the wallet twice.
        if (a.Status != "Proposed") throw new InvalidOperationException("Đề xuất đã được xử lý.");
        if (r.Action == "Accept")
        {
            var milestone = Current(c, a.AllowedMilestone, a.MilestoneSequence);
            if (a.ExtendDays > 0 && c.DeadlineAt is null) throw new InvalidOperationException("Cần đặt deadline trước khi gia hạn.");
            var wallet = await wallets.GetOrCreateWalletAsync(userId, ct);
            if (wallet.Status != WalletStatus.Active) throw new InvalidOperationException("Ví không hoạt động.");
            if (wallet.Balance < a.Price) throw new InvalidOperationException("Số dư ví không đủ để ký quỹ phụ phí.");
            c.FinalPrice += a.Price; c.TotalPrice += a.Price; c.EscrowHeldAmount += a.Price;
            c.DeadlineAt = c.DeadlineAt?.AddDays(a.ExtendDays);
            c.UpdatedAt = DateTimeOffset.UtcNow;
            milestone.Price += a.Price; milestone.UpdatedAt = DateTimeOffset.UtcNow;
            a.Status = target; a.UpdatedAt = DateTimeOffset.UtcNow;
            await wallets.HoldFundsAsync(wallet, WalletTransactionType.EscrowHold, a.Price, nameof(CommissionAddon), a.Id, "Ký quỹ phụ phí: " + a.Title, ct);
        }
        else { a.Status = target; a.UpdatedAt = DateTimeOffset.UtcNow; }
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return a;
    }
}
