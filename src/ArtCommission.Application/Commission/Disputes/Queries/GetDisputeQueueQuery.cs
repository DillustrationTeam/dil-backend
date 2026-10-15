using ArtCommission.Application.Commission.Disputes.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Commission.Disputes.Queries;

/// <summary>
/// Query lấy danh sách hàng đợi các vụ khiếu nại tranh chấp cần xử lý, kèm tổng tiền escrow bị lock (SCR-22 / UC30).
/// </summary>
public record GetDisputeQueueQuery(
    string? Status = "Pending",
    string? Search = null,
    int Page = 1,
    int PageSize = 10
) : IRequest<(IReadOnlyList<DisputeQueueItemDto> Items, decimal TotalLockedEscrow, int TotalCount, int PendingCount, int UnderReviewCount)>;

public class GetDisputeQueueQueryHandler
    : IRequestHandler<GetDisputeQueueQuery, (IReadOnlyList<DisputeQueueItemDto> Items, decimal TotalLockedEscrow, int TotalCount, int PendingCount, int UnderReviewCount)>
{
    private readonly IApplicationDbContext _db;

    public GetDisputeQueueQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<DisputeQueueItemDto> Items, decimal TotalLockedEscrow, int TotalCount, int PendingCount, int UnderReviewCount)> Handle(
        GetDisputeQueueQuery request,
        CancellationToken cancellationToken)
    {
        var pendingCount = await _db.Disputes
            .CountAsync(d => d.Status == "Pending" && !d.IsDeleted, cancellationToken);

        var underReviewCount = await _db.Disputes
            .CountAsync(d => d.Status == "UnderReview" && !d.IsDeleted, cancellationToken);

        var query = _db.Disputes
            .AsNoTracking()
            .Include(d => d.Commission)
            .Where(d => !d.IsDeleted && !d.Commission.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Status) && !request.Status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(d => d.Status == request.Status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(d => d.Commission.Title.Contains(search) || d.Reason.Contains(search));
        }

        var totalLockedEscrow = await query
            .SumAsync(d => d.Commission.EscrowHeldAmount, cancellationToken);

        var totalCount = await query.CountAsync(cancellationToken);

        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize is > 0 and <= 100 ? request.PageSize : 10;

        var rawDisputes = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (rawDisputes.Count == 0)
        {
            return (Array.Empty<DisputeQueueItemDto>(), totalLockedEscrow, totalCount, pendingCount, underReviewCount);
        }

        // Lấy danh sách UserId cần hiển thị (Client, Creator, RaisedBy)
        var userIds = rawDisputes
            .SelectMany(d => new[] { d.Commission.ClientId, d.Commission.CreatorId, d.RaisedById })
            .Distinct()
            .ToList();

        var users = await _db.Set<ApplicationUser>()
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u, cancellationToken);

        var items = rawDisputes.Select(d =>
        {
            users.TryGetValue(d.Commission.ClientId, out var client);
            users.TryGetValue(d.Commission.CreatorId, out var creator);
            users.TryGetValue(d.RaisedById, out var raisedBy);

            var raisedByRole = d.RaisedById == d.Commission.ClientId ? "Client"
                : d.RaisedById == d.Commission.CreatorId ? "Creator"
                : "Other";

            return new DisputeQueueItemDto
            {
                DisputeId = d.Id,
                CommissionId = d.CommissionId,
                CommissionTitle = d.Commission.Title,
                RaisedById = d.RaisedById,
                RaisedByName = raisedBy?.FullName ?? raisedBy?.UserName ?? "Unknown",
                RaisedByRole = raisedByRole,
                ClientId = d.Commission.ClientId,
                ClientName = client?.FullName ?? client?.UserName ?? "Unknown",
                ClientEmail = client?.Email ?? string.Empty,
                CreatorId = d.Commission.CreatorId,
                CreatorName = creator?.FullName ?? creator?.UserName ?? "Unknown",
                CreatorEmail = creator?.Email ?? string.Empty,
                FinalPrice = d.Commission.FinalPrice,
                EscrowHeldAmount = d.Commission.EscrowHeldAmount,
                Reason = d.Reason,
                Status = d.Status,
                Resolution = d.Resolution,
                CreatedAt = d.CreatedAt,
                ResolvedAt = d.ResolvedAt
            };
        }).ToList();

        return (items, totalLockedEscrow, totalCount, pendingCount, underReviewCount);
    }
}
