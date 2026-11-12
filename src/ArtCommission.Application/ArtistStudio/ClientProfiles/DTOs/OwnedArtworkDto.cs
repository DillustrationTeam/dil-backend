namespace ArtCommission.Application.ArtistStudio.ClientProfiles.DTOs;

/// <summary>
/// DTO đại diện cho một tác phẩm nghệ thuật thuộc quyền sở hữu của Client
/// (thu thập từ Đấu giá thành công, Đơn đặt vẽ hoàn tất, hoặc Trao đổi tranh Art Trade).
/// </summary>
public sealed record OwnedArtworkDto(
    Guid Id,
    string Title,
    string? ThumbnailUrl,
    string ImageUrl,
    string SourceType, // "Auction" | "Commission" | "Trade"
    Guid SourceId,
    string SourceDetailUrl,
    Guid CreatorId,
    string CreatorName,
    string? CreatorAvatarUrl,
    DateTimeOffset AcquiredAt,
    decimal? Price,
    string? CertificateNumber,
    bool HasMasterFile,
    string? DownloadUrl,
    string ExclusiveBadge
);

