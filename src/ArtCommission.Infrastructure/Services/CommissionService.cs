using ArtCommission.Application.Commission.DTOs;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Domain.Entities.Commission;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Infrastructure.Services;

public class CommissionService : ICommissionService
{
    private readonly ApplicationDbContext _dbContext;

    public CommissionService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CommissionDto> CreateCommissionAsync(CreateCommissionRequest request, Guid clientId, CancellationToken cancellationToken = default)
    {
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

        if (request.Milestones != null && request.Milestones.Any())
        {
            foreach (var m in request.Milestones)
            {
                commission.Milestones.Add(new Milestone
                {
                    Sequence = m.Sequence,
                    Title = m.Title,
                    Price = m.Price,
                    Status = MilestoneStatus.Pending
                });
            }
        }

        _dbContext.Commissions.Add(commission);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(commission);
    }

    public async Task<(List<CommissionDto> Items, int TotalItems)> GetCommissionsAsync(string? status, string? role, Guid userId, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Commissions.AsNoTracking().AsQueryable();

        if (string.Equals(role, "Client", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.ClientId == userId);
        }
        else if (string.Equals(role, "Creator", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.CreatorId == userId);
        }

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

    public async Task<CommissionDetailDto?> GetCommissionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var commission = await _dbContext.Commissions
            .Include(c => c.Milestones)
            .Include(c => c.Reviews)
            .Include(c => c.Disputes)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (commission == null) return null;

        var detailDto = MapToDetailDto(commission);
        return detailDto;
    }

    public async Task<CommissionDto> RespondCommissionAsync(Guid id, RespondCommissionRequest request, Guid creatorId, CancellationToken cancellationToken = default)
    {
        var commission = await _dbContext.Commissions.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (commission == null) throw new KeyNotFoundException("Không tìm thấy đơn hàng commission.");

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
                commission.TotalPrice = request.NegotiatePrice.Value;
                commission.FinalPrice = request.NegotiatePrice.Value - commission.DiscountAmount;
            }
        }

        commission.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(commission);
    }

    public async Task<CommissionDto> DepositEscrowAsync(Guid id, Guid clientId, string paymentMethod, CancellationToken cancellationToken = default)
    {
        var commission = await _dbContext.Commissions.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (commission == null) throw new KeyNotFoundException("Không tìm thấy đơn hàng commission.");

        commission.EscrowHeldAmount = commission.FinalPrice;
        commission.EscrowStatus = EscrowStatus.Deposited;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(commission);
    }

    public async Task<MilestoneDto> SubmitMilestoneWipAsync(Guid commissionId, Guid milestoneId, SubmitMilestoneRequest request, Guid creatorId, CancellationToken cancellationToken = default)
    {
        var milestone = await _dbContext.Milestones.FirstOrDefaultAsync(m => m.Id == milestoneId && m.CommissionId == commissionId, cancellationToken);
        if (milestone == null) throw new KeyNotFoundException("Không tìm thấy cột mốc milestone.");

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

        var milestone = commission.Milestones.FirstOrDefault(m => m.Id == milestoneId);
        if (milestone == null) throw new KeyNotFoundException("Không tìm thấy cột mốc milestone.");

        milestone.Status = MilestoneStatus.Approved;
        milestone.ApprovedAt = DateTimeOffset.UtcNow;
        milestone.UpdatedAt = DateTimeOffset.UtcNow;

        commission.DisbursedAmount += milestone.Price;
        commission.EscrowHeldAmount = Math.Max(0, commission.EscrowHeldAmount - milestone.Price);
        commission.CurrentStage += 1;
        commission.EscrowStatus = commission.EscrowHeldAmount == 0 ? EscrowStatus.Released : EscrowStatus.PartialReleased;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToMilestoneDto(milestone);
    }

    public async Task<MilestoneDto> RequestMilestoneRevisionAsync(Guid commissionId, Guid milestoneId, RequestRevisionRequest request, Guid clientId, CancellationToken cancellationToken = default)
    {
        var milestone = await _dbContext.Milestones.FirstOrDefaultAsync(m => m.Id == milestoneId && m.CommissionId == commissionId, cancellationToken);
        if (milestone == null) throw new KeyNotFoundException("Không tìm thấy cột mốc milestone.");

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

        commission.Status = CommissionStatus.Completed;
        commission.EscrowStatus = EscrowStatus.Released;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var lastDeliverable = commission.Milestones.LastOrDefault()?.FinalDeliverableUrl ?? "https://storage.dillustration.com/deliverables/final.png";
        var presignedUrl = $"{lastDeliverable}?token=s3-presigned-download-access";

        return (MapToDto(commission), presignedUrl);
    }

    public async Task<CommissionDto> CancelCommissionAsync(Guid id, string reason, Guid userId, CancellationToken cancellationToken = default)
    {
        var commission = await _dbContext.Commissions.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (commission == null) throw new KeyNotFoundException("Không tìm thấy đơn hàng commission.");

        commission.Status = CommissionStatus.Cancelled;
        commission.EscrowStatus = EscrowStatus.Refunded;
        commission.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(commission);
    }

    public async Task<DisputeDto> CreateDisputeAsync(Guid id, CreateDisputeRequest request, Guid raisedById, CancellationToken cancellationToken = default)
    {
        var dispute = new Dispute
        {
            CommissionId = id,
            RaisedById = raisedById,
            Reason = request.Reason,
            EvidenceUrls = request.EvidenceUrls != null ? string.Join(",", request.EvidenceUrls) : null,
            Status = "Pending"
        };

        _dbContext.Disputes.Add(dispute);

        var commission = await _dbContext.Commissions.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (commission != null)
        {
            commission.Status = CommissionStatus.Disputed;
            commission.EscrowStatus = EscrowStatus.Disputed;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDisputeDto(dispute);
    }

    public async Task<DisputeDto?> GetDisputeByCommissionIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dispute = await _dbContext.Disputes.AsNoTracking().FirstOrDefaultAsync(d => d.CommissionId == id, cancellationToken);
        return dispute == null ? null : MapToDisputeDto(dispute);
    }

    public async Task<ReviewDto> CreateReviewAsync(Guid id, CreateReviewRequest request, Guid reviewerId, CancellationToken cancellationToken = default)
    {
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
        var review = await _dbContext.Reviews.FirstOrDefaultAsync(r => r.CommissionId == id, cancellationToken);
        if (review == null) throw new KeyNotFoundException("Không tìm thấy đánh giá cho đơn hàng này.");

        review.ReviewerReply = replyComment;
        review.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToReviewDto(review);
    }

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
