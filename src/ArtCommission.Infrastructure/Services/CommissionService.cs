using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.Commission.DTOs;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Commission.Validators;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.Commission;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ArtCommission.Infrastructure.Services;

public class CommissionService : ICommissionService
{
    private readonly AppDbContext _dbContext;
    private readonly IWalletService _walletService;

    public CommissionService(AppDbContext dbContext, IWalletService walletService)
    {
        _dbContext = dbContext;
        _walletService = walletService;
    }

    public async Task<CommissionDto> CreateCommissionAsync(CreateCommissionRequest request, Guid clientId, CancellationToken cancellationToken = default)
    {
        RequireUser(clientId);
        var validation = new CreateCommissionRequestValidator().Validate(request);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        var creator = await _dbContext.CreatorProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.CreatorId && !p.IsDeleted, cancellationToken);
        if (creator is null || !creator.IsAcceptingOrders)
            throw new ArgumentException("Creator hiện không nhận đơn.");

        var packages = CreatorRateCard.Read(creator.RateCardJson);
        CreatorRateCard.Write(packages);
        var selectedPackage = packages.FirstOrDefault(package => package.Id == request.PackageId);
        if (selectedPackage is null)
            throw new ArgumentException("Gói giá đã thay đổi hoặc không còn khả dụng. Vui lòng chọn lại.");

        var totalPrice = selectedPackage.Price;
        var discountAmount = 0.00m;
        var finalPrice = totalPrice - discountAmount;

        var commission = new Commission
        {
            Title = request.Title,
            Description = request.Description,
            ClientId = clientId,
            CreatorId = request.CreatorId,
            TotalPrice = totalPrice,
            DiscountAmount = discountAmount,
            FinalPrice = finalPrice,
            EscrowHeldAmount = 0.00m,
            DisbursedAmount = 0.00m,
            EscrowStatus = EscrowStatus.Pending,
            CurrentStage = 1,
            Status = CommissionStatus.PendingAcceptance,
            DeadlineAt = request.DeadlineAt
        };

        foreach (var milestone in selectedPackage.Milestones)
        {
            commission.Milestones.Add(new Milestone
            {
                Sequence = milestone.Sequence,
                Title = milestone.Title,
                Price = milestone.Price,
                Status = MilestoneStatus.Pending
            });
        }

