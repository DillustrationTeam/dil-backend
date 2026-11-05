using ArtCommission.Application.ArtTrade.DTOs;
using ArtTradeEntity = ArtCommission.Domain.Entities.Trade.ArtTrade;

namespace ArtCommission.Application.ArtTrade.Common;

public static class TradeMapper
{
    public static ArtTradeDto ToDto(ArtTradeEntity trade)
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

        return new ArtTradeDto(
            Id: trade.Id,
            Title: trade.Title,
            Description: trade.Description,
            Status: trade.Status,
            Initiator: initiatorSummary,
            Receiver: receiverSummary,
            InitiatorRequestNote: trade.InitiatorRequestNote,
            ReceiverRequestNote: trade.ReceiverRequestNote,
            InitiatorArtworkId: trade.InitiatorArtworkId,
            ReceiverArtworkId: trade.ReceiverArtworkId,
            InitiatorArtwork: initiatorArt,
            ReceiverArtwork: receiverArt,
            InitiatorPreviewUrl: trade.InitiatorPreviewUrl,
            ReceiverPreviewUrl: trade.ReceiverPreviewUrl,
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

    public static ArtTradeSummaryDto ToSummaryDto(ArtTradeEntity trade, Guid currentUserId)
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
