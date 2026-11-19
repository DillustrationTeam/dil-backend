using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.ArtistStudio;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Queries.TraceArtworkForensic;

public record TraceArtworkForensicQuery(
    string? ReceiptToken,
    long? NumericPayload,
    Guid? ArtworkId,
    Guid? CommissionId = null,
    string? FileSha256 = null,
    bool AllowFuzzyDwtMatch = false,
    long? VisualHash = null,
    string? ArtworkPrefix = null,
    string? CommissionPrefix = null,
    Guid? RequestingUserId = null,
    bool IsAdmin = false
) : IRequest<List<ArtworkForensicTraceDto>>;

public record GetRecentForensicReceiptsQuery(
    Guid UserId,
    bool IncludeAll
) : IRequest<List<ArtworkForensicTraceDto>>;

public record ArtworkForensicTraceDto(
    Guid ReceiptId,
    string ReceiptToken,
    long? NumericPayload,
    Guid? ArtworkId,
    string ArtworkTitle,
    string CreatorName,
    Guid? ViewerUserId,
    string? ViewerEmail,
    string? ViewerName,
    string IpAddress,
    string? UserAgent,
    DateTimeOffset ViewedAt,
    string Status,
    Guid? CommissionId = null,
    Guid? MilestoneId = null,
    string? WatermarkType = null,
    string? FileName = null,
    string? FileSha256 = null
);

