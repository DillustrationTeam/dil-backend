using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Commands.CreateAuction;

/// <summary>
/// UC32 — POST /api/v1/auctions
/// Người sở hữu tranh niêm yết tranh thành phiên đấu giá.
/// </summary>
public record CreateAuctionCommand(
    Guid UserId,
    Guid ArtworkId,
    decimal StartPrice,
    decimal? ReservePrice,
    decimal BidStep,
    decimal? BuyNowPrice,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt
) : IRequest<(bool Success, AuctionDto? Data, string[] Errors)>;

public class CreateAuctionCommandValidator : AbstractValidator<CreateAuctionCommand>
{
    /// <summary>Thời lượng tối thiểu của một phiên — ngắn hơn thì không ai kịp đặt giá.</summary>
    public const int MinimumDurationHours = 1;

    /// <summary>Thời lượng tối đa — chặn phiên treo vô thời hạn làm đóng băng tranh.</summary>
    public const int MaximumDurationDays = 30;

    public CreateAuctionCommandValidator()
    {
        RuleFor(x => x.ArtworkId).NotEmpty().WithMessage("Thiếu tranh cần đấu giá.");

        RuleFor(x => x.StartPrice)
            .GreaterThan(0).WithMessage("Giá khởi điểm phải lớn hơn 0.");

        RuleFor(x => x.BidStep)
            .GreaterThan(0).WithMessage("Bước giá phải lớn hơn 0.");

        RuleFor(x => x.ReservePrice)
            .GreaterThanOrEqualTo(x => x.StartPrice)
            .When(x => x.ReservePrice.HasValue)
            .WithMessage("Giá sàn không được thấp hơn giá khởi điểm.");

        RuleFor(x => x.BuyNowPrice)
            .GreaterThan(x => x.StartPrice)
            .When(x => x.BuyNowPrice.HasValue)
            .WithMessage("Giá mua ngay phải cao hơn giá khởi điểm.");

        RuleFor(x => x.EndAt)
            .GreaterThan(x => x.StartAt)
            .WithMessage("Thời điểm kết thúc phải sau thời điểm bắt đầu.");

        RuleFor(x => x)
            .Must(x => x.EndAt - x.StartAt >= TimeSpan.FromHours(MinimumDurationHours))
            .WithMessage($"Phiên đấu giá phải kéo dài ít nhất {MinimumDurationHours} giờ.");

        RuleFor(x => x)
            .Must(x => x.EndAt - x.StartAt <= TimeSpan.FromDays(MaximumDurationDays))
            .WithMessage($"Phiên đấu giá tối đa {MaximumDurationDays} ngày.");
    }
}

