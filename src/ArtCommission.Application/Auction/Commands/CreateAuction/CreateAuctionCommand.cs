using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Enums;
using System.Data;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
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
) : IRequest<(bool Success, AuctionDto? Data, string[] Errors, bool Conflict)>;

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

        RuleFor(x => x.EndAt)
            .Must(endAt => endAt > DateTimeOffset.UtcNow)
            .WithMessage("Thời điểm kết thúc phải nằm trong tương lai.");

        RuleFor(x => x)
            .Must(x => x.EndAt - x.StartAt >= TimeSpan.FromHours(MinimumDurationHours))
            .WithMessage($"Phiên đấu giá phải kéo dài ít nhất {MinimumDurationHours} giờ.");

        RuleFor(x => x)
            .Must(x => x.EndAt - x.StartAt <= TimeSpan.FromDays(MaximumDurationDays))
            .WithMessage($"Phiên đấu giá tối đa {MaximumDurationDays} ngày.");
    }
}

public class CreateAuctionCommandHandler
    : IRequestHandler<CreateAuctionCommand, (bool, AuctionDto?, string[], bool)>
{
    private readonly IApplicationDbContext _db;
    private readonly IAuctionRealtimePublisher _realtime;
    private readonly IAuctionLifecycleNotifier _lifecycleNotifier;
    private readonly ILogger<CreateAuctionCommandHandler> _logger;

    public CreateAuctionCommandHandler(
        IApplicationDbContext db,
        IAuctionRealtimePublisher realtime,
        IAuctionLifecycleNotifier lifecycleNotifier,
        ILogger<CreateAuctionCommandHandler> logger)
    {
        _db = db;
        _realtime = realtime;
        _lifecycleNotifier = lifecycleNotifier;
        _logger = logger;
    }

    public async Task<(bool, AuctionDto?, string[], bool)> Handle(
        CreateAuctionCommand request,
        CancellationToken cancellationToken)
    {
        var validation = new CreateAuctionCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray(), false);
        }

        Domain.Entities.ArtistStudio.Artwork artwork;
        Domain.Entities.Auction.Auction auction;
        try
        {
            // Khóa hàng Artwork đến lúc commit. SERIALIZABLE giữ nhất quán cho các
            // kiểm tra ownership/auction; UPDLOCK khiến request cùng artwork xếp hàng,
            // kể cả khi backend chạy nhiều instance.
            var isSqlServer = string.Equals(
                _db.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.SqlServer",
                StringComparison.Ordinal);
            var usesInMemoryProvider = string.Equals(
                _db.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal);
            await using (var transaction = usesInMemoryProvider
                ? null
                : await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
            {
                IQueryable<Domain.Entities.ArtistStudio.Artwork> artworkQuery = _db.Artworks;
                if (isSqlServer)
                {
                    artworkQuery = _db.Artworks.FromSqlInterpolated(
                        $"SELECT * FROM [Artworks] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {request.ArtworkId}");
                }

                var loadedArtwork = await artworkQuery.FirstOrDefaultAsync(
                    a => a.Id == request.ArtworkId && !a.IsDeleted,
                    cancellationToken);

                if (loadedArtwork is null)
                {
                    return (false, null, ["Không tìm thấy tranh."], false);
                }

                artwork = loadedArtwork;
                var ownsArtwork = await IsArtworkOwnerAsync(request.UserId, artwork, cancellationToken);
                if (!ownsArtwork)
                {
                    return (false, null, ["Bạn không phải chủ sở hữu hiện tại của tranh này."], false);
                }

                // Tính cả phiên Ended đang chờ settlement; tranh chỉ được niêm yết lại
                // sau khi settlement hoàn tất hoặc phiên bị hủy.
                var hasLiveAuction = await _db.Auctions.AnyAsync(
                    a => a.ArtworkId == request.ArtworkId
                         && !a.IsDeleted
                         && (a.Status == AuctionStatus.Scheduled
                             || a.Status == AuctionStatus.Active
                             || a.Status == AuctionStatus.Ended),
                    cancellationToken);

                if (hasLiveAuction)
                {
                    return (false, null, ["Tranh này đang có phiên đấu giá chưa kết thúc."], true);
                }

                var now = DateTimeOffset.UtcNow;
                auction = new Domain.Entities.Auction.Auction
                {
                    ArtworkId = artwork.Id,
                    SellerId = request.UserId,
                    AuctionType = request.BuyNowPrice.HasValue ? AuctionType.BuyNow : AuctionType.Standard,
                    StartPrice = request.StartPrice,
                    ReservePrice = request.ReservePrice,
                    BidStep = request.BidStep,
                    BuyNowPrice = request.BuyNowPrice,
                    CurrentPrice = request.StartPrice,
                    BidCount = 0,
                    StartAt = request.StartAt,
                    EndAt = request.EndAt,
                    Status = request.StartAt <= now ? AuctionStatus.Active : AuctionStatus.Scheduled
                };

                _db.Auctions.Add(auction);
                await EnsureContributorOwnerRowAsync(request.UserId, artwork, cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
            }
        }
        catch (Exception ex) when (AuctionConcurrency.IsExpectedConflict(ex))
        {
            _logger.LogInformation(ex,
                "Tạo auction tranh chấp khi niêm yết artworkId={ArtworkId}, sellerId={SellerId}.",
                request.ArtworkId, request.UserId);
            return (false, null, ["Tranh hoặc quyền sở hữu vừa được cập nhật. Hãy tải lại và thử lại."], true);
        }

        if (auction.Status == AuctionStatus.Active)
        {
            try
            {
                await _lifecycleNotifier.NotifyParticipantsAsync(
                    auction.Id, AuctionLifecycleEvent.Started, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                // Commit đã thành công; lỗi notification không được biến thành lỗi API
                // khiến client gửi lại request.
                _logger.LogError(ex, "Không gửi được thông báo bắt đầu auctionId={AuctionId}.", auction.Id);
            }

            try
            {
                await _realtime.PublishAuctionStatusChangedAsync(
                    auction.Id, auction.Status.ToString(), auction.CurrentPrice, auction.EndAt, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không broadcast được phiên vừa tạo auctionId={AuctionId}.", auction.Id);
            }
        }

        // Chỉ trả file gốc cho chính người bán.
        return (true, AuctionMapper.ToDto(auction, artwork.ImageUrl), [], false);
    }

    /// <summary>
    /// Sổ ArtworkOwnership là nguồn sự thật nếu đã có chủ sở hữu hiện hành.
    /// Creator profile chỉ là fallback cho tranh cũ chưa có dòng ownership hiện hành.
    /// </summary>
    private async Task<bool> IsArtworkOwnerAsync(
        Guid userId,
        Domain.Entities.ArtistStudio.Artwork artwork,
        CancellationToken cancellationToken)
    {
        var currentOwnerId = await _db.ArtworkOwnerships
            .Where(o => o.ArtworkId == artwork.Id && o.IsCurrent && !o.IsDeleted)
            .Select(o => (Guid?)o.OwnerId)
            .FirstOrDefaultAsync(cancellationToken);

        if (currentOwnerId.HasValue)
        {
            return currentOwnerId.Value == userId;
        }

        return await _db.CreatorProfiles.AnyAsync(
            p => p.Id == artwork.CreatorProfileId && p.UserId == userId && !p.IsDeleted,
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

    }
}
