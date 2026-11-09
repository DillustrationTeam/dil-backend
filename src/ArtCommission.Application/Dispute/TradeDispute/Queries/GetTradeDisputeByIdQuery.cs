using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Dispute.TradeDispute.DTOs;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ArtCommission.Application.Dispute.TradeDispute.Queries;

public sealed record GetTradeDisputeByIdQuery(
    Guid DisputeId,
    Guid CurrentUserId
) : IRequest<(bool Success, TradeDisputeDto? Data, string[] Errors)>;

public class GetTradeDisputeByIdQueryHandler
    : IRequestHandler<GetTradeDisputeByIdQuery, (bool Success, TradeDisputeDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public GetTradeDisputeByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, TradeDisputeDto? Data, string[] Errors)> Handle(
        GetTradeDisputeByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dispute = await _db.TradeDisputes
            .AsNoTracking()
            .Include(d => d.ArtTrade)
            .FirstOrDefaultAsync(d => d.Id == request.DisputeId && !d.IsDeleted, cancellationToken);

        if (dispute == null)
        {
            return (false, null, ["Không tìm thấy hồ sơ khiếu nại."]);
        }

        var trade = dispute.ArtTrade;
        var isParticipant = trade != null && (trade.InitiatorId == request.CurrentUserId || trade.ReceiverId == request.CurrentUserId);
        // Note: Admin check can also be done via controller authorize

        var raisedByUser = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == dispute.RaisedById, cancellationToken);

        Guid partnerId = Guid.Empty;
        string? partnerName = null;

        if (trade != null)
        {
            partnerId = dispute.RaisedById == trade.InitiatorId ? trade.ReceiverId : trade.InitiatorId;
            var partnerUser = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == partnerId, cancellationToken);
            partnerName = partnerUser?.FullName;
        }

        List<string> evidenceList = [];
        if (!string.IsNullOrEmpty(dispute.EvidenceUrls))
        {
            try
            {
                evidenceList = JsonSerializer.Deserialize<List<string>>(dispute.EvidenceUrls) ?? [];
            }
            catch
            {
                evidenceList = [dispute.EvidenceUrls];
            }
        }

        var category = dispute.Category;
        if (!category.HasValue && !string.IsNullOrWhiteSpace(dispute.Reason) && dispute.Reason.StartsWith("["))
        {
            var closeBracketIdx = dispute.Reason.IndexOf(']');
            if (closeBracketIdx > 1)
            {
                category = TradeDisputeCategoryExtensions.ParseFlexible(dispute.Reason.Substring(1, closeBracketIdx - 1));
            }
        }

        var dto = new TradeDisputeDto
        {
            Id = dispute.Id,
            ArtTradeId = dispute.ArtTradeId,
            TradeTitle = trade?.Title,
            RaisedById = dispute.RaisedById,
            RaisedByName = raisedByUser?.FullName,
            PartnerId = partnerId,
            PartnerName = partnerName,
            Category = category,
            Reason = dispute.Reason,
            EvidenceUrls = dispute.EvidenceUrls,
            EvidenceUrlList = evidenceList,
            Status = dispute.Status,
            Resolution = dispute.Resolution,
            AdminNote = dispute.AdminNote,
            ResolvedAt = dispute.ResolvedAt,
            CreatedAt = dispute.CreatedAt
        };

        return (true, dto, []);
    }
}

