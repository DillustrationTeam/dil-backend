using ArtCommission.Application.ArtTrade.Common;
using ArtCommission.Application.ArtTrade.DTOs;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Common.Interfaces;
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

    public SubmitTradeArtworkCommandHandler(
        IApplicationDbContext db,
        IStorageService storageService,
        IWatermarkService watermarkService)
    {
        _db = db;
        _storageService = storageService;
        _watermarkService = watermarkService;
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

        if (trade.Status != TradeStatus.Accepted)
        {
            return (false, null, ["Chỉ có thể nộp tranh khi giao dịch đang ở trạng thái Accepted (Đang vẽ)."]);
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
        }
        else
        {
            trade.ReceiverOriginalFileUrl = originalStorageKey;
            trade.ReceiverPreviewUrl = previewPublicUrl;
            trade.ReceiverSubmittedAt = now;
        }

        trade.UpdatedAt = now;

        // Nếu cả hai bên đều đã nộp bài -> Chuyển sang BothSubmitted và kích hoạt 72h duyệt
        if (trade.InitiatorSubmittedAt.HasValue && trade.ReceiverSubmittedAt.HasValue)
        {
            trade.Status = TradeStatus.BothSubmitted;
            trade.ConfirmationDeadlineAt = now.AddHours(72);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (true, TradeMapper.ToDto(trade), []);
    }
}

