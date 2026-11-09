using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Dispute.TradeDispute.DTOs;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ArtCommission.Application.Dispute.TradeDispute.Queries;

public sealed record GetTradeDisputesQuery(
    string? Status = null,
    TradeDisputeCategory? Category = null,
    Guid? ArtTradeId = null,
    int Page = 1,
    int PageSize = 10
) : IRequest<(bool Success, List<TradeDisputeDto> Items, int TotalCount)>;

public class GetTradeDisputesQueryHandler
    : IRequestHandler<GetTradeDisputesQuery, (bool Success, List<TradeDisputeDto> Items, int TotalCount)>
{
    private readonly IApplicationDbContext _db;

    public GetTradeDisputesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, List<TradeDisputeDto> Items, int TotalCount)> Handle(
        GetTradeDisputesQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 50 ? 10 : request.PageSize;

        var query = _db.TradeDisputes
            .AsNoTracking()
            .Include(d => d.ArtTrade)
            .Where(d => !d.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(d => d.Status == request.Status);
        }

        if (request.Category.HasValue)
        {
            query = query.Where(d => d.Category == request.Category.Value);
        }

        if (request.ArtTradeId.HasValue)
        {
            query = query.Where(d => d.ArtTradeId == request.ArtTradeId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var disputes = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var userIds = disputes.Select(d => d.RaisedById).Distinct().ToList();
        var users = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var items = disputes.Select(d =>
        {
            List<string> evidenceList = [];
            if (!string.IsNullOrEmpty(d.EvidenceUrls))
            {
                try
                {
                    evidenceList = JsonSerializer.Deserialize<List<string>>(d.EvidenceUrls) ?? [];
                }
                catch
                {
                    evidenceList = [d.EvidenceUrls];
                }
            }

            users.TryGetValue(d.RaisedById, out var raisedByName);

            var category = d.Category;
            if (!category.HasValue && !string.IsNullOrWhiteSpace(d.Reason) && d.Reason.StartsWith("["))
            {
                var closeBracketIdx = d.Reason.IndexOf(']');
                if (closeBracketIdx > 1)
                {
                    category = TradeDisputeCategoryExtensions.ParseFlexible(d.Reason.Substring(1, closeBracketIdx - 1));
                }
            }

            return new TradeDisputeDto
            {
                Id = d.Id,
                ArtTradeId = d.ArtTradeId,
                TradeTitle = d.ArtTrade?.Title,
                RaisedById = d.RaisedById,
                RaisedByName = raisedByName,
                Category = category,
                Reason = d.Reason,
                EvidenceUrls = d.EvidenceUrls,
                EvidenceUrlList = evidenceList,
                Status = d.Status,
                Resolution = d.Resolution,
                AdminNote = d.AdminNote,
                ResolvedAt = d.ResolvedAt,
                CreatedAt = d.CreatedAt
            };
        }).ToList();

        return (true, items, totalCount);
    }
}

