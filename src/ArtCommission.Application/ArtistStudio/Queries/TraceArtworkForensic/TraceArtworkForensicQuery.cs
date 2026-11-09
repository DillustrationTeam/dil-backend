using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Queries.TraceArtworkForensic;

public record TraceArtworkForensicQuery(
    string? ReceiptToken,
    long? NumericPayload,
    Guid? ArtworkId
) : IRequest<List<ArtworkForensicTraceDto>>;

public record ArtworkForensicTraceDto(
    Guid ReceiptId,
    string ReceiptToken,
    long? NumericPayload,
    Guid ArtworkId,
    string ArtworkTitle,
    string CreatorName,
    Guid? ViewerUserId,
    string? ViewerEmail,
    string? ViewerName,
    string IpAddress,
    string? UserAgent,
    DateTimeOffset ViewedAt,
    string Status
);

public class TraceArtworkForensicQueryHandler : IRequestHandler<TraceArtworkForensicQuery, List<ArtworkForensicTraceDto>>
{
    private readonly IApplicationDbContext _db;

    public TraceArtworkForensicQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<ArtworkForensicTraceDto>> Handle(TraceArtworkForensicQuery request, CancellationToken cancellationToken)
    {
        var query = _db.ArtworkWatermarkReceipts
            .Include(r => r.Artwork)
                .ThenInclude(a => a!.CreatorProfile)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.ReceiptToken))
        {
            var tokenClean = request.ReceiptToken.Trim();
            query = query.Where(r => r.ReceiptToken == tokenClean || r.ReceiptToken.Contains(tokenClean));
        }
        else if (request.NumericPayload.HasValue)
        {
            query = query.Where(r => r.NumericPayload == request.NumericPayload.Value);
        }
        else if (request.ArtworkId.HasValue)
        {
            query = query.Where(r => r.ArtworkId == request.ArtworkId.Value);
        }
        else
        {
            return new List<ArtworkForensicTraceDto>();
        }

        var results = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .Select(r => new ArtworkForensicTraceDto(
                r.Id,
                r.ReceiptToken,
                r.NumericPayload,
                r.ArtworkId,
                r.Artwork != null ? r.Artwork.Title : "Unknown",
                r.Artwork != null && r.Artwork.CreatorProfile != null ? r.Artwork.CreatorProfile.DisplayName : "Unknown",
                r.ViewerUserId,
                r.ViewerEmail,
                r.ViewerName,
                r.IpAddress,
                r.UserAgent,
                r.CreatedAt,
                r.Status
            ))
            .ToListAsync(cancellationToken);

        return results;
    }
}