public class CreateAuctionCommandHandler
    : IRequestHandler<CreateAuctionCommand, (bool, AuctionDto?, string[])>
{
    private readonly IApplicationDbContext _db;

    public CreateAuctionCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, AuctionDto?, string[])> Handle(
        CreateAuctionCommand request,
        CancellationToken cancellationToken)
    {
        var validation = new CreateAuctionCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var artwork = await _db.Artworks.FirstOrDefaultAsync(
            a => a.Id == request.ArtworkId && !a.IsDeleted,
            cancellationToken);

        if (artwork is null)
        {
            return (false, null, ["Không tìm thấy tranh."]);
        }

        // Quyền sở hữu: chấp nhận CẢ HAI nguồn để không chặn nhầm người bán hợp lệ.
        //   - CreatorProfile của chính user (tranh do mình tạo), hoặc
        //   - dòng ArtworkOwnership IsCurrent = 1 trỏ tới user (tranh đã mua lại).
        var ownsArtwork = await IsArtworkOwnerAsync(request.UserId, artwork, cancellationToken);
        if (!ownsArtwork)
        {
            return (false, null, ["Bạn không phải chủ sở hữu hiện tại của tranh này."]);
        }

        // Một tranh không được nằm trong 2 phiên còn hiệu lực — nếu không, cùng một
        // tranh có thể được bán cho 2 người ở 2 phiên chồng nhau.
        var hasLiveAuction = await _db.Auctions.AnyAsync(
            a => a.ArtworkId == request.ArtworkId
                 && !a.IsDeleted
                 && (a.Status == AuctionStatus.Scheduled
                     || a.Status == AuctionStatus.Active
                     || a.Status == AuctionStatus.Ended),
            cancellationToken);

        if (hasLiveAuction)
        {
            return (false, null, ["Tranh này đang có phiên đấu giá chưa kết thúc."]);
        }

        var now = DateTimeOffset.UtcNow;

        var auction = new Domain.Entities.Auction.Auction
        {
            ArtworkId = artwork.Id,
            SellerId = request.UserId,
            AuctionType = request.BuyNowPrice.HasValue ? AuctionType.BuyNow : AuctionType.Standard,
            StartPrice = request.StartPrice,
            ReservePrice = request.ReservePrice,
            BidStep = request.BidStep,
            BuyNowPrice = request.BuyNowPrice,
            // Giá hiện tại khởi tạo bằng giá khởi điểm; mọi bid so với trường này.
            CurrentPrice = request.StartPrice,
            BidCount = 0,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            // StartAt ở quá khứ nghĩa là mở ngay — tránh phiên "Scheduled" mà không bao giờ Active.
            Status = request.StartAt <= now ? AuctionStatus.Active : AuctionStatus.Scheduled
        };

        _db.Auctions.Add(auction);

        // Ghi nhận chủ sở hữu nếu tranh chưa có dòng ownership hiện tại nào.
        // Không ghi đè khi đã có — sẽ đụng unique index một-chủ-sở-hữu-hiện-tại.
        await EnsureContributorOwnerRowAsync(request.UserId, artwork, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        // Chỉ trả file gốc cho chính người bán.
        return (true, AuctionMapper.ToDto(auction, artwork.ImageUrl), []);
    }

    /// <summary>
    /// Kiểm tra user có quyền bán tranh: là chủ profile đã tạo tranh, hoặc là
    /// chủ sở hữu hiện tại theo sổ ArtworkOwnership.
    /// </summary>
    private async Task<bool> IsArtworkOwnerAsync(
        Guid userId,
        Domain.Entities.ArtistStudio.Artwork artwork,
        CancellationToken cancellationToken)
    {
        var isCreatorOfArtwork = await _db.CreatorProfiles.AnyAsync(
            p => p.Id == artwork.CreatorProfileId && p.UserId == userId && !p.IsDeleted,
            cancellationToken);

        if (isCreatorOfArtwork)
        {
            return true;
        }

        return await _db.ArtworkOwnerships.AnyAsync(
            o => o.ArtworkId == artwork.Id && o.OwnerId == userId && o.IsCurrent && !o.IsDeleted,
            cancellationToken);
    }

    /// <summary>
    /// Bảo đảm người tạo tranh cũng có dòng sở hữu. Chỉ thêm khi chưa có dòng hiện tại
    /// nào trỏ tới chính họ — nếu không sẽ đụng unique index một-chủ-sở-hữu.
    /// </summary>
    private async Task EnsureContributorOwnerRowAsync(
        Guid userId,
        Domain.Entities.ArtistStudio.Artwork artwork,
        CancellationToken cancellationToken)
    {
        var alreadyOwner = await _db.ArtworkOwnerships.AnyAsync(
            o => o.ArtworkId == artwork.Id && o.OwnerId == userId && o.IsCurrent && !o.IsDeleted,
            cancellationToken);

        if (alreadyOwner)
        {
            return;
        }

        var hasCurrentOwner = await _db.ArtworkOwnerships.AnyAsync(
            o => o.ArtworkId == artwork.Id && o.IsCurrent && !o.IsDeleted,
            cancellationToken);

        if (hasCurrentOwner)
        {
            return;
        }

        _db.ArtworkOwnerships.Add(new ArtworkOwnership
        {
            ArtworkId = artwork.Id,
            OwnerId = userId,
            AcquiredAt = DateTimeOffset.UtcNow,
            TransferReason = OwnershipTransferReason.AdminAdjustment,
            IsCurrent = true
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
