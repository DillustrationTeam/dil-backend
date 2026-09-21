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
private readonly ApplicationDbContext _dbContext;
    private readonly AppDbContext _identityDb;

    public CommissionService(ApplicationDbContext dbContext, AppDbContext identityDb)
    {
        _dbContext = dbContext;
        _identityDb = identityDb;
    }

    private static void RequireClient(Commission commission, Guid userId)
    {
        if (userId == Guid.Empty || commission.ClientId != userId)
            throw new UnauthorizedAccessException("You are not the client for this commission.");
    }

    private static void RequireCreator(Commission commission, Guid userId)
    {
        if (userId == Guid.Empty || commission.CreatorId != userId)
            throw new UnauthorizedAccessException("You are not the creator for this commission.");
    }

    private static void RequireParticipant(Commission commission, Guid userId)
    {
        if (userId == Guid.Empty || (commission.ClientId != userId && commission.CreatorId != userId))
            throw new UnauthorizedAccessException("You are not a participant in this commission.");
    }

    private static void RequireFundedWork(Commission commission)
    {
        if (commission.Status != CommissionStatus.InProgress ||
            commission.EscrowStatus is not (EscrowStatus.Deposited or EscrowStatus.PartialReleased) ||
            commission.EscrowHeldAmount <= 0)
            throw new InvalidOperationException("The commission must be accepted and funded before milestone work.");
    }

    private static void RequireCurrentMilestone(Commission commission, Milestone milestone)
    {
        if (milestone.Sequence != commission.CurrentStage)
            throw new InvalidOperationException("Only the current milestone can be changed.");
    }

    public async Task<CommissionDto> CreateCommissionAsync(CreateCommissionRequest request, Guid clientId, CancellationToken cancellationToken = default)
    {
if (clientId == Guid.Empty || request.CreatorId == Guid.Empty || clientId == request.CreatorId)
            throw new ArgumentException("A valid, distinct client and creator are required.");
        if (request.TotalPrice <= 0 || decimal.Round(request.TotalPrice, 2) != request.TotalPrice)
            throw new ArgumentException("TotalPrice must be a positive amount in cents.");
        var milestones = request.Milestones?.OrderBy(m => m.Sequence).ToList();
        if (milestones is null || milestones.Count == 0 ||
            !milestones.Select(m => m.Sequence).SequenceEqual(Enumerable.Range(1, milestones.Count)) ||
            milestones.Any(m => m.Price <= 0) ||
            Math.Abs(milestones.Sum(m => m.Price) - request.TotalPrice) > 0.01m)
            throw new ArgumentException("Milestones must be ordered from 1, have positive prices, and total the commission price.");
        var roundedPrices = milestones.Take(milestones.Count - 1)
            .Select(m => decimal.Round(m.Price, 2, MidpointRounding.AwayFromZero)).ToList();
        roundedPrices.Add(request.TotalPrice - roundedPrices.Sum());
        if (roundedPrices.Any(price => price <= 0))
            throw new ArgumentException("Each milestone must cost at least 0.01.");
        var creatorHasRole = await _identityDb.UserRoles
            .Join(_identityDb.Roles, userRole => userRole.RoleId, role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Name })
            .AnyAsync(x => x.UserId == request.CreatorId && x.Name == "Creator", cancellationToken);
        if (!creatorHasRole || !await _identityDb.CreatorProfiles
                .AnyAsync(p => p.UserId == request.CreatorId && !p.IsDeleted, cancellationToken))
            throw new ArgumentException("CreatorId must identify an active creator profile.");
        var discountAmount = 0.00m;
        var finalPrice = request.TotalPrice - discountAmount;

        var commission = new Commission
        {
            Title = request.Title,
            Description = request.Description,
            ClientId = clientId,
            CreatorId = request.CreatorId,
            TotalPrice = request.TotalPrice,
            DiscountAmount = discountAmount,
            FinalPrice = finalPrice,
            EscrowHeldAmount = 0.00m,
            DisbursedAmount = 0.00m,
            EscrowStatus = EscrowStatus.Pending,
            CurrentStage = 1,
            Status = CommissionStatus.PendingAcceptance,
            DeadlineAt = request.DeadlineAt
        };

        for (var index = 0; index < milestones.Count; index++)
        {
            var milestone = milestones[index];
            commission.Milestones.Add(new Milestone
            {
                Sequence = milestone.Sequence,
                Title = milestone.Title,
                Price = roundedPrices[index],
                Status = MilestoneStatus.Pending
            });
        }

        _dbContext.Commissions.Add(commission);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(commission);
    }

    public async Task<(List<CommissionDto> Items, int TotalItems)> GetCommissionsAsync(string? status, string? role, Guid userId, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
if (userId == Guid.Empty) throw new UnauthorizedAccessException("A valid user identity is required.");
        var query = _dbContext.Commissions.AsNoTracking()
            .Where(c => c.ClientId == userId || c.CreatorId == userId);

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

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<CommissionStatus>(status, true, out var parsedStatus) || !Enum.IsDefined(parsedStatus))
                throw new ArgumentException("Invalid commission status.");
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
        RequireParticipant(commission, userId);

        var detailDto = MapToDetailDto(commission);
        return detailDto;
    }

    public async Task<CommissionDto> RespondCommissionAsync(Guid id, RespondCommissionRequest request, Guid creatorId, CancellationToken cancellationToken = default)
    {
var commission = await _dbContext.Commissions.Include(c => c.Milestones)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (commission == null) throw new KeyNotFoundException("Không tìm thấy đơn hàng commission.");
        RequireCreator(commission, creatorId);
        if (commission.Status is not (CommissionStatus.PendingAcceptance or CommissionStatus.Negotiating))
            throw new InvalidOperationException("Only a pending or negotiating commission can be answered.");

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
            if (request.NegotiatePrice is not { } newPrice || newPrice <= commission.DiscountAmount ||
                decimal.Round(newPrice, 2) != newPrice)
                throw new ArgumentException("NegotiatePrice must be a positive amount in cents.");
            var ordered = commission.Milestones.OrderBy(m => m.Sequence).ToList();
            var previousTotal = ordered.Sum(m => m.Price);
            if (ordered.Count == 0 || previousTotal <= 0 || newPrice < ordered.Count * 0.01m)
                throw new InvalidOperationException("The negotiated price cannot cover all milestones.");
            var allocated = 0m;
            for (var index = 0; index < ordered.Count; index++)
            {
var price = index == ordered.Count - 1 ? newPrice - allocated :
                    decimal.Round(newPrice * ordered[index].Price / previousTotal, 2, MidpointRounding.AwayFromZero);
                if (price <= 0) throw new InvalidOperationException("Each negotiated milestone must cost at least 0.01.");
                ordered[index].Price = price;
                allocated += price;
            }
            commission.TotalPrice = newPrice;
            commission.FinalPrice = newPrice - commission.DiscountAmount;
            commission.Status = CommissionStatus.Negotiating;
        }

        commission.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(commission);
    }

    public async Task<CommissionDto> DepositEscrowAsync(Guid id, Guid clientId, string paymentMethod, CancellationToken cancellationToken = default)
    {
var commission = await _dbContext.Commissions.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (commission == null) throw new KeyNotFoundException("Không tìm thấy đơn hàng commission.");
        RequireClient(commission, clientId);
        if (commission.Status != CommissionStatus.InProgress)
            throw new InvalidOperationException("The creator must accept the commission before escrow deposit.");
        if (commission.EscrowStatus != EscrowStatus.Pending || commission.EscrowHeldAmount != 0)
            throw new InvalidOperationException("Escrow has already been deposited for this commission.");

        commission.EscrowHeldAmount = commission.FinalPrice;
        commission.EscrowStatus = EscrowStatus.Deposited;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);

        return MapToDto(commission);
    }

    public async Task<MilestoneDto> SubmitMilestoneWipAsync(Guid commissionId, Guid milestoneId, SubmitMilestoneRequest request, Guid creatorId, CancellationToken cancellationToken = default)
    {
var commission = await _dbContext.Commissions.FirstOrDefaultAsync(c => c.Id == commissionId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy đơn hàng commission.");
        RequireCreator(commission, creatorId);
        var milestone = await _dbContext.Milestones.FirstOrDefaultAsync(m => m.Id == milestoneId && m.CommissionId == commissionId, cancellationToken);
        if (milestone == null) throw new KeyNotFoundException("Không tìm thấy cột mốc milestone.");
        RequireFundedWork(commission);
        RequireCurrentMilestone(commission, milestone);
        if (milestone.Status is not (MilestoneStatus.Pending or MilestoneStatus.RevisionRequested))
            throw new InvalidOperationException("Only a pending or revision-requested milestone can be submitted.");

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
var commission = await _dbContext.Commissions.Include(c => c.Milestones).FirstOrDefaultAsync(c => c.Id == commissionId, cancellationToken);
        if (commission == null) throw new KeyNotFoundException("Không tìm thấy đơn hàng commission.");
        RequireClient(commission, clientId);

        var milestone = commission.Milestones.FirstOrDefault(m => m.Id == milestoneId);
        if (milestone == null) throw new KeyNotFoundException("Không tìm thấy cột mốc milestone.");
        RequireFundedWork(commission);
        RequireCurrentMilestone(commission, milestone);
        if (milestone.Status != MilestoneStatus.Submitted)
            throw new InvalidOperationException("The milestone must be submitted before approval.");
        if (commission.EscrowHeldAmount < milestone.Price || commission.DisbursedAmount + milestone.Price > commission.FinalPrice)
            throw new InvalidOperationException("Insufficient escrow for milestone approval.");

        milestone.Status = MilestoneStatus.Approved;
        milestone.ApprovedAt = DateTimeOffset.UtcNow;
        milestone.UpdatedAt = DateTimeOffset.UtcNow;

        commission.DisbursedAmount += milestone.Price;
        commission.EscrowHeldAmount -= milestone.Price;
        commission.CurrentStage += 1;
        commission.EscrowStatus = commission.EscrowHeldAmount == 0 ? EscrowStatus.Released : EscrowStatus.PartialReleased;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);

        return MapToMilestoneDto(milestone);
    }

    public async Task<MilestoneDto> RequestMilestoneRevisionAsync(Guid commissionId, Guid milestoneId, RequestRevisionRequest request, Guid clientId, CancellationToken cancellationToken = default)
    {
var commission = await _dbContext.Commissions.FirstOrDefaultAsync(c => c.Id == commissionId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy đơn hàng commission.");
        RequireClient(commission, clientId);
        var milestone = await _dbContext.Milestones.FirstOrDefaultAsync(m => m.Id == milestoneId && m.CommissionId == commissionId, cancellationToken);
        if (milestone == null) throw new KeyNotFoundException("Không tìm thấy cột mốc milestone.");
        RequireFundedWork(commission);
        RequireCurrentMilestone(commission, milestone);
        if (milestone.Status != MilestoneStatus.Submitted)
            throw new InvalidOperationException("The milestone must be submitted before a revision can be requested.");

        milestone.Status = MilestoneStatus.RevisionRequested;
        milestone.RevisionCount += 1;
        milestone.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToMilestoneDto(milestone);
    }

    public async Task<CommissionDto> DeliverFinalWorkAsync(Guid id, string finalDeliverableUrl, Guid creatorId, CancellationToken cancellationToken = default)
    {
var commission = await _dbContext.Commissions.Include(c => c.Milestones).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (commission == null) throw new KeyNotFoundException("Không tìm thấy đơn hàng commission.");
        RequireCreator(commission, creatorId);
        if (commission.Status != CommissionStatus.InProgress || commission.EscrowStatus != EscrowStatus.Released ||
            commission.EscrowHeldAmount != 0 || commission.DisbursedAmount != commission.FinalPrice ||
            commission.Milestones.Count == 0 || commission.Milestones.Any(m => m.Status != MilestoneStatus.Approved))
            throw new InvalidOperationException("All funded milestones must be approved before final delivery.");

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
var commission = await _dbContext.Commissions.Include(c => c.Milestones).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (commission == null) throw new KeyNotFoundException("Không tìm thấy đơn hàng commission.");
        RequireClient(commission, clientId);
        var lastMilestone = commission.Milestones.OrderByDescending(m => m.Sequence).FirstOrDefault();
        if (commission.Status != CommissionStatus.SubmittedFinal || commission.EscrowStatus != EscrowStatus.Released ||
            commission.EscrowHeldAmount != 0 || commission.DisbursedAmount != commission.FinalPrice ||
            commission.Milestones.Any(m => m.Status != MilestoneStatus.Approved) ||
            string.IsNullOrWhiteSpace(lastMilestone?.FinalDeliverableUrl))
            throw new InvalidOperationException("A fully approved final delivery is required before completion.");

        commission.Status = CommissionStatus.Completed;
        commission.EscrowStatus = EscrowStatus.Released;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

var presignedUrl = lastMilestone?.FinalDeliverableUrl == null ? string.Empty : $"{lastMilestone.FinalDeliverableUrl}?token=s3-presigned-download-access";

        return (MapToDto(commission), presignedUrl);
    }

    public async Task<CommissionDto> CancelCommissionAsync(Guid id, string reason, Guid userId, CancellationToken cancellationToken = default)
    {
var commission = await _dbContext.Commissions.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (commission == null) throw new KeyNotFoundException("Không tìm thấy đơn hàng commission.");
        RequireParticipant(commission, userId);
        if (commission.Status is not (CommissionStatus.PendingAcceptance or CommissionStatus.Negotiating or CommissionStatus.InProgress) ||
            commission.EscrowStatus != EscrowStatus.Pending || commission.EscrowHeldAmount != 0 || commission.DisbursedAmount != 0)
            throw new InvalidOperationException("Only an unfunded active commission can be cancelled directly.");

        commission.Status = CommissionStatus.Cancelled;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);

        return MapToDto(commission);
    }

    public async Task<DisputeDto> CreateDisputeAsync(Guid id, CreateDisputeRequest request, Guid raisedById, CancellationToken cancellationToken = default)
    {
var commission = await _dbContext.Commissions.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy đơn hàng commission.");
        RequireParticipant(commission, raisedById);
        if (commission.Status is CommissionStatus.Cancelled or CommissionStatus.Completed or CommissionStatus.Disputed ||
            await _dbContext.Disputes.AnyAsync(d => d.CommissionId == id, cancellationToken))
            throw new InvalidOperationException("This commission cannot be disputed again.");
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
if (commission.EscrowHeldAmount > 0)
            commission.EscrowStatus = EscrowStatus.Disputed;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDisputeDto(dispute);
    }

    public async Task<DisputeDto?> GetDisputeByCommissionIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
