using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Auction.DTOs;

// =====================================================================
// Auction (UC32–UC35)
// =====================================================================

/// <summary>Tóm tắt tranh gắn trong phiên — chỉ trường cần cho danh sách/chi tiết.</summary>
public sealed record AuctionArtworkDto(
    Guid ArtworkId,
    string ArtworkTitle,
    string? ThumbnailUrl,
    string? ImageUrl,
    string? Style
);

public sealed record AuctionSellerDto(
    Guid UserId,
    string? FullName
);

/// <summary>Phiên đấu giá trả về sau khi tạo/sửa — khớp hợp đồng POST /auctions.</summary>
public sealed record AuctionDto(
    Guid AuctionId,
    Guid ArtworkId,
    Guid SellerId,
    string AuctionType,
    decimal StartPrice,
    decimal? ReservePrice,
    decimal BidStep,
    decimal? BuyNowPrice,
    decimal CurrentPrice,
    int BidCount,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string AuctionStatus,
    Guid? WinnerId,
    decimal? FinalPrice,
    DateTimeOffset? SettledAt,
    DateTimeOffset? PaymentDeadline,
    string? CancelReason,
    /// <summary>Tên file gốc bàn giao cho winner. Null khi người gọi không đủ quyền xem.</summary>
    string? OriginalFileUrl
);

/// <summary>Một dòng trong danh sách chợ đấu giá (GET /auctions).</summary>
public sealed record AuctionListItemDto(
    Guid AuctionId,
    AuctionArtworkDto? Artwork,
    decimal CurrentPrice,
    int BidCount,
    DateTimeOffset EndAt,
    string AuctionStatus,
    int WatchCount
);

/// <summary>Chi tiết phiên kèm lịch sử bid gần nhất (GET /auctions/{id}).</summary>
public sealed record AuctionDetailDto(
    Guid AuctionId,
    AuctionArtworkDto? Artwork,
    AuctionSellerDto? Seller,
    string AuctionType,
    decimal StartPrice,
    decimal? ReservePrice,
    decimal? BuyNowPrice,
    decimal BidStep,
    decimal CurrentPrice,
    int BidCount,
    int WatchCount,
    Guid? WinnerId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string AuctionStatus,
    DateTimeOffset? PaymentDeadline,
    /// <summary>Người gọi hiện tại có đang theo dõi phiên không.</summary>
    bool IsWatching,
    /// <summary>Người dẫn đầu hiện tại.</summary>
    Guid? LeadingBidderId
);

/// <summary>
/// Người đặt giá, trả dạng object lồng theo đặc tả `bidder: { userId, fullName }`.
/// </summary>
public sealed record BidderDto(
    Guid UserId,
    string? FullName
);

public sealed record BidDto(
    Guid BidId,
    Guid AuctionId,
    /// <summary>Giữ lại ID phẳng để tương thích; object `bidder` mới là dạng đặc tả yêu cầu.</summary>
    Guid BidderId,
    string? BidderFullName,
    /// <summary>Dạng lồng mà đặc tả API ghi: <c>bidder: { userId, fullName }</c>.</summary>
    BidderDto? Bidder,
    decimal Amount,
    decimal HoldAmount,
    string BidStatus,
    string HoldStatus,
    DateTimeOffset PlacedAt
);

/// <summary>
/// Số dư ví trả kèm sau một thao tác tiền, để FE cập nhật ngay không phải gọi thêm.
/// Tách thành object riêng vì đặc tả API ghi rõ `wallet: { balance, lockedBalance }` —
/// trả phẳng hai trường ở cấp ngoài sẽ lệch hợp đồng mà FE đang đọc.
/// </summary>
public sealed record WalletSnapshotDto(
    decimal Balance,
    decimal LockedBalance
);

/// <summary>Kết quả đặt giá — trả kèm số dư ví.</summary>
public sealed record PlaceBidResultDto(
    BidDto Bid,
    WalletSnapshotDto Wallet
);

/// <summary>Kết quả theo dõi / bỏ theo dõi.</summary>
public sealed record AuctionWatchDto(
    Guid? AuctionWatchId,
    Guid AuctionId,
    Guid UserId,
    bool IsWatching
);

/// <summary>Kết quả chốt phiên (POST /auctions/{id}/settle).</summary>
public sealed record AuctionSettlementDto(
    Guid AuctionId,
    Guid? WinnerId,
    decimal FinalPrice,
    DateTimeOffset SettledAt,
    Guid? ArtworkOwnershipId,
    Guid? EscrowTransactionId
);

/// <summary>Trạng thái thanh toán của winner (GET /auctions/{id}/settlement).</summary>
public sealed record AuctionSettlementStatusDto(
    Guid AuctionId,
    Guid? WinnerId,
    decimal FinalPrice,
    DateTimeOffset? SettledAt,
    string? PaymentOrderStatus,
    Guid? ArtworkOwnershipId,
    DateTimeOffset? PaymentDeadline,
    decimal? EscrowHeldAmount
);