        _dbContext.Commissions.Add(commission);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(commission);
    }

    public async Task<(List<CommissionDto> Items, int TotalItems)> GetCommissionsAsync(string? status, string? role, Guid userId, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        RequireUser(userId);
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentException("Invalid pagination.");
        var creatorProfileId = await CreatorProfileIdAsync(userId, cancellationToken);
        var query = _dbContext.Commissions.AsNoTracking().Where(c => !c.IsDeleted);

        if (string.Equals(role, "Client", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.ClientId == userId);
        }
        else if (string.Equals(role, "Creator", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.CreatorId == creatorProfileId);
        }
        else if (string.IsNullOrWhiteSpace(role))
            query = query.Where(c => c.ClientId == userId || c.CreatorId == creatorProfileId);
        else throw new ArgumentException("Invalid role filter.");

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CommissionStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(c => c.Status == parsedStatus);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => MapToDto(c))
            .ToListAsync(cancellationToken);

        return (items, totalItems);
    }

    public async Task<CommissionDetailDto?> GetCommissionByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        RequireUser(userId);
        var creatorProfileId = await CreatorProfileIdAsync(userId, cancellationToken);
        var commission = await _dbContext.Commissions
            .Include(c => c.Milestones)
            .Include(c => c.Reviews)
            .Include(c => c.Disputes)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted && (c.ClientId == userId || c.CreatorId == creatorProfileId), cancellationToken);

        if (commission == null) return null;

        var detailDto = MapToDetailDto(commission);
        return detailDto;
    }

    public async Task<CommissionDto> RespondCommissionAsync(Guid id, RespondCommissionRequest request, Guid creatorId, CancellationToken cancellationToken = default)
    {
        var commission = await OwnedCommissionAsync(id, creatorId, creator: true, cancellationToken);
        var validation = new RespondCommissionRequestValidator().Validate(request);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        if (commission.Status != CommissionStatus.PendingAcceptance)
            throw new InvalidOperationException("Commission cannot be changed in this state.");

        if (string.Equals(request.Action, "Accept", StringComparison.OrdinalIgnoreCase))
        {
            commission.Status = CommissionStatus.InProgress;
        }
        else if (string.Equals(request.Action, "Reject", StringComparison.OrdinalIgnoreCase))
        {
            commission.Status = CommissionStatus.Cancelled;
        }
        else if (string.Equals(request.Action, "Negotiate", StringComparison.OrdinalIgnoreCase))
        {
            commission.Status = CommissionStatus.Negotiating;
            if (request.NegotiatePrice.HasValue)
            {
                if (request.NegotiatePrice.Value <= 0) throw new ArgumentException("Negotiated price must be positive.");
                var milestones = await _dbContext.Milestones.Where(m => m.CommissionId == id).OrderBy(m => m.Sequence).ToListAsync(cancellationToken);
                if (milestones.Count == 0 || milestones[^1].Price + request.NegotiatePrice.Value - commission.TotalPrice <= 0)
                    throw new ArgumentException("Negotiated price cannot produce an invalid milestone.");
                milestones[^1].Price += request.NegotiatePrice.Value - commission.TotalPrice;
                commission.TotalPrice = request.NegotiatePrice.Value;
                commission.FinalPrice = request.NegotiatePrice.Value - commission.DiscountAmount;
            }
        }

        commission.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(commission);
    }

    public async Task<CommissionDto> RespondToCounterofferAsync(Guid id, bool accept, Guid clientId, CancellationToken cancellationToken = default)
    {
        var commission = await OwnedCommissionAsync(id, clientId, creator: false, cancellationToken);
        if (commission.Status != CommissionStatus.Negotiating)
            throw new InvalidOperationException("Commission has no pending counter-offer.");

        commission.Status = accept ? CommissionStatus.InProgress : CommissionStatus.Cancelled;
        commission.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(commission);
    }

    public async Task<CommissionDto> DepositEscrowAsync(Guid id, Guid clientId, string paymentMethod, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(paymentMethod, "Wallet", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only wallet escrow is supported.");
        await using var tx = await BeginTransactionAsync(cancellationToken);
        var commission = await OwnedCommissionAsync(id, clientId, creator: false, cancellationToken);
        if (commission.Status != CommissionStatus.InProgress || commission.EscrowStatus != EscrowStatus.Pending)
            throw new InvalidOperationException("Commission is not ready for escrow deposit.");
        var wallet = await _walletService.GetOrCreateWalletAsync(clientId, cancellationToken);
        if (wallet.Status != WalletStatus.Active) throw new InvalidOperationException("Wallet is inactive.");
        await _walletService.HoldFundsAsync(wallet, WalletTransactionType.EscrowHold, commission.FinalPrice,
            nameof(Commission), commission.Id, "Commission escrow deposit", cancellationToken);

        commission.EscrowHeldAmount = commission.FinalPrice;
        commission.EscrowStatus = EscrowStatus.Deposited;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);

        return MapToDto(commission);
    }

    public async Task<MilestoneDto> SubmitMilestoneWipAsync(Guid commissionId, Guid milestoneId, SubmitMilestoneRequest request, Guid creatorId, CancellationToken cancellationToken = default)
    {
        var commission = await OwnedCommissionAsync(commissionId, creatorId, creator: true, cancellationToken);
        if (commission.Status != CommissionStatus.InProgress || commission.EscrowStatus is not (EscrowStatus.Deposited or EscrowStatus.PartialReleased))
            throw new InvalidOperationException("Commission is not funded and in progress.");
        if (string.IsNullOrWhiteSpace(request.WipFileUrl)) throw new ArgumentException("WIP file URL is required.");
        var milestone = await _dbContext.Milestones.FirstOrDefaultAsync(m => m.Id == milestoneId && m.CommissionId == commissionId, cancellationToken);
        if (milestone == null) throw new KeyNotFoundException("Không tìm thấy cột mốc milestone.");
        if (milestone.Sequence != commission.CurrentStage || milestone.Status is not (MilestoneStatus.Pending or MilestoneStatus.RevisionRequested))
            throw new InvalidOperationException("Milestone cannot be submitted in this state.");

        milestone.WipPreviewUrl = request.WipFileUrl;
        milestone.WatermarkedUrl = request.WipFileUrl; // Watermark applied
        milestone.Status = MilestoneStatus.Submitted;
        milestone.SubmittedAt = DateTimeOffset.UtcNow;
        milestone.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToMilestoneDto(milestone);
    }

    public async Task<MilestoneDto> ApproveMilestoneAsync(Guid commissionId, Guid milestoneId, Guid clientId, CancellationToken cancellationToken = default)
    {
        await using var tx = await BeginTransactionAsync(cancellationToken);
        var commission = await OwnedCommissionAsync(commissionId, clientId, creator: false, cancellationToken);
        await _dbContext.Entry(commission).Collection(c => c.Milestones).LoadAsync(cancellationToken);
        if (commission.Status != CommissionStatus.InProgress || commission.EscrowStatus is not (EscrowStatus.Deposited or EscrowStatus.PartialReleased))
            throw new InvalidOperationException("Commission is not ready for milestone approval.");

        var milestone = commission.Milestones.FirstOrDefault(m => m.Id == milestoneId);
        if (milestone == null) throw new KeyNotFoundException("Không tìm thấy cột mốc milestone.");
        if (milestone.Status != MilestoneStatus.Submitted || milestone.Sequence != commission.CurrentStage || milestone.Price > commission.EscrowHeldAmount)
            throw new InvalidOperationException("Milestone cannot be approved in this state.");
        var clientWallet = await _walletService.GetOrCreateWalletAsync(clientId, cancellationToken);
        var creatorUserId = await _dbContext.CreatorProfiles.Where(p => p.Id == commission.CreatorId)
            .Select(p => p.UserId).SingleAsync(cancellationToken);
        var creatorWallet = await _walletService.GetOrCreateWalletAsync(creatorUserId, cancellationToken);
        await _walletService.ReleaseFundsAsync(clientWallet, WalletTransactionType.EscrowRelease, milestone.Price,
            nameof(Commission), commission.Id, "Commission milestone release", cancellationToken);
        await _walletService.CreditAsync(creatorWallet, WalletTransactionType.CommissionEarning, milestone.Price,
            nameof(Commission), commission.Id, "Commission milestone earning", cancellationToken);

        milestone.Status = MilestoneStatus.Approved;
        milestone.ApprovedAt = DateTimeOffset.UtcNow;
        milestone.UpdatedAt = DateTimeOffset.UtcNow;

        commission.DisbursedAmount += milestone.Price;
        commission.EscrowHeldAmount = Math.Max(0, commission.EscrowHeldAmount - milestone.Price);
        commission.CurrentStage += 1;
        commission.EscrowStatus = commission.EscrowHeldAmount == 0 ? EscrowStatus.Released : EscrowStatus.PartialReleased;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);

        return MapToMilestoneDto(milestone);
    }

    public async Task<MilestoneDto> RequestMilestoneRevisionAsync(Guid commissionId, Guid milestoneId, RequestRevisionRequest request, Guid clientId, CancellationToken cancellationToken = default)
    {
        var commission = await OwnedCommissionAsync(commissionId, clientId, creator: false, cancellationToken);
        if (commission.Status != CommissionStatus.InProgress) throw new InvalidOperationException("Commission is not in progress.");
        var milestone = await _dbContext.Milestones.FirstOrDefaultAsync(m => m.Id == milestoneId && m.CommissionId == commissionId, cancellationToken);
        if (milestone == null) throw new KeyNotFoundException("Không tìm thấy cột mốc milestone.");
        if (milestone.Status != MilestoneStatus.Submitted || milestone.Sequence != commission.CurrentStage)
            throw new InvalidOperationException("Milestone cannot be revised in this state.");

        milestone.Status = MilestoneStatus.RevisionRequested;
        milestone.RevisionCount += 1;
        milestone.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToMilestoneDto(milestone);
    }

    public async Task<CommissionDto> DeliverFinalWorkAsync(Guid id, string finalDeliverableUrl, Guid creatorId, CancellationToken cancellationToken = default)
    {
        var commission = await OwnedCommissionAsync(id, creatorId, creator: true, cancellationToken);
        await _dbContext.Entry(commission).Collection(c => c.Milestones).LoadAsync(cancellationToken);
        if (commission.Status != CommissionStatus.InProgress || commission.Milestones.Count == 0
            || commission.Milestones.Any(m => m.Status != MilestoneStatus.Approved))
            throw new InvalidOperationException("All milestones must be approved before final delivery.");
        if (string.IsNullOrWhiteSpace(finalDeliverableUrl)) throw new ArgumentException("Final deliverable URL is required.");

        commission.Status = CommissionStatus.SubmittedFinal;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        var lastMilestone = commission.Milestones.OrderByDescending(m => m.Sequence).FirstOrDefault();
        if (lastMilestone != null)
        {
            lastMilestone.FinalDeliverableUrl = finalDeliverableUrl;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(commission);
    }

    public async Task<(CommissionDto Commission, string DownloadPresignedUrl)> CompleteCommissionAsync(Guid id, Guid clientId, CancellationToken cancellationToken = default)
    {
        var commission = await OwnedCommissionAsync(id, clientId, creator: false, cancellationToken);
        await _dbContext.Entry(commission).Collection(c => c.Milestones).LoadAsync(cancellationToken);
        if (commission.Status != CommissionStatus.SubmittedFinal || commission.EscrowHeldAmount != 0
            || commission.Milestones.Any(m => m.Status != MilestoneStatus.Approved))
            throw new InvalidOperationException("Commission is not ready for completion.");

        commission.Status = CommissionStatus.Completed;
        commission.EscrowStatus = EscrowStatus.Released;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var presignedUrl = commission.Milestones.OrderByDescending(m => m.Sequence).FirstOrDefault()?.FinalDeliverableUrl ?? string.Empty;

        return (MapToDto(commission), presignedUrl);
    }

    public async Task<CommissionDto> CancelCommissionAsync(Guid id, string reason, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var tx = await BeginTransactionAsync(cancellationToken);
        var commission = await PartyCommissionAsync(id, userId, cancellationToken);
        if (commission.Status is CommissionStatus.Completed or CommissionStatus.Cancelled or CommissionStatus.Disputed)
            throw new InvalidOperationException("Commission cannot be cancelled in this state.");
        if (commission.DisbursedAmount > 0) throw new InvalidOperationException("Commission with released milestones requires manual dispute resolution.");
        if (commission.EscrowHeldAmount > 0)
        {
            var wallet = await _walletService.GetOrCreateWalletAsync(commission.ClientId, cancellationToken);
            await _walletService.RefundHeldFundsAsync(wallet, WalletTransactionType.RefundFromHold,
                commission.EscrowHeldAmount, nameof(Commission), commission.Id, reason, cancellationToken);
            commission.EscrowHeldAmount = 0;
            commission.EscrowStatus = EscrowStatus.Refunded;
        }

        commission.Status = CommissionStatus.Cancelled;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);

        return MapToDto(commission);
    }

    public async Task<DisputeDto> CreateDisputeAsync(Guid id, CreateDisputeRequest request, Guid raisedById, CancellationToken cancellationToken = default)
    {
        var commission = await PartyCommissionAsync(id, raisedById, cancellationToken);
        if (commission.Status is CommissionStatus.Completed or CommissionStatus.Cancelled or CommissionStatus.Disputed)
            throw new InvalidOperationException("Commission cannot be disputed in this state.");
        if (await _dbContext.Disputes.AnyAsync(d => d.CommissionId == id, cancellationToken))
            throw new InvalidOperationException("Commission already has a dispute.");
        var dispute = new Dispute
        {
            CommissionId = id,
            RaisedById = raisedById,
            Reason = request.Reason,
            EvidenceUrls = request.EvidenceUrls != null ? string.Join(",", request.EvidenceUrls) : null,
            Status = "Pending"
        };

        _dbContext.Disputes.Add(dispute);

        commission.Status = CommissionStatus.Disputed;
        commission.EscrowStatus = EscrowStatus.Disputed;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDisputeDto(dispute);
    }

    public async Task<DisputeDto?> GetDisputeByCommissionIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        await PartyCommissionAsync(id, userId, cancellationToken);
        var dispute = await _dbContext.Disputes.AsNoTracking().FirstOrDefaultAsync(d => d.CommissionId == id, cancellationToken);
        return dispute == null ? null : MapToDisputeDto(dispute);
    }

    public async Task<ReviewDto> CreateReviewAsync(Guid id, CreateReviewRequest request, Guid reviewerId, CancellationToken cancellationToken = default)
    {
        var commission = await OwnedCommissionAsync(id, reviewerId, creator: false, cancellationToken);
        if (commission.Status != CommissionStatus.Completed) throw new InvalidOperationException("Review requires a completed commission.");
        if (await _dbContext.Reviews.AnyAsync(r => r.CommissionId == id, cancellationToken))
            throw new InvalidOperationException("Commission already has a review.");
        var validation = new CreateReviewRequestValidator().Validate(request);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        var review = new Review
        {
            CommissionId = id,
            ReviewerId = reviewerId,
            Rating = request.Rating,
            Comment = request.Comment
        };

        _dbContext.Reviews.Add(review);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToReviewDto(review);
    }

    public async Task<ReviewDto> ReplyReviewAsync(Guid id, string replyComment, Guid creatorId, CancellationToken cancellationToken = default)
    {
        await OwnedCommissionAsync(id, creatorId, creator: true, cancellationToken);
        var review = await _dbContext.Reviews.FirstOrDefaultAsync(r => r.CommissionId == id, cancellationToken);
        if (review == null) throw new KeyNotFoundException("Không tìm thấy đánh giá cho đơn hàng này.");
        if (review.ReviewerReply is not null) throw new InvalidOperationException("Review already has a reply.");

        review.ReviewerReply = replyComment;
        review.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToReviewDto(review);
    }

    private static void RequireUser(Guid userId)
    {
        if (userId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
    }

    private Task<Guid> CreatorProfileIdAsync(Guid userId, CancellationToken cancellationToken) =>
        _dbContext.CreatorProfiles.Where(p => p.UserId == userId && !p.IsDeleted)
            .Select(p => p.Id).FirstOrDefaultAsync(cancellationToken);

    private async Task<Commission> PartyCommissionAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        RequireUser(userId);
        var creatorProfileId = await CreatorProfileIdAsync(userId, cancellationToken);
        return await _dbContext.Commissions.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted
            && (c.ClientId == userId || c.CreatorId == creatorProfileId), cancellationToken)
            ?? throw new KeyNotFoundException("Commission not found.");
    }

    private async Task<Commission> OwnedCommissionAsync(Guid id, Guid userId, bool creator, CancellationToken cancellationToken)
    {
        var commission = await PartyCommissionAsync(id, userId, cancellationToken);
        var creatorProfileId = creator ? await CreatorProfileIdAsync(userId, cancellationToken) : Guid.Empty;
        if (creator ? commission.CreatorId != creatorProfileId : commission.ClientId != userId)
            throw new UnauthorizedAccessException("This commission belongs to another user.");
        return commission;
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

    #region Helper Mappers
    private static CommissionDto MapToDto(Commission c)
    {
        return new CommissionDto
        {
            Id = c.Id,
            Title = c.Title,
            Description = c.Description,
            ClientId = c.ClientId,
            CreatorId = c.CreatorId,
            VoucherId = c.VoucherId,
            DiscountAmount = c.DiscountAmount,
            TotalPrice = c.TotalPrice,
            FinalPrice = c.FinalPrice,
            EscrowHeldAmount = c.EscrowHeldAmount,
            DisbursedAmount = c.DisbursedAmount,
            EscrowStatus = c.EscrowStatus.ToString(),
            CurrentStage = c.CurrentStage,
            Status = c.Status.ToString(),
            DeadlineAt = c.DeadlineAt,
            CreatedAt = c.CreatedAt
        };
    }

    private static CommissionDetailDto MapToDetailDto(Commission c)
    {
        var dto = MapToDto(c);
        return new CommissionDetailDto
        {
            Id = dto.Id,
            Title = dto.Title,
            Description = dto.Description,
            ClientId = dto.ClientId,
            CreatorId = dto.CreatorId,
            VoucherId = dto.VoucherId,
            DiscountAmount = dto.DiscountAmount,
            TotalPrice = dto.TotalPrice,
            FinalPrice = dto.FinalPrice,
            EscrowHeldAmount = dto.EscrowHeldAmount,
            DisbursedAmount = dto.DisbursedAmount,
            EscrowStatus = dto.EscrowStatus,
            CurrentStage = dto.CurrentStage,
            Status = dto.Status,
            DeadlineAt = dto.DeadlineAt,
            CreatedAt = dto.CreatedAt,
            Milestones = c.Milestones.Select(MapToMilestoneDto).ToList(),
            Review = c.Reviews.FirstOrDefault() != null ? MapToReviewDto(c.Reviews.First()) : null,
            Dispute = c.Disputes.FirstOrDefault() != null ? MapToDisputeDto(c.Disputes.First()) : null
        };
    }

    private static MilestoneDto MapToMilestoneDto(Milestone m)
    {
        return new MilestoneDto
        {
            Id = m.Id,
            CommissionId = m.CommissionId,
            Sequence = m.Sequence,
            Title = m.Title,
            Price = m.Price,
            Status = m.Status.ToString(),
            WipPreviewUrl = m.WipPreviewUrl,
            WatermarkedUrl = m.WatermarkedUrl,
            FinalDeliverableUrl = m.FinalDeliverableUrl,
            RevisionCount = m.RevisionCount,
            SubmittedAt = m.SubmittedAt,
            ApprovedAt = m.ApprovedAt
        };
    }

    private static ReviewDto MapToReviewDto(Review r)
    {
        return new ReviewDto
        {
            Id = r.Id,
            CommissionId = r.CommissionId,
            ReviewerId = r.ReviewerId,
            Rating = r.Rating,
            Comment = r.Comment,
            ReviewerReply = r.ReviewerReply,
            CreatedAt = r.CreatedAt
        };
    }

    private static DisputeDto MapToDisputeDto(Dispute d)
    {
        return new DisputeDto
        {
            Id = d.Id,
            CommissionId = d.CommissionId,
            RaisedById = d.RaisedById,
            Reason = d.Reason,
            EvidenceUrls = d.EvidenceUrls,
            Status = d.Status,
            Resolution = d.Resolution,
            ClientRefundAmount = d.ClientRefundAmount,
            ArtistPayAmount = d.ArtistPayAmount,
            AdminNote = d.AdminNote,
            ResolvedAt = d.ResolvedAt,
            CreatedAt = d.CreatedAt
        };
    }
    #endregion
}
