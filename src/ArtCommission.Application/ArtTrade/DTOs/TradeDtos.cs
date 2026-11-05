using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.ArtTrade.DTOs;

public sealed record UserSummaryDto(
    Guid Id,
    string FullName,
    string? AvatarUrl
);

public sealed record ArtworkSummaryDto(
    Guid Id,
    string Title,
    string? ThumbnailUrl,
    string? ImageUrl
);

public sealed record ArtTradeDto(
    Guid Id,
    string Title,
    string? Description,
    TradeStatus Status,
    UserSummaryDto Initiator,
    UserSummaryDto Receiver,
    string? InitiatorRequestNote,
    string? ReceiverRequestNote,
    Guid? InitiatorArtworkId,
    Guid? ReceiverArtworkId,
    ArtworkSummaryDto? InitiatorArtwork,
    ArtworkSummaryDto? ReceiverArtwork,
    string? InitiatorPreviewUrl,
    string? ReceiverPreviewUrl,
    DateTimeOffset? InitiatorSubmittedAt,
    DateTimeOffset? ReceiverSubmittedAt,
    bool IsInitiatorConfirmed,
    DateTimeOffset? InitiatorConfirmedAt,
    bool IsReceiverConfirmed,
    DateTimeOffset? ReceiverConfirmedAt,
    DateTimeOffset? SubmissionDeadlineAt,
    DateTimeOffset? ConfirmationDeadlineAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    Guid? CancelledById,
    string? CancellationReason,
    string? RejectionReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);

public sealed record ArtTradeSummaryDto(
    Guid Id,
    string Title,
    TradeStatus Status,
    UserSummaryDto Partner,
    string Role, // "Initiator" or "Receiver"
    string? MyPreviewUrl,
    string? PartnerPreviewUrl,
    bool HasSubmitted,
    bool PartnerHasSubmitted,
    bool HasConfirmed,
    bool PartnerHasConfirmed,
    DateTimeOffset? SubmissionDeadlineAt,
    DateTimeOffset CreatedAt
);

public sealed record FrozenSlotDetailDto(
    Guid FreezeId,
    Guid ArtTradeId,
    DateTimeOffset FrozenFrom,
    DateTimeOffset FrozenUntil,
    string? Reason,
    int DaysRemaining
);

public sealed record TradeSlotStatusDto(
    Guid UserId,
    int TotalSlots,
    int ActiveTradesCount,
    int FrozenSlotsCount,
    int AvailableSlots,
    bool CanTrade,
    List<FrozenSlotDetailDto> FrozenDetails
);

public sealed record TradeDownloadUrlDto(
    string DownloadUrl,
    DateTimeOffset ExpiresAt,
    string FileName
);

