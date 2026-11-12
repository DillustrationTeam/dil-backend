using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArtCommission.Application.ArtistStudio.ClientProfiles.DTOs;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.ClientProfiles.Queries.GetClientOwnedArtworks;

public sealed record GetClientOwnedArtworksQuery(
    Guid UserId
) : IRequest<(bool Success, IReadOnlyList<OwnedArtworkDto>? Data, string[] Errors)>;

public class GetClientOwnedArtworksQueryHandler
    : IRequestHandler<GetClientOwnedArtworksQuery, (bool Success, IReadOnlyList<OwnedArtworkDto>? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly IStorageService _storageService;
    private readonly ICommissionService? _commissionService;

    public GetClientOwnedArtworksQueryHandler(
        IApplicationDbContext db,
        IStorageService storageService,
        ICommissionService? commissionService = null)
    {
        _db = db;
        _storageService = storageService;
        _commissionService = commissionService;
    }

    public async Task<(bool Success, IReadOnlyList<OwnedArtworkDto>? Data, string[] Errors)> Handle(
        GetClientOwnedArtworksQuery request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return (false, null, ["Yêu cầu đăng nhập để xem tác phẩm đã sở hữu."]);
        }

        var results = new List<OwnedArtworkDto>();

        // ==========================================
        // 1. AUCTIONS (Tác phẩm thắng đấu giá / BuyNow)
        // ==========================================
        try
        {
            var ownerships = await _db.ArtworkOwnerships
                .AsNoTracking()
                .Where(o => o.OwnerId == request.UserId && o.IsCurrent && !o.IsDeleted)
                .OrderByDescending(o => o.AcquiredAt)
                .ToListAsync(cancellationToken);

            if (ownerships.Count > 0)
            {
                var artworkIds = ownerships.Select(o => o.ArtworkId).Distinct().ToList();
                var auctionIds = ownerships.Where(o => o.AuctionId.HasValue).Select(o => o.AuctionId!.Value).Distinct().ToList();

                var artworks = await _db.Artworks
                    .AsNoTracking()
                    .Include(a => a.CreatorProfile)
                    .Where(a => artworkIds.Contains(a.Id) && !a.IsDeleted)
                    .ToDictionaryAsync(a => a.Id, cancellationToken);

                var auctions = await _db.Auctions
                    .AsNoTracking()
                    .Where(a => auctionIds.Contains(a.Id))
                    .ToDictionaryAsync(a => a.Id, cancellationToken);

                var deliverables = await _db.Deliverables
                    .AsNoTracking()
                    .Where(d => auctionIds.Contains(d.AuctionId) && !d.IsDeleted)
                    .Select(d => d.AuctionId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                var deliverableSet = deliverables.ToHashSet();

                var creatorProfileIds = artworks.Values
                    .Select(a => a.CreatorProfileId)
                    .Distinct()
                    .ToList();

                var creatorProfiles = await _db.CreatorProfiles
                    .AsNoTracking()
                    .Include(cp => cp.User)
                    .Where(cp => creatorProfileIds.Contains(cp.Id))
                    .ToDictionaryAsync(cp => cp.Id, cancellationToken);

                foreach (var o in ownerships)
                {
                    if (!artworks.TryGetValue(o.ArtworkId, out var artwork))
                    {
                        continue;
                    }

                    auctions.TryGetValue(o.AuctionId ?? Guid.Empty, out var auction);

                    creatorProfiles.TryGetValue(artwork.CreatorProfileId, out var cp);
                    var creatorName = cp?.User?.FullName ?? cp?.DisplayName ?? artwork.CreatorProfile?.DisplayName ?? "Dillustration Artist";
                    var creatorAvatar = cp?.User?.AvatarUrl;

                    var certNumber = o.AuctionId.HasValue
                        ? $"DIL-CERT-{o.AcquiredAt:yyyyMMdd}-{o.AuctionId.Value.ToString()[..8].ToUpperInvariant()}"
                        : $"DIL-CERT-OWN-{o.Id.ToString()[..8].ToUpperInvariant()}";

                    var hasMaster = o.AuctionId.HasValue && deliverableSet.Contains(o.AuctionId.Value);

                    results.Add(new OwnedArtworkDto(
                        Id: artwork.Id,
                        Title: artwork.Title,
                        ThumbnailUrl: artwork.ThumbnailUrl ?? artwork.WatermarkedUrl ?? artwork.ImageUrl,
                        ImageUrl: artwork.WatermarkedUrl ?? artwork.ImageUrl,
                        SourceType: "Auction",
                        SourceId: o.AuctionId ?? artwork.Id,
                        SourceDetailUrl: o.AuctionId.HasValue ? $"/auctions/{o.AuctionId.Value}" : $"/artworks/{artwork.Id}",
                        CreatorId: artwork.CreatorProfileId,
                        CreatorName: creatorName,
                        CreatorAvatarUrl: creatorAvatar,
                        AcquiredAt: o.AcquiredAt,
                        Price: auction?.FinalPrice,
                        CertificateNumber: certNumber,
                        HasMasterFile: hasMaster,
                        DownloadUrl: null,
                        ExclusiveBadge: "Exclusive Owner"
                    ));
                }
            }
        }
        catch
        {
            // Do not fail entire endpoint if one source has issues
        }

        // ==========================================
        // 2. COMMISSIONS (Đơn đặt vẽ đã hoàn tất nghiệm thu)
        // ==========================================
        try
        {
            var commissions = await _db.Commissions
                .AsNoTracking()
                .Include(c => c.Milestones)
                .Where(c => c.ClientId == request.UserId && !c.IsDeleted && c.Status == CommissionStatus.Completed)
                .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
                .ToListAsync(cancellationToken);

            if (commissions.Count > 0)
            {
                var creatorUserIds = commissions.Select(c => c.CreatorId).Distinct().ToList();
                var creatorUsers = await _db.Users
                    .AsNoTracking()
                    .Where(u => creatorUserIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, cancellationToken);

                foreach (var c in commissions)
                {
                    creatorUsers.TryGetValue(c.CreatorId, out var creatorUser);
                    var creatorName = creatorUser?.FullName ?? "Artist";
                    var creatorAvatar = creatorUser?.AvatarUrl;

                    // Lấy milestone hoàn thiện (approved gần nhất hoặc có deliverable)
                    var finalMilestone = c.Milestones
                        .Where(m => m.Status == MilestoneStatus.Approved)
                        .OrderByDescending(m => m.Sequence)
                        .FirstOrDefault()
                        ?? c.Milestones.OrderByDescending(m => m.Sequence).FirstOrDefault();

                    var previewUrl = finalMilestone?.WipPreviewUrl
                        ?? finalMilestone?.WatermarkedUrl
                        ?? finalMilestone?.OriginalWipUrl;

                    string? downloadUrl = null;
                    if (!string.IsNullOrEmpty(finalMilestone?.FinalDeliverableUrl))
                    {
                        try
                        {
                            if (finalMilestone.FinalDeliverableUrl.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                            {
                                if (_commissionService is not null)
                                {
                                    var finalDownload = await _commissionService.GetFinalDownloadUrlAsync(c.Id, request.UserId, cancellationToken);
                                    downloadUrl = finalDownload.DownloadUrl;
                                }
                            }
                            else
                            {
                                downloadUrl = _storageService.GeneratePresignedDownloadUrl(finalMilestone.FinalDeliverableUrl, TimeSpan.FromHours(24));
                            }
                        }
                        catch
                        {
                            // fallback
                        }
                    }

                    var acquiredDate = c.UpdatedAt ?? c.CreatedAt;
                    var certNumber = $"DIL-COMM-{acquiredDate:yyyyMMdd}-{c.Id.ToString()[..8].ToUpperInvariant()}";

                    results.Add(new OwnedArtworkDto(
                        Id: c.Id,
                        Title: c.Title,
                        ThumbnailUrl: previewUrl,
                        ImageUrl: previewUrl ?? "",
                        SourceType: "Commission",
                        SourceId: c.Id,
                        SourceDetailUrl: $"/workroom?commissionId={c.Id}",
                        CreatorId: c.CreatorId,
                        CreatorName: creatorName,
                        CreatorAvatarUrl: creatorAvatar,
                        AcquiredAt: acquiredDate,
                        Price: c.FinalPrice > 0 ? c.FinalPrice : c.TotalPrice,
                        CertificateNumber: certNumber,
                        HasMasterFile: !string.IsNullOrEmpty(downloadUrl) || !string.IsNullOrEmpty(finalMilestone?.FinalDeliverableUrl),
                        DownloadUrl: downloadUrl,
                        ExclusiveBadge: "Commission Deliverable"
                    ));
                }
            }
        }
        catch
        {
            // Ignore failure on commission query
        }

        // ==========================================
        // 3. ART TRADES (Giao dịch trao đổi tranh hoàn tất)
        // ==========================================
        try
        {
            var trades = await _db.ArtTrades
                .AsNoTracking()
                .Include(t => t.Initiator)
                .Include(t => t.Receiver)
                .Include(t => t.InitiatorArtwork)
                .Include(t => t.ReceiverArtwork)
                .Where(t => (t.InitiatorId == request.UserId || t.ReceiverId == request.UserId)
                            && !t.IsDeleted
                            && t.Status == TradeStatus.Completed)
                .OrderByDescending(t => t.CompletedAt ?? t.UpdatedAt ?? t.CreatedAt)
                .ToListAsync(cancellationToken);

            foreach (var t in trades)
            {
                var isInitiator = t.InitiatorId == request.UserId;
                var partner = isInitiator ? t.Receiver : t.Initiator;
                var partnerArtwork = isInitiator ? t.ReceiverArtwork : t.InitiatorArtwork;
                var partnerPreviewUrl = isInitiator ? t.ReceiverPreviewUrl : t.InitiatorPreviewUrl;
                var partnerOriginalKey = isInitiator ? t.ReceiverOriginalFileUrl : t.InitiatorOriginalFileUrl;

                var cleanUrl = partnerPreviewUrl;
                string? downloadUrl = null;
                if (!string.IsNullOrEmpty(partnerOriginalKey))
                {
                    try
                    {
                        downloadUrl = _storageService.GeneratePresignedDownloadUrl(partnerOriginalKey, TimeSpan.FromHours(24));
                        cleanUrl = downloadUrl;
                    }
                    catch
                    {
                        // fallback
                    }
                }

                var acquiredDate = t.CompletedAt ?? t.UpdatedAt ?? t.CreatedAt;
                var certNumber = $"DIL-TRADE-{acquiredDate:yyyyMMdd}-{t.Id.ToString()[..8].ToUpperInvariant()}";

                var tradeTitle = partnerArtwork?.Title ?? t.Title;

                results.Add(new OwnedArtworkDto(
                    Id: t.Id,
                    Title: tradeTitle,
                    ThumbnailUrl: cleanUrl,
                    ImageUrl: cleanUrl ?? partnerArtwork?.ImageUrl ?? "",
                    SourceType: "Trade",
                    SourceId: t.Id,
                    SourceDetailUrl: $"/art-trades/{t.Id}",
                    CreatorId: partner?.Id ?? Guid.Empty,
                    CreatorName: partner?.FullName ?? "Trade Partner",
                    CreatorAvatarUrl: partner?.AvatarUrl,
                    AcquiredAt: acquiredDate,
                    Price: null, // Peer-to-peer exchange (no monetary price)
                    CertificateNumber: certNumber,
                    HasMasterFile: !string.IsNullOrEmpty(downloadUrl) || !string.IsNullOrEmpty(partnerOriginalKey),
                    DownloadUrl: downloadUrl,
                    ExclusiveBadge: "Art Trade Exchange"
                ));
            }
        }
        catch
        {
            // Ignore failure on trade query
        }

        // Sort all by AcquiredAt descending
        var sorted = results.OrderByDescending(x => x.AcquiredAt).ToList();

        return (true, sorted, []);
    }
}

