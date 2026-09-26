using System.Text.Json;
using ArtCommission.Application.Commission.Disputes.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Commission.Disputes.Queries;

/// <summary>
/// Query lấy chi tiết hồ sơ trọng tài tranh chấp: claim, commission, escrow amount, milestones và chat snapshot (SCR-22 / UC30).
/// </summary>
public record GetDisputeArbitrationDetailQuery(Guid DisputeId) : IRequest<DisputeArbitrationDetailDto?>;

public class GetDisputeArbitrationDetailQueryHandler : IRequestHandler<GetDisputeArbitrationDetailQuery, DisputeArbitrationDetailDto?>
{
    private readonly IApplicationDbContext _db;

    public GetDisputeArbitrationDetailQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DisputeArbitrationDetailDto?> Handle(GetDisputeArbitrationDetailQuery request, CancellationToken cancellationToken)
    {
        var dispute = await _db.Disputes
            .AsNoTracking()
            .Include(d => d.Commission)
                .ThenInclude(c => c.Milestones)
            .FirstOrDefaultAsync(d => d.Id == request.DisputeId && !d.IsDeleted, cancellationToken);

        if (dispute == null)
        {
            return null;
        }

        var commission = dispute.Commission;

        var userIds = new[] { commission.ClientId, commission.CreatorId, dispute.RaisedById }
            .Distinct()
            .ToList();

        var users = await _db.Set<ApplicationUser>()
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u, cancellationToken);

        users.TryGetValue(commission.ClientId, out var client);
        users.TryGetValue(commission.CreatorId, out var creator);
        users.TryGetValue(dispute.RaisedById, out var raisedBy);

        var raisedByRole = dispute.RaisedById == commission.ClientId ? "Client"
            : dispute.RaisedById == commission.CreatorId ? "Creator"
            : "Other";

        var evidenceList = new List<string>();
        if (!string.IsNullOrWhiteSpace(dispute.EvidenceUrls))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(dispute.EvidenceUrls);
                if (parsed != null)
                {
                    evidenceList.AddRange(parsed);
                }
            }
            catch
            {
                evidenceList.AddRange(dispute.EvidenceUrls.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        var milestones = commission.Milestones
            .OrderBy(m => m.Sequence)
            .Select(m => new DisputeMilestoneDto
            {
                Id = m.Id,
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
            })
            .ToList();

        // Lấy lịch sử chat snapshot giữa 2 bên trong phòng làm việc Workroom
        var messages = await _db.Messages
            .AsNoTracking()
            .Where(m => m.CommissionId == commission.Id && !m.IsDeleted)
            .OrderBy(m => m.SentAt)
            .ToListAsync(cancellationToken);

        var chatSnapshot = messages.Select(m =>
        {
            string senderRole;
            string senderName;

            if (m.SenderId == commission.ClientId)
            {
                senderRole = "Client";
                senderName = client?.FullName ?? client?.UserName ?? "Client";
            }
            else if (m.SenderId == commission.CreatorId)
            {
                senderRole = "Creator";
                senderName = creator?.FullName ?? creator?.UserName ?? "Creator";
            }
            else
            {
                senderRole = "System";
                senderName = "Hệ thống";
            }

            return new DisputeChatSnapshotMessageDto
            {
                MessageId = m.Id,
                SenderId = m.SenderId,
                SenderName = senderName,
                SenderRole = senderRole,
                MessageType = m.MessageType,
                Body = m.Body,
                AttachmentUrl = m.AttachmentUrl,
                SentAt = m.SentAt
            };
        }).ToList();

        return new DisputeArbitrationDetailDto
        {
            DisputeId = dispute.Id,
            CommissionId = commission.Id,
            CommissionTitle = commission.Title,
            CommissionDescription = commission.Description,
            CommissionStatus = commission.Status.ToString(),
            EscrowStatus = commission.EscrowStatus.ToString(),
            TotalPrice = commission.TotalPrice,
            DiscountAmount = commission.DiscountAmount,
            FinalPrice = commission.FinalPrice,
            EscrowHeldAmount = commission.EscrowHeldAmount,
            DisbursedAmount = commission.DisbursedAmount,
            CurrentStage = commission.CurrentStage,
            DeadlineAt = commission.DeadlineAt,

            Client = new DisputePartyDto
            {
                UserId = commission.ClientId,
                FullName = client?.FullName ?? "Unknown",
                UserName = client?.UserName ?? string.Empty,
                Email = client?.Email ?? string.Empty
            },

            Creator = new DisputePartyDto
            {
                UserId = commission.CreatorId,
                FullName = creator?.FullName ?? "Unknown",
                UserName = creator?.UserName ?? string.Empty,
                Email = creator?.Email ?? string.Empty
            },

            RaisedBy = new DisputeRaiserDto
            {
                UserId = dispute.RaisedById,
                FullName = raisedBy?.FullName ?? raisedBy?.UserName ?? "Unknown",
                Role = raisedByRole
            },

            Reason = dispute.Reason,
            EvidenceUrls = evidenceList,
            Status = dispute.Status,
            Resolution = dispute.Resolution,
            ClientRefundAmount = dispute.ClientRefundAmount,
            ArtistPayAmount = dispute.ArtistPayAmount,
            AdminNote = dispute.AdminNote,
            CreatedAt = dispute.CreatedAt,
            ResolvedAt = dispute.ResolvedAt,

            Milestones = milestones,
            ChatSnapshot = chatSnapshot
        };
    }
}
