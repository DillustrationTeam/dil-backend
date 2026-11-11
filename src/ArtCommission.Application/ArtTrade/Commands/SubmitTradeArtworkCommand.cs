using ArtCommission.Application.ArtTrade.Common;
using ArtCommission.Application.ArtTrade.DTOs;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Common.Interfaces;
using INotificationPublisher = ArtCommission.Application.Notifications.Common.INotificationPublisher;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtTrade.Commands;

public sealed record SubmitTradeArtworkCommand(
    Guid TradeId,
    Guid UserId,
    Stream FileStream,
    string ContentType,
    string FileName,
    string? ArtworkTitle = null
) : IRequest<(bool Success, ArtTradeDto? Data, string[] Errors)>;

public class SubmitTradeArtworkCommandValidator : AbstractValidator<SubmitTradeArtworkCommand>
{
    private static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".psd", ".clip"];

    public SubmitTradeArtworkCommandValidator()
    {
        RuleFor(x => x.TradeId).NotEmpty().WithMessage("Mã giao dịch không hợp lệ.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Mã người dùng không hợp lệ.");
        RuleFor(x => x.FileStream).NotNull().WithMessage("File tranh gốc là bắt buộc.");
        RuleFor(x => x.ContentType).NotEmpty().WithMessage("Thiếu định dạng file.");
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("Tên file không được để trống.")
            .Must(name =>
            {
                var ext = Path.GetExtension(name)?.ToLowerInvariant();
                return !string.IsNullOrEmpty(ext) && AllowedExtensions.Contains(ext);
            }).WithMessage("Định dạng file không được hỗ trợ. Chỉ chấp nhận PNG, JPG, PSD, CLIP.");
    }
}

public class SubmitTradeArtworkCommandHandler : IRequestHandler<SubmitTradeArtworkCommand, (bool Success, ArtTradeDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly IStorageService _storageService;
    private readonly IWatermarkService _watermarkService;
    private readonly INotificationPublisher _notifPublisher;

    public SubmitTradeArtworkCommandHandler(
        IApplicationDbContext db,
        IStorageService storageService,
        IWatermarkService watermarkService,
        INotificationPublisher notifPublisher)
    {
        _db = db;
        _storageService = storageService;
        _watermarkService = watermarkService;
        _notifPublisher = notifPublisher;
    }

    public async Task<(bool Success, ArtTradeDto? Data, string[] Errors)> Handle(SubmitTradeArtworkCommand request, CancellationToken cancellationToken)
    {
        var validator = new SubmitTradeArtworkCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, null, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var trade = await _db.ArtTrades
            .Include(t => t.Initiator)
            .Include(t => t.Receiver)
            .FirstOrDefaultAsync(t => t.Id == request.TradeId && !t.IsDeleted, cancellationToken);

        if (trade == null)
        {
            return (false, null, ["Không tìm thấy giao dịch trao đổi tranh."]);
        }

        var isInitiator = trade.InitiatorId == request.UserId;
        var isReceiver = trade.ReceiverId == request.UserId;
        if (!isInitiator && !isReceiver)
        {
            return (false, null, ["Bạn không tham gia vào giao dịch này."]);
        }

        if (trade.Status != TradeStatus.Accepted && trade.Status != TradeStatus.BothSubmitted)
        {
            return (false, null, ["Chỉ có thể nộp hoặc thay đổi tranh khi giao dịch đang ở trạng thái Accepted (Đang vẽ) hoặc BothSubmitted."]);
        }

        var now = DateTimeOffset.UtcNow;
        var ext = Path.GetExtension(request.FileName).ToLowerInvariant();

        // 1. Upload bản gốc vào Private Storage Bucket (Tuyệt đối không để lộ link công khai)
        var originalKey = $"trades/{trade.Id}/original-user-{request.UserId:N}-{Guid.NewGuid():N}{ext}";
        var originalStorageKey = await _storageService.UploadPrivateAsync(
            request.FileStream, originalKey, request.ContentType, cancellationToken);

        // 2. Tự động can thiệp đóng Watermark bảo vệ tác quyền
        request.FileStream.Position = 0;
        using var watermarkedStream = await _watermarkService.ApplyWatermarkAsync(
            request.FileStream, "DILLUSTRATION ART TRADE — PREVIEW ONLY", cancellationToken);

        // 3. Upload bản Watermark preview lên Public CDN để đối phương xem trước
        var watermarkedKey = $"trades/{trade.Id}/preview-user-{request.UserId:N}-{Guid.NewGuid():N}.jpg";
        var previewPublicUrl = await _storageService.UploadPublicAsync(
            watermarkedStream, watermarkedKey, "image/jpeg", cancellationToken);

        if (isInitiator)
        {
            trade.InitiatorOriginalFileUrl = originalStorageKey;
            trade.InitiatorPreviewUrl = previewPublicUrl;
            trade.InitiatorSubmittedAt = now;

            // Khi nộp hoặc thay đổi tranh, reset xác nhận của đối tác để đối tác xem lại tranh mới
            trade.IsReceiverConfirmed = false;
            trade.ReceiverConfirmedAt = null;
        }
        else
        {
            trade.ReceiverOriginalFileUrl = originalStorageKey;
            trade.ReceiverPreviewUrl = previewPublicUrl;
            trade.ReceiverSubmittedAt = now;

            // Khi nộp hoặc thay đổi tranh, reset xác nhận của đối tác để đối tác xem lại tranh mới
            trade.IsInitiatorConfirmed = false;
            trade.InitiatorConfirmedAt = null;
        }

        trade.UpdatedAt = now;

        // Nếu cả hai bên đều đã nộp bài -> Chuyển sang BothSubmitted
        if (!string.IsNullOrEmpty(trade.InitiatorOriginalFileUrl) && !string.IsNullOrEmpty(trade.ReceiverOriginalFileUrl))
        {
            trade.Status = TradeStatus.BothSubmitted;
            trade.ConfirmationDeadlineAt = now.AddHours(72);
        }
        else
        {
            trade.Status = TradeStatus.Accepted;
            trade.ConfirmationDeadlineAt = null;
        }

        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var recipientId = isInitiator ? trade.ReceiverId : trade.InitiatorId;
            var submitterName = (isInitiator ? trade.Initiator?.FullName : trade.Receiver?.FullName) ?? "Đối tác";
            await _notifPublisher.PublishAsync(
                userId: recipientId,
                notificationType: NotificationType.ArtTradeArtworkSubmitted,
                title: "Đối tác đã nộp tác phẩm trao đổi",
                body: $"Họa sĩ {submitterName} đã nộp tranh cho phiên \"{trade.Title}\". Hãy vào xem bản watermark và kiểm tra chất lượng!",
                refType: "ArtTrade",
                refId: trade.Id,
                dedupKey: $"ArtTradeArtworkSubmitted:{trade.Id}:{request.UserId}",
                cancellationToken: cancellationToken
            );
        }
        catch
        {
            // Bỏ qua lỗi gửi thông báo để không gián đoạn thao tác nộp tranh
        }

        return (true, ArtTradeDto.FromEntity(trade, _storageService), []);
    }
}