/// <summary>Link tải file gốc có hạn (POST /auctions/{id}/settlement/download-url).</summary>
public sealed record DeliverableDownloadDto(
    string DownloadUrl,
    DateTimeOffset ExpiresAt,
    string FileName
);

/// <summary>Kết quả xử lý winner quá hạn thanh toán.</summary>
public sealed record AuctionExpireResultDto(
    Guid AuctionId,
    Guid? ExpiredBidId,
    decimal ReleasedHoldAmount,
    Guid? NextBidderId
);

public sealed record BuyNowResultDto(
    AuctionDto Auction,
    Guid? PaymentOrderId,
    DateTimeOffset? PaymentDeadline
);

// =====================================================================
// Workroom Chat (UC43)
// =====================================================================

public sealed record ChatAttachmentDto(
    Guid MessageAttachmentId,
    Guid MessageId,
    string FileUrl,
    string? FileName,
    string MimeType,
    long FileSize
);

public sealed record ChatMessageDto(
    Guid MessageId,
    Guid? RoomId,
    Guid SenderId,
    string? SenderFullName,
    string Body,
    string? TranslatedBody,
    string? SourceLang,
    string? TargetLang,
    string TranslationStatus,
    string MessageType,
    DateTimeOffset SentAt,
    bool IsRead,
    IReadOnlyList<ChatAttachmentDto> Attachments
);

public sealed record ChatRoomMemberDto(
    Guid UserId,
    string? FullName,
    string MemberRole,
    DateTimeOffset? LastReadAt
);

/// <summary>
/// Phòng chat trả về cho FE (UC43).
/// </summary>
/// <param name="CommissionId">
/// Commission gắn với phòng (null với phòng đấu giá / phòng hỗ trợ).
/// FE dùng để gọi /commissions/{id}/deadline-risks cho widget tiến độ (UC46)
/// mà không phải đoán từ tiêu đề phòng.
/// </param>
public sealed record ChatRoomDto(
    Guid RoomId,
    string ChatRoomType,
    string ChatRoomTitle,
    Guid? CommissionId,
    DateTimeOffset? LastMessageAt,
    bool IsLocked,
    IReadOnlyList<ChatRoomMemberDto> Members
);

public sealed record ChatReadReceiptDto(
    Guid RoomId,
    DateTimeOffset LastReadAt
);

public sealed record TranslationResultDto(
    Guid MessageId,
    string TranslatedBody,
    string? SourceLang,
    string TargetLang
);

// =====================================================================
// AI Assistant (UC44 / UC46)
// =====================================================================

public sealed record AiConversationDto(
    Guid AiConversationId,
    string Topic,
    string ContextType,
    Guid? ContextId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastMessageAt,
    int MessageCount
);

public sealed record AiMessageDto(
    Guid AiMessageId,
    Guid ConversationId,
    string Role,
    string Content,
    int? TokenCount,
    bool? Helpful,
    string? Note,
    string? ModelVersion,
    DateTimeOffset CreatedAt
);

public sealed record SendAiMessageResultDto(
    AiMessageDto UserMessage,
    AiMessageDto AssistantMessage
);

public sealed record AiFeedbackDto(
    Guid AiMessageId,
    bool Helpful,
    string? Note
);

public sealed record DeadlineReminderDto(
    Guid DeadlineReminderId,
    Guid CommissionId,
    Guid? MilestoneId,
    DateTimeOffset RemindAt,
    string Channel,
    DateTimeOffset? SentAt,
    decimal RiskScore,
    bool IsManual
);

public sealed record DeadlineRiskDto(
    Guid CommissionId,
    decimal RiskScore,
    string RiskLevel,
    DateTimeOffset? Deadline,
    int CurrentStage,
    DateTimeOffset? PredictedAt,
    DateTimeOffset? NextRemindAt,
    string? ModelVersion,
    IReadOnlyList<DeadlineReminderDto> Reminders
);

public sealed record ReminderSettingDto(
    bool EmailEnabled,
    bool PushEnabled,
    int LeadHours
);

// =====================================================================
// Creator Revenue Analytics (UC51)
// =====================================================================

public sealed record RevenueSummaryDto(
    decimal GrossAmount,
    decimal FeeAmount,
    decimal NetAmount,
    int CompletedOrderCount,
    decimal AverageOrderValue
);

public sealed record RevenueTimeseriesPointDto(
    string PeriodLabel,
    decimal GrossAmount,
    decimal FeeAmount,
    decimal NetAmount
);

public sealed record RevenueBreakdownItemDto(
    string Key,
    string Label,
    decimal GrossAmount,
    decimal FeeAmount,
    decimal NetAmount,
    int OrderCount
);

public sealed record RevenueSnapshotDto(
    Guid RevenueSnapshotId,
    string Scope,
    DateOnly SnapshotDate,
    decimal GrossAmount,
    decimal FeeAmount,
    decimal NetAmount,
    int CompletedOrderCount,
    DateTimeOffset? RebuiltAt,
    int RebuildCount
);
