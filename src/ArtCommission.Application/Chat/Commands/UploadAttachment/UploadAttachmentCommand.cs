using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Chat.Common;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Chat;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Chat.Commands.UploadAttachment;

/// <summary>
/// UC43 — POST /api/v1/chat/rooms/{roomId}/attachments
/// Upload ảnh/file tham khảo vào phòng chat.
///
/// VÌ SAO tách khỏi tầng HTTP: handler nhận <see cref="Stream"/> + metadata thay vì
/// <c>IFormFile</c>, để tầng Application không phụ thuộc ASP.NET Core. Controller
/// chịu trách nhiệm đọc multipart và mở stream.
///
/// Luồng: nếu client không gửi kèm <c>MessageId</c>, hệ thống tạo một tin nhắn mới
/// thuộc loại Image/File rồi gắn đính kèm vào đó — như vậy mọi file luôn thuộc
/// một tin nhắn và hiện đúng vị trí trong lịch sử chat.
/// </summary>
public record UploadAttachmentCommand(
    Guid UserId,
    Guid RoomId,
    Stream Content,
    string FileName,
    string ContentType,
    long FileSize,
    Guid? MessageId = null
) : IRequest<(bool Success, ChatAttachmentDto? Data, Guid MessageId, string[] Errors)>;

public class UploadAttachmentCommandValidator : AbstractValidator<UploadAttachmentCommand>
{
    /// <summary>Trần dung lượng một file đính kèm (25 MB).</summary>
    public const long MaxFileSizeBytes = 25 * 1024 * 1024;

    /// <summary>Định dạng được phép — chặn upload file thực thi vào phòng chat.</summary>
    public static readonly string[] AllowedContentTypes =
    [
        "image/jpeg", "image/png", "image/webp", "image/gif",
        "application/pdf", "application/zip",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain"
    ];

    public UploadAttachmentCommandValidator()
    {
        RuleFor(x => x.RoomId).NotEmpty().WithMessage("Thiếu mã phòng chat.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("Thiếu tên file.")
            .MaximumLength(300).WithMessage("Tên file tối đa 300 ký tự.");

        RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("File rỗng.")
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage($"File tối đa {MaxFileSizeBytes / 1024 / 1024} MB.");

        RuleFor(x => x.ContentType)
            .Must(ct => AllowedContentTypes.Contains(ct, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Định dạng file không được hỗ trợ.");
    }
}

public class UploadAttachmentCommandHandler
    : IRequestHandler<UploadAttachmentCommand, (bool, ChatAttachmentDto?, Guid, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly IChatRoomService _chatRoomService;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<UploadAttachmentCommandHandler> _logger;

    public UploadAttachmentCommandHandler(
        IApplicationDbContext db,
        IChatRoomService chatRoomService,
        IFileStorageService fileStorage,
        ILogger<UploadAttachmentCommandHandler> logger)
    {
        _db = db;
        _chatRoomService = chatRoomService;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<(bool, ChatAttachmentDto?, Guid, string[])> Handle(
        UploadAttachmentCommand request,
        CancellationToken cancellationToken)
    {
        var validation = new UploadAttachmentCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, Guid.Empty, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var (room, error) = await _chatRoomService.ResolveRoomAsync(
            request.RoomId, request.UserId, cancellationToken);

        if (room is null)
        {
            return (false, null, Guid.Empty, [error ?? "Không truy cập được phòng chat."]);
        }

        if (room.IsLocked)
        {
            return (false, null, Guid.Empty, ["Phòng chat đã bị khoá nên không gửi được file."]);
        }

        var now = DateTimeOffset.UtcNow;

        Message message;

        if (request.MessageId.HasValue)
        {
            var existing = await _db.Messages.FirstOrDefaultAsync(
                m => m.Id == request.MessageId.Value
                     && m.RoomId == room.Id
                     && !m.IsDeleted,
                cancellationToken);

            if (existing is null)
            {
                return (false, null, Guid.Empty, ["Không tìm thấy tin nhắn để gắn file."]);
            }

            if (existing.SenderId != request.UserId)
            {
                return (false, null, Guid.Empty, ["Chỉ gắn được file vào tin nhắn của chính bạn."]);
            }

            message = existing;
        }
        else
        {
            var isImage = request.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

            message = new Message
            {
                RoomId = room.Id,
                CommissionId = room.CommissionId,
                SenderId = request.UserId,
                MessageType = isImage ? Domain.Enums.MessageTypes.Image : Domain.Enums.MessageTypes.File,
                Body = request.FileName,
                SentAt = now
            };

            _db.Messages.Add(message);
            room.LastMessageAt = now;
            room.UpdatedAt = now;
        }

        // Lưu file TRƯỚC khi ghi DB: nếu upload lỗi, không để lại bản ghi trỏ tới file không tồn tại.
        string fileUrl;
        try
        {
            fileUrl = await _fileStorage.SaveAsync(
                request.Content,
                request.FileName,
                request.ContentType,
                $"chat/{room.Id}",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Upload file đính kèm thất bại cho room {RoomId}, user {UserId}.",
                room.Id, request.UserId);

            return (false, null, Guid.Empty,
                ["Không lưu được file. Vui lòng thử lại."]);
        }

        var attachment = new MessageAttachment
        {
            MessageId = message.Id,
            UploadedBy = request.UserId,
            FileUrl = fileUrl,
            FileName = request.FileName,
            MimeType = request.ContentType,
            FileSize = request.FileSize
        };

        _db.MessageAttachments.Add(attachment);

        await _db.SaveChangesAsync(cancellationToken);

        var dto = new ChatAttachmentDto(
            MessageAttachmentId: attachment.Id,
            MessageId: attachment.MessageId,
            FileUrl: attachment.FileUrl,
            FileName: attachment.FileName,
            MimeType: attachment.MimeType,
            FileSize: attachment.FileSize);

        return (true, dto, message.Id, []);
    }
}