var commission = await _dbContext.Commissions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (commission == null) return null;
        RequireParticipant(commission, userId);
        var dispute = await _dbContext.Disputes.AsNoTracking().FirstOrDefaultAsync(d => d.CommissionId == id, cancellationToken);
        return dispute == null ? null : MapToDisputeDto(dispute);
    }

    public async Task<ReviewDto> CreateReviewAsync(Guid id, CreateReviewRequest request, Guid reviewerId, CancellationToken cancellationToken = default)
    {
var commission = await _dbContext.Commissions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy đơn hàng commission.");
        RequireClient(commission, reviewerId);
        if (commission.Status != CommissionStatus.Completed)
            throw new InvalidOperationException("The commission must be completed before review.");
        if (await _dbContext.Reviews.AnyAsync(r => r.CommissionId == id, cancellationToken))
            throw new InvalidOperationException("This commission has already been reviewed.");
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
var commission = await _dbContext.Commissions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy đơn hàng commission.");
        RequireCreator(commission, creatorId);
        if (commission.Status != CommissionStatus.Completed)
            throw new InvalidOperationException("Only a completed commission review can be answered.");
        var review = await _dbContext.Reviews.FirstOrDefaultAsync(r => r.CommissionId == id, cancellationToken);
        if (review == null) throw new KeyNotFoundException("Không tìm thấy đánh giá cho đơn hàng này.");
        if (!string.IsNullOrWhiteSpace(review.ReviewerReply))
            throw new InvalidOperationException("This review has already been answered.");

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