public class TraceArtworkForensicQueryHandler :
    IRequestHandler<TraceArtworkForensicQuery, List<ArtworkForensicTraceDto>>,
    IRequestHandler<GetRecentForensicReceiptsQuery, List<ArtworkForensicTraceDto>>
{
    private readonly IApplicationDbContext _db;

    public TraceArtworkForensicQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<ArtworkForensicTraceDto>> Handle(
        GetRecentForensicReceiptsQuery request,
        CancellationToken cancellationToken)
    {
        var baseQuery = _db.ArtworkWatermarkReceipts
            .Include(r => r.Artwork)
                .ThenInclude(a => a!.CreatorProfile)
            .AsNoTracking();

        if (!request.IncludeAll)
        {
            var myCreatorId = await _db.CreatorProfiles
                .AsNoTracking()
                .Where(p => p.UserId == request.UserId && !p.IsDeleted)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var myCommissionIds = await _db.Commissions
                .AsNoTracking()
                .Where(c => c.ClientId == request.UserId || (myCreatorId.HasValue && c.CreatorId == myCreatorId.Value))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            baseQuery = baseQuery.Where(r =>
                r.ViewerUserId == request.UserId
                || (myCreatorId.HasValue && r.Artwork != null && r.Artwork.CreatorProfileId == myCreatorId.Value)
                || (r.CommissionId.HasValue && myCommissionIds.Contains(r.CommissionId.Value)));
        }

        var rows = await baseQuery
            .OrderByDescending(r => r.CreatedAt)
            .Take(15)
            .ToListAsync(cancellationToken);

        return await MapRowsAsync(rows, cancellationToken);
    }

    public async Task<List<ArtworkForensicTraceDto>> Handle(TraceArtworkForensicQuery request, CancellationToken cancellationToken)
    {
        var query = _db.ArtworkWatermarkReceipts
            .Include(r => r.Artwork)
                .ThenInclude(a => a!.CreatorProfile)
            .AsNoTracking()
            .AsQueryable();

        // Bảo mật RBAC: Nếu không phải Admin/Moderator, chỉ được tra cứu biên lai của chính mình:
        // - Là người xem/tải (ViewerUserId == userId)
        // - Là tác giả của Artwork (Artwork.CreatorProfile.UserId == userId)
        // - Là Khách hàng hoặc Họa sĩ của đơn Commission
        if (!request.IsAdmin && request.RequestingUserId.HasValue && request.RequestingUserId.Value != Guid.Empty)
        {
            var userId = request.RequestingUserId.Value;
            var myCreatorId = await _db.CreatorProfiles
                .AsNoTracking()
                .Where(p => p.UserId == userId && !p.IsDeleted)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var myCommissionIds = await _db.Commissions
                .AsNoTracking()
                .Where(c => c.ClientId == userId || (myCreatorId.HasValue && c.CreatorId == myCreatorId.Value))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            query = query.Where(r =>
                r.ViewerUserId == userId
                || (myCreatorId.HasValue && r.Artwork != null && r.Artwork.CreatorProfileId == myCreatorId.Value)
                || (r.CommissionId.HasValue && myCommissionIds.Contains(r.CommissionId.Value)));
        }

        if (!string.IsNullOrWhiteSpace(request.ReceiptToken))
        {
            var tokenClean = request.ReceiptToken.Trim();
            query = query.Where(r => r.ReceiptToken == tokenClean || r.ReceiptToken.Contains(tokenClean));
        }
        else if (request.NumericPayload.HasValue)
        {
            query = query.Where(r => r.NumericPayload == request.NumericPayload.Value);
        }
        else if (!string.IsNullOrWhiteSpace(request.FileSha256))
        {
            var shaClean = request.FileSha256.Trim().ToLowerInvariant();
            query = query.Where(r => r.FileSha256 == shaClean);
        }
        else if (request.ArtworkId.HasValue)
        {
            query = query.Where(r => r.ArtworkId == request.ArtworkId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(request.ArtworkPrefix))
        {
            var pfx = request.ArtworkPrefix.Trim().ToUpperInvariant();
            query = query.Where(r => r.ArtworkId.HasValue && r.ArtworkId.Value.ToString().Replace("-", "").ToUpper().StartsWith(pfx));
        }
        else if (request.CommissionId.HasValue)
        {
            query = query.Where(r => r.CommissionId == request.CommissionId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(request.CommissionPrefix))
        {
            var pfx = request.CommissionPrefix.Trim().ToUpperInvariant();
            query = query.Where(r => r.CommissionId.HasValue && r.CommissionId.Value.ToString().Replace("-", "").ToUpper().StartsWith(pfx));
        }
        else if (request.VisualHash.HasValue)
        {
            ulong targetVisual = (ulong)request.VisualHash.Value;
            var visualCandidates = await _db.ArtworkWatermarkReceipts
                .Include(r => r.Artwork)
                    .ThenInclude(a => a!.CreatorProfile)
                .AsNoTracking()
                .Where(r => r.VisualHash.HasValue)
                .OrderByDescending(r => r.CreatedAt)
                .Take(1000)
                .ToListAsync(cancellationToken);

            var visualRows = visualCandidates
                .Select(r => new { Receipt = r, Dist = BitOperations.PopCount((ulong)r.VisualHash!.Value ^ targetVisual) })
                .Where(x => x.Dist <= 10)
                .OrderBy(x => x.Dist)
                .ThenByDescending(x => x.Receipt.CreatedAt)
                .Take(10)
                .Select(x => x.Receipt)
                .ToList();

            return await MapRowsAsync(visualRows, cancellationToken);
        }
        else
        {
            return new List<ArtworkForensicTraceDto>();
        }

        var rows = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0 && request.AllowFuzzyDwtMatch && request.NumericPayload.HasValue)
        {
            ulong target = (ulong)request.NumericPayload.Value;
            int pop = BitOperations.PopCount(target);
            if (pop is >= 8 and <= 56)
            {
                var candidates = await _db.ArtworkWatermarkReceipts
                    .Include(r => r.Artwork)
                        .ThenInclude(a => a!.CreatorProfile)
                    .AsNoTracking()
                    .Where(r => r.NumericPayload.HasValue)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(1000)
                    .ToListAsync(cancellationToken);

                rows = candidates
                    .Select(r => new { Receipt = r, Dist = BitOperations.PopCount((ulong)r.NumericPayload!.Value ^ target) })
                    .Where(x => x.Dist <= 4)
                    .OrderBy(x => x.Dist)
                    .ThenByDescending(x => x.Receipt.CreatedAt)
                    .Take(20)
                    .Select(x => x.Receipt)
                    .ToList();
            }
        }

        return await MapRowsAsync(rows, cancellationToken);
    }

    private async Task<List<ArtworkForensicTraceDto>> MapRowsAsync(
        List<ArtworkWatermarkReceipt> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return new List<ArtworkForensicTraceDto>();

        var commissionIds = rows
            .Where(r => r.CommissionId.HasValue)
            .Select(r => r.CommissionId!.Value)
            .Distinct()
            .ToList();

        var commissions = commissionIds.Count > 0
            ? await _db.Commissions
                .AsNoTracking()
                .Where(c => commissionIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Title, c.CreatorId })
                .ToDictionaryAsync(c => c.Id, cancellationToken)
            : null;

        var commissionCreatorIds = commissions?.Values
            .Select(c => c.CreatorId)
            .Distinct()
            .ToList() ?? new List<Guid>();

        var creatorNames = commissionCreatorIds.Count > 0
            ? await _db.CreatorProfiles
                .AsNoTracking()
                .Where(p => commissionCreatorIds.Contains(p.Id))
                .Select(p => new { p.Id, p.DisplayName })
                .ToDictionaryAsync(p => p.Id, p => p.DisplayName, cancellationToken)
            : null;

        var viewerIds = rows
            .Where(r => r.ViewerUserId.HasValue && (string.IsNullOrWhiteSpace(r.ViewerName) || string.IsNullOrWhiteSpace(r.ViewerEmail)))
            .Select(r => r.ViewerUserId!.Value)
            .Distinct()
            .ToList();

        var users = viewerIds.Count > 0
            ? await _db.Users
                .AsNoTracking()
                .Where(u => viewerIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName, u.UserName, u.Email })
                .ToDictionaryAsync(u => u.Id, cancellationToken)
            : null;

        return rows.Select(r =>
        {
            string title = r.Artwork?.Title
                ?? (r.CommissionId.HasValue && commissions != null && commissions.TryGetValue(r.CommissionId.Value, out var comm)
                    ? $"Đơn #{comm.Title}"
                    : r.FileName ?? "Không rõ tên");

            string creator = r.Artwork?.CreatorProfile?.DisplayName
                ?? (r.CommissionId.HasValue
                    && commissions != null
                    && commissions.TryGetValue(r.CommissionId.Value, out var cInfo)
                    && creatorNames != null
                    && creatorNames.TryGetValue(cInfo.CreatorId, out var cDisplay)
                        ? cDisplay
                        : "DIL Studio");

            string? viewerName = r.ViewerName;
            string? viewerEmail = r.ViewerEmail;
            if (r.ViewerUserId.HasValue && users != null && users.TryGetValue(r.ViewerUserId.Value, out var u))
            {
                viewerName ??= !string.IsNullOrWhiteSpace(u.FullName) ? u.FullName : u.UserName;
                viewerEmail ??= u.Email;
            }

            return new ArtworkForensicTraceDto(
                r.Id,
                r.ReceiptToken,
                r.NumericPayload,
                r.ArtworkId,
                title,
                creator,
                r.ViewerUserId,
                viewerEmail,
                viewerName,
                r.IpAddress,
                r.UserAgent,
                r.CreatedAt,
                r.Status,
                r.CommissionId,
                r.MilestoneId,
                r.WatermarkType,
                r.FileName,
                r.FileSha256
            );
        }).ToList();
    }
}
