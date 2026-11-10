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

        // Sinh NumericPayload 64-bit khác 0 cho dải tần DWT
        long numericPayload;
        do
        {
            numericPayload = BitConverter.ToInt64(RandomNumberGenerator.GetBytes(8));
        } while (numericPayload == 0);

        var receipt = new ArtworkWatermarkReceipt
        {
            ArtworkId = artwork.Id,
            ReceiptToken = tokenHex,
            NumericPayload = numericPayload,
            FileName = $"{artwork.Title}.png",
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

        var protectedUrl = $"/api/v1/artworks/{artwork.Id}/protected-image?token={Uri.EscapeDataString(receipt.ReceiptToken)}";

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
