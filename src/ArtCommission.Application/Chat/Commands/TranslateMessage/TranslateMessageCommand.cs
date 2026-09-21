using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Chat.Common;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Chat.Commands.TranslateMessage;

/// <summary>
/// UC43 — POST /api/v1/chat/messages/{messageId}/translate
/// Dịch một tin nhắn sang ngôn ngữ người nhận (AI Real-time Translation).
///
/// CHIẾN LƯỢC CACHE — điểm quan trọng nhất của endpoint này:
///   - Nếu tin nhắn ĐÃ có bản dịch đúng ngôn ngữ đích ⇒ trả lại ngay, KHÔNG gọi AI.
///     Gọi lại vừa tốn tiền vừa chậm, mà kết quả thì giống nhau.
///   - Nếu lần trước dịch LỖI ⇒ cho phép thử lại (trạng thái Failed không chặn).
///   - Chỉ gọi AI khi thật sự cần, và lưu <c>TranslationError</c> khi lỗi để tra soát.
/// </summary>
public record TranslateMessageCommand(
    Guid UserId,
    Guid MessageId,
    string TargetLang
) : IRequest<(bool Success, TranslationResultDto? Data, string[] Errors)>;

public class TranslateMessageCommandValidator : AbstractValidator<TranslateMessageCommand>
{
    /// <summary>Các ngôn ngữ hệ thống hỗ trợ. Mở rộng bằng cách thêm mã ISO 639-1.</summary>
    public static readonly string[] SupportedLanguages = ["vi", "en", "ja", "ko", "zh", "fr"];

    public TranslateMessageCommandValidator()
    {
        RuleFor(x => x.MessageId).NotEmpty().WithMessage("Thiếu mã tin nhắn.");

        RuleFor(x => x.TargetLang)
            .NotEmpty().WithMessage("Thiếu ngôn ngữ đích.")
            .Must(lang => SupportedLanguages.Contains(lang.ToLowerInvariant()))
            .WithMessage($"Ngôn ngữ đích không được hỗ trợ. Chỉ nhận: {string.Join(", ", SupportedLanguages)}.");
    }
}

public class TranslateMessageCommandHandler
    : IRequestHandler<TranslateMessageCommand, (bool, TranslationResultDto?, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly IChatRoomService _chatRoomService;
    private readonly ITranslationClient _translationClient;
    private readonly ILogger<TranslateMessageCommandHandler> _logger;

    public TranslateMessageCommandHandler(
        IApplicationDbContext db,
        IChatRoomService chatRoomService,
        ITranslationClient translationClient,
        ILogger<TranslateMessageCommandHandler> logger)
    {
        _db = db;
        _chatRoomService = chatRoomService;
        _translationClient = translationClient;
        _logger = logger;
    }

    public async Task<(bool, TranslationResultDto?, string[])> Handle(
        TranslateMessageCommand request,
        CancellationToken cancellationToken)
    {
        var validation = new TranslateMessageCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var targetLang = request.TargetLang.ToLowerInvariant();

        var message = await _db.Messages.FirstOrDefaultAsync(
            m => m.Id == request.MessageId && !m.IsDeleted,
            cancellationToken);

        if (message is null)
        {
            return (false, null, ["Không tìm thấy tin nhắn."]);
        }

        // Quyền: chỉ thành viên phòng được dịch nội dung trong phòng đó.
        //
        // LỖI ĐÃ SỬA: trước đây chỉ kiểm khi `message.RoomId.HasValue`. Tin nhắn cũ
        // (tạo trước khi có ChatRoom) có RoomId = null nên BỎ QUA hoàn toàn bước kiểm tra —
        // bất kỳ người dùng đã đăng nhập nào đoán được messageId cũng dịch và đọc được
        // nội dung riêng tư của phòng mình không thuộc về.
        if (message.RoomId.HasValue)
        {
            var isMember = await _chatRoomService.IsMemberAsync(
                message.RoomId.Value, request.UserId, cancellationToken);

            if (!isMember)
            {
                return (false, null, ["Bạn không phải thành viên của phòng chat này."]);
            }
        }
        else if (message.CommissionId.HasValue)
        {
            // Đường dự phòng cho dữ liệu cũ: suy quyền từ đơn đặt vẽ gắn với tin nhắn.
            var isParticipant = await _db.Commissions
                .AsNoTracking()
                .AnyAsync(
                    c => c.Id == message.CommissionId.Value
                         && !c.IsDeleted
                         && (c.ClientId == request.UserId || c.CreatorId == request.UserId),
                    cancellationToken);

            if (!isParticipant)
            {
                return (false, null, ["Bạn không phải thành viên của phòng chat này."]);
            }
        }
        else
        {
            // Không xác định được phòng lẫn đơn ⇒ không có căn cứ nào để cho phép.
            // Từ chối là lựa chọn an toàn: thà chặn nhầm một tin mồ côi còn hơn mở
            // quyền đọc nội dung riêng tư cho mọi người dùng.
            return (false, null, ["Không xác định được phòng chat của tin nhắn này."]);
        }

        if (string.IsNullOrWhiteSpace(message.Body))
        {
            return (false, null, ["Tin nhắn không có nội dung văn bản để dịch."]);
        }

        // Cache: đã có bản dịch cho đúng ngôn ngữ đích và trạng thái Completed.
        if (message.TranslationStatus == TranslationStatus.Completed
            && !string.IsNullOrWhiteSpace(message.TranslatedBody)
            && string.Equals(message.TargetLang, targetLang, StringComparison.OrdinalIgnoreCase))
        {
            return (true, new TranslationResultDto(
                message.Id,
                message.TranslatedBody,
                message.SourceLang,
                targetLang), []);
        }

        // Ngôn ngữ gốc: nếu chưa biết, đánh dấu "auto" để AI tự nhận diện.
        var sourceLang = string.IsNullOrWhiteSpace(message.SourceLang) ? "auto" : message.SourceLang;

        // Nếu tin nhắn vốn đã ở ngôn ngữ đích thì không cần dịch.
        if (string.Equals(sourceLang, targetLang, StringComparison.OrdinalIgnoreCase))
        {
            message.TranslatedBody = message.Body;
            message.TargetLang = targetLang;
            message.TranslationStatus = TranslationStatus.Completed;
            message.TranslationError = null;
            message.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            return (true, new TranslationResultDto(message.Id, message.Body, sourceLang, targetLang), []);
        }

        var result = await _translationClient.TranslateAsync(
            message.Body, targetLang, cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Content))
        {
            // Lưu trạng thái lỗi để lần sau được phép thử lại và để tra soát nguyên nhân.
            message.TranslationStatus = TranslationStatus.Failed;
            message.TranslationError = Truncate(result.Error ?? "Dịch thất bại.", 500);
            message.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Dịch tin nhắn {MessageId} sang {TargetLang} thất bại: {Error}",
                message.Id, targetLang, result.Error);

            return (false, null, ["Không dịch được tin nhắn lúc này. Vui lòng thử lại."]);
        }

        message.TranslatedBody = result.Content;
        message.TargetLang = targetLang;

        // Chỉ ghi SourceLang khi AI trả về được; nếu không thì giữ "auto" để lần sau
        // vẫn cho phép dịch lại (không tự nhận "auto" là ngôn ngữ thật).
        if (!string.Equals(sourceLang, "auto", StringComparison.OrdinalIgnoreCase))
        {
            message.SourceLang = sourceLang;
        }

        message.TranslationStatus = TranslationStatus.Completed;
        message.TranslationError = null;
        message.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return (true, new TranslationResultDto(
            message.Id,
            result.Content,
            message.SourceLang,
            targetLang), []);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
