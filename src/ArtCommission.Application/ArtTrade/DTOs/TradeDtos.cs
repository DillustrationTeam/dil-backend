using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Domain.Enums;
using ArtTradeEntity = ArtCommission.Domain.Entities.Trade.ArtTrade;

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
    string? ReceiverResponseNote,
    DateTimeOffset? ReceiverAcceptedAt,
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
)
{
    public static ArtTradeDto FromEntity(ArtTradeEntity trade, IStorageService? storageService = null)
    {
        var initiatorSummary = new UserSummaryDto(
            Id: trade.InitiatorId,
            FullName: trade.Initiator?.FullName ?? "Initiator",
            AvatarUrl: trade.Initiator?.AvatarUrl
        );

        var receiverSummary = new UserSummaryDto(
            Id: trade.ReceiverId,
            FullName: trade.Receiver?.FullName ?? "Receiver",
            AvatarUrl: trade.Receiver?.AvatarUrl
        );

        ArtworkSummaryDto? initiatorArt = trade.InitiatorArtwork != null
            ? new ArtworkSummaryDto(
                trade.InitiatorArtwork.Id,
                trade.InitiatorArtwork.Title,
                trade.InitiatorArtwork.ThumbnailUrl,
                trade.InitiatorArtwork.ImageUrl)
            : null;

        ArtworkSummaryDto? receiverArt = trade.ReceiverArtwork != null
            ? new ArtworkSummaryDto(
                trade.ReceiverArtwork.Id,
                trade.ReceiverArtwork.Title,
                trade.ReceiverArtwork.ThumbnailUrl,
                trade.ReceiverArtwork.ImageUrl)
            : null;

        var initPreviewUrl = trade.InitiatorPreviewUrl;
        var recvPreviewUrl = trade.ReceiverPreviewUrl;

        // Khi cả 2 bên đã hoàn tất (Completed), gỡ bỏ watermark bằng cách cung cấp link ảnh gốc không gắn watermark
        if (trade.Status == TradeStatus.Completed && storageService != null)
        {
            if (!string.IsNullOrEmpty(trade.InitiatorOriginalFileUrl))
            {
                try
                {
                    initPreviewUrl = storageService.GeneratePresignedDownloadUrl(trade.InitiatorOriginalFileUrl, TimeSpan.FromHours(24));
                }
                catch
                {
                    // Fallback to existing preview if error
                }
            }

            if (!string.IsNullOrEmpty(trade.ReceiverOriginalFileUrl))
            {
                try
                {
                    recvPreviewUrl = storageService.GeneratePresignedDownloadUrl(trade.ReceiverOriginalFileUrl, TimeSpan.FromHours(24));
                }
                catch
                {
                    // Fallback to existing preview if error
                }
            }
        }

        return new ArtTradeDto(
            Id: trade.Id,
            Title: trade.Title,
            Description: trade.Description,
            Status: trade.Status,
            Initiator: initiatorSummary,
            Receiver: receiverSummary,
            InitiatorRequestNote: trade.InitiatorRequestNote,
            ReceiverRequestNote: trade.ReceiverRequestNote,
            ReceiverResponseNote: trade.ReceiverResponseNote,
            ReceiverAcceptedAt: trade.ReceiverAcceptedAt,
            InitiatorArtworkId: trade.InitiatorArtworkId,
            ReceiverArtworkId: trade.ReceiverArtworkId,
            InitiatorArtwork: initiatorArt,
            ReceiverArtwork: receiverArt,
            InitiatorPreviewUrl: initPreviewUrl,
            ReceiverPreviewUrl: recvPreviewUrl,
            InitiatorSubmittedAt: trade.InitiatorSubmittedAt,
            ReceiverSubmittedAt: trade.ReceiverSubmittedAt,
            IsInitiatorConfirmed: trade.IsInitiatorConfirmed,
            InitiatorConfirmedAt: trade.InitiatorConfirmedAt,
            IsReceiverConfirmed: trade.IsReceiverConfirmed,
            ReceiverConfirmedAt: trade.ReceiverConfirmedAt,
            SubmissionDeadlineAt: trade.SubmissionDeadlineAt,
            ConfirmationDeadlineAt: trade.ConfirmationDeadlineAt,
            CompletedAt: trade.CompletedAt,
            CancelledAt: trade.CancelledAt,
            CancelledById: trade.CancelledById,
            CancellationReason: trade.CancellationReason,
            RejectionReason: trade.RejectionReason,
            CreatedAt: trade.CreatedAt,
            UpdatedAt: trade.UpdatedAt
        );
    }
}

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
)
{
    public static ArtTradeSummaryDto FromEntity(ArtTradeEntity trade, Guid currentUserId)
    {
        var isInitiator = trade.InitiatorId == currentUserId;
        var partnerUser = isInitiator ? trade.Receiver : trade.Initiator;
        var partnerId = isInitiator ? trade.ReceiverId : trade.InitiatorId;

        var partnerSummary = new UserSummaryDto(
            Id: partnerId,
            FullName: partnerUser?.FullName ?? "Partner",
            AvatarUrl: partnerUser?.AvatarUrl
        );

        var myPreview = isInitiator ? trade.InitiatorPreviewUrl : trade.ReceiverPreviewUrl;
        var partnerPreview = isInitiator ? trade.ReceiverPreviewUrl : trade.InitiatorPreviewUrl;

        var hasSubmitted = isInitiator ? trade.InitiatorSubmittedAt.HasValue : trade.ReceiverSubmittedAt.HasValue;
        var partnerHasSubmitted = isInitiator ? trade.ReceiverSubmittedAt.HasValue : trade.InitiatorSubmittedAt.HasValue;

        var hasConfirmed = isInitiator ? trade.IsInitiatorConfirmed : trade.IsReceiverConfirmed;
        var partnerHasConfirmed = isInitiator ? trade.IsReceiverConfirmed : trade.IsInitiatorConfirmed;

        return new ArtTradeSummaryDto(
            Id: trade.Id,
            Title: trade.Title,
            Status: trade.Status,
            Partner: partnerSummary,
            Role: isInitiator ? "Initiator" : "Receiver",
            MyPreviewUrl: myPreview,
            PartnerPreviewUrl: partnerPreview,
            HasSubmitted: hasSubmitted,
            PartnerHasSubmitted: partnerHasSubmitted,
            HasConfirmed: hasConfirmed,
            PartnerHasConfirmed: partnerHasConfirmed,
            SubmissionDeadlineAt: trade.SubmissionDeadlineAt,
            CreatedAt: trade.CreatedAt
        );
    }
}

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

public sealed record CreatorTradeSearchDto(
    Guid CreatorProfileId,
    Guid UserId,
    string DisplayName,
    string? Headline,
    string? Specialties,
    string? AvatarUrl,
    int AvailableSlots,
    bool IsAcceptingOrders
);

public sealed record CreateArtTradeRequest(
    Guid? ReceiverId = null,
    string? ReceiverDisplayName = null,
    string Title = "",
    string? Description = null,
    string? RequestNote = null,
    int? DurationDays = 14,
    string? InitiatorRequestNote = null,
    string? ReceiverRequestNote = null
);

public sealed record ProposeTradeRequest(
    Guid? ReceiverId = null,
    string? ReceiverDisplayName = null,
    string Title = "",
    string? Description = null,
    string? RequestNote = null,
    int? DurationDays = 14,
    string? InitiatorRequestNote = null,
    string? ReceiverRequestNote = null
);

public sealed record RespondTradeProposalRequest(
    bool Accept,
    string? RequestNote = null,
    string? ResponseNote = null,
    string? RejectionReason = null
);

public sealed record CancelTradeRequest(
    string Reason,
    bool IsViolation = false,
    Guid? ViolatorUserId = null
);


