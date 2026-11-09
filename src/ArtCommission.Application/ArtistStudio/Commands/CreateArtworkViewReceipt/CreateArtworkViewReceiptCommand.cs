using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.ArtistStudio;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Commands.CreateArtworkViewReceipt;

public record CreateArtworkViewReceiptCommand(
    Guid ArtworkId,
    Guid? ViewerUserId,
    string? ViewerEmail,
    string? ViewerName,
    string IpAddress,
    string? UserAgent,
    string? SessionId
) : IRequest<ArtworkViewReceiptDto>;

public record ArtworkViewReceiptDto(
    Guid ReceiptId,
    string ReceiptToken,
    string WatermarkedImageUrl,
    string DisplayTitle,
    string CreatorName,
    DateTimeOffset CreatedAt,
    string OverlayMaskText
);

public class CreateArtworkViewReceiptCommandHandler : IRequestHandler<CreateArtworkViewReceiptCommand, ArtworkViewReceiptDto>
{
    private readonly IApplicationDbContext _db;

    public CreateArtworkViewReceiptCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ArtworkViewReceiptDto> Handle(CreateArtworkViewReceiptCommand request, CancellationToken cancellationToken)
    {
        var artwork = await _db.Artworks
            .Include(a => a.CreatorProfile)
            .FirstOrDefaultAsync(a => a.Id == request.ArtworkId && !a.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException($"Artwork with ID {request.ArtworkId} not found.");

        // Sinh ReceiptToken ngẫu nhiên bảo mật 12 bytes hex (VD: WR-9F3A1C...)
        var randomBytes = RandomNumberGenerator.GetBytes(12);
        var tokenHex = "WR-" + Convert.ToHexString(randomBytes);

        // Sinh NumericPayload 64-bit cho dải tần DWT
        var numericPayload = BitConverter.ToInt64(RandomNumberGenerator.GetBytes(8));

        var receipt = new ArtworkWatermarkReceipt
        {
            ArtworkId = artwork.Id,
            ReceiptToken = tokenHex,
            NumericPayload = numericPayload,
            ViewerUserId = request.ViewerUserId,
            ViewerEmail = request.ViewerEmail,
            ViewerName = request.ViewerName,
            IpAddress = string.IsNullOrWhiteSpace(request.IpAddress) ? "127.0.0.1" : request.IpAddress,
            UserAgent = request.UserAgent,
            SessionId = request.SessionId,
            WatermarkType = "DwtForensic",
            Status = "Active"
        };

        _db.ArtworkWatermarkReceipts.Add(receipt);
        artwork.ViewCount += 1;
        await _db.SaveChangesAsync(cancellationToken);

        var creatorName = artwork.CreatorProfile?.DisplayName ?? "Creator";
        var overlayMask = $"{creatorName} • {tokenHex} • {receipt.CreatedAt:yyyy-MM-dd HH:mm}";

        var isOriginalGif = !string.IsNullOrWhiteSpace(artwork.ImageUrl) &&
                            artwork.ImageUrl.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
        var isWatermarkedGif = !string.IsNullOrWhiteSpace(artwork.WatermarkedUrl) &&
                               artwork.WatermarkedUrl.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);

        var protectedUrl = (isOriginalGif && !isWatermarkedGif)
            ? artwork.ImageUrl
            : (!string.IsNullOrWhiteSpace(artwork.WatermarkedUrl) ? artwork.WatermarkedUrl : artwork.ImageUrl);

        return new ArtworkViewReceiptDto(
            receipt.Id,
            receipt.ReceiptToken,
            protectedUrl,
            artwork.Title,
            creatorName,
            receipt.CreatedAt,
            overlayMask
        );
    }
}
