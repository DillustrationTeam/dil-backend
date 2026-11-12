using System.Numerics;
using ArtCommission.Application.ArtistStudio.Moderation.DTOs;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Moderation.Queries;

/// <summary>
/// Kiểm tra trùng lặp tác phẩm dựa trên SHA-256, mã DWT thuỷ vân và VisualHash (dHash 64-bit).
/// </summary>
public record CheckArtworkDuplicatesQuery(
    Guid ArtworkId,
    long? FallbackVisualHash = null,
    string? FallbackSha256 = null,
    long? FallbackDwtPayload = null
) : IRequest<ArtworkDuplicateCheckResultDto?>;

public class CheckArtworkDuplicatesQueryHandler : IRequestHandler<CheckArtworkDuplicatesQuery, ArtworkDuplicateCheckResultDto?>
{
    private readonly IApplicationDbContext _db;

    public CheckArtworkDuplicatesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ArtworkDuplicateCheckResultDto?> Handle(
        CheckArtworkDuplicatesQuery request,
        CancellationToken cancellationToken)
    {
        var target = await _db.Artworks
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.ArtworkId && !a.IsDeleted, cancellationToken);

        if (target is null)
            return null;

        var targetReceipt = await _db.ArtworkWatermarkReceipts
            .AsNoTracking()
            .Where(r => r.ArtworkId == target.Id && r.WatermarkType == "DwtPublish")
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        long? targetVisual = targetReceipt?.VisualHash ?? request.FallbackVisualHash;
        string? targetSha = targetReceipt?.FileSha256 ?? request.FallbackSha256;
        long? targetDwt = targetReceipt?.NumericPayload ?? request.FallbackDwtPayload;

        // Nạp toàn bộ các tác phẩm khác chưa xoá cùng thông tin Creator
        var otherArtworks = await _db.Artworks
            .AsNoTracking()
            .Include(a => a.CreatorProfile)
            .Where(a => a.Id != target.Id && !a.IsDeleted && a.CreatorProfile != null && !a.CreatorProfile.IsDeleted)
            .Select(a => new
            {
                a.Id,
                a.Title,
                a.ThumbnailUrl,
                a.WatermarkedUrl,
                a.ImageUrl,
                a.CreatorProfileId,
                CreatorName = a.CreatorProfile!.DisplayName,
                a.ModerationStatus,
                a.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var receiptList = await _db.ArtworkWatermarkReceipts
            .AsNoTracking()
            .Where(r => r.ArtworkId.HasValue && r.ArtworkId != target.Id
                && (r.WatermarkType == "DwtPublish" || r.WatermarkType == "DwtForensic" || r.WatermarkType == "DwtCommissionFinal"))
            .OrderByDescending(r => r.CreatedAt)
            .Take(2000)
            .Select(r => new
            {
                ArtworkId = r.ArtworkId!.Value,
                r.ReceiptToken,
                r.WatermarkType,
                r.FileSha256,
                r.VisualHash,
                r.NumericPayload
            })
            .ToListAsync(cancellationToken);

        var receiptsByArtwork = receiptList
            .GroupBy(r => r.ArtworkId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var matches = new List<ArtworkDuplicateMatchDto>();

        foreach (var other in otherArtworks)
        {
            string matchType = string.Empty;
            int similarity = 0;
            int bestDist = 64;
            string? matchedToken = null;

            // 1. Trùng chính xác URL file gốc
            if (!string.IsNullOrWhiteSpace(target.ImageUrl)
                && string.Equals(other.ImageUrl, target.ImageUrl, StringComparison.OrdinalIgnoreCase))
            {
                matchType = "ExactFile";
                similarity = 100;
                bestDist = 0;
            }

            if (receiptsByArtwork.TryGetValue(other.Id, out var recs))
            {
                foreach (var rec in recs)
                {
                    // 2. Trùng SHA-256
                    if (!string.IsNullOrWhiteSpace(targetSha)
                        && string.Equals(rec.FileSha256, targetSha, StringComparison.OrdinalIgnoreCase))
                    {
                        matchType = "ExactFile";
                        similarity = 100;
                        bestDist = 0;
                        matchedToken = rec.ReceiptToken;
                        break;
                    }

                    // 3. Re-up ảnh có sẵn mã DWT thuỷ vân của hệ thống
                    if (targetDwt.HasValue && targetDwt.Value != 0 && rec.NumericPayload.HasValue && rec.NumericPayload.Value != 0)
                    {
                        int dwtPop = BitOperations.PopCount((ulong)targetDwt.Value);
                        if (dwtPop is >= 8 and <= 56)
                        {
                            int dwtDist = BitOperations.PopCount((ulong)targetDwt.Value ^ (ulong)rec.NumericPayload.Value);
                            if (dwtDist <= 4)
                            {
                                int sim = (int)Math.Round((64 - dwtDist) * 100.0 / 64.0);
                                if (sim > similarity)
                                {
                                    matchType = "ReuploadedWatermark";
                                    similarity = sim;
                                    bestDist = dwtDist;
                                    matchedToken = rec.ReceiptToken;
                                }
                            }
                        }
                    }

                    // 4. Đối chiếu tương đồng thị giác VisualHash (dHash)
                    if (targetVisual.HasValue && rec.VisualHash.HasValue)
                    {
                        int vDist = BitOperations.PopCount((ulong)targetVisual.Value ^ (ulong)rec.VisualHash.Value);
                        if (vDist <= 10)
                        {
                            int sim = (int)Math.Round((64 - vDist) * 100.0 / 64.0);
                            if (sim > similarity)
                            {
                                matchType = "VisualSimilar";
                                similarity = sim;
                                bestDist = vDist;
                                matchedToken = rec.ReceiptToken;
                            }
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(matchType))
            {
                matches.Add(new ArtworkDuplicateMatchDto(
                    other.Id,
                    other.Title,
                    other.WatermarkedUrl ?? other.ThumbnailUrl ?? other.ImageUrl,
                    other.WatermarkedUrl ?? other.ImageUrl,
                    other.CreatorProfileId,
                    other.CreatorName,
                    other.ModerationStatus,
                    other.CreatedAt,
                    matchType,
                    similarity,
                    bestDist,
                    other.CreatorProfileId == target.CreatorProfileId,
                    matchedToken));
            }
        }

        var ordered = matches
            .OrderBy(m => m.SameCreator) // Khác tác giả (nghi re-up/đạo tranh) lên đầu
            .ThenByDescending(m => m.SimilarityPercent)
            .ThenBy(m => m.HammingDistance)
            .ThenByDescending(m => m.CreatedAt)
            .Take(10)
            .ToList();

        return new ArtworkDuplicateCheckResultDto(
            target.Id,
            ordered.Count > 0,
            otherArtworks.Count,
            targetSha,
            targetVisual,
            ordered);
    }
}
