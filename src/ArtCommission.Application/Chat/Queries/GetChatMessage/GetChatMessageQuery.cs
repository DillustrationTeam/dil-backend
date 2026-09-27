using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Chat.Common;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Chat.Queries.GetChatMessage;

/// <summary>
/// UC43 — lấy MỘT tin nhắn theo Id, dùng cho luồng phát real-time.
///
/// VÌ SAO cần: tệp/ảnh đi qua REST (<c>POST /chat/rooms/{id}/attachments</c>) nên
/// handler đó không có <c>IHubContext</c> để phát cho cả phòng. Sau khi upload xong,
/// client gọi <c>ChatHub.PublishStoredMessage(roomId, messageId)</c>; hub lấy lại
/// đúng DTO qua query này rồi phát. Nhờ vậy shape tin nhắn phát đi LUÔN giống shape
/// của danh sách tin nhắn — không tự dựng DTO ở hub.
///
/// Quyền: chỉ thành viên phòng chứa tin nhắn mới đọc được.
/// </summary>
public record GetChatMessageQuery(
    Guid UserId,
    Guid MessageId
) : IRequest<(bool Success, ChatMessageDto? Data, string[] Errors)>;

public class GetChatMessageQueryHandler
    : IRequestHandler<GetChatMessageQuery, (bool, ChatMessageDto?, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly IChatRoomService _chatRoomService;

    public GetChatMessageQueryHandler(
        IApplicationDbContext db,
        IChatRoomService chatRoomService)
    {
        _db = db;
        _chatRoomService = chatRoomService;
    }

    public async Task<(bool, ChatMessageDto?, string[])> Handle(
        GetChatMessageQuery request,
        CancellationToken cancellationToken)
    {
        if (request.MessageId == Guid.Empty)
        {
            return (false, null, ["Thiếu mã tin nhắn."]);
        }

        var message = await _db.Messages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MessageId && !m.IsDeleted, cancellationToken);

        if (message is null)
        {
            return (false, null, ["Không tìm thấy tin nhắn."]);
        }

        if (message.RoomId is not Guid roomId)
        {
            return (false, null, ["Tin nhắn không thuộc phòng chat nào."]);
        }

        // Trả cùng thông báo cho "không có quyền" và "không tồn tại" để không lộ dữ liệu phòng khác.
        var isMember = await _chatRoomService.IsMemberAsync(roomId, request.UserId, cancellationToken);
        if (!isMember)
        {
            return (false, null, ["Bạn không phải thành viên của phòng chat này."]);
        }

        var attachments = await _db.MessageAttachments
            .AsNoTracking()
            .Where(a => a.MessageId == message.Id && !a.IsDeleted)
            .Select(a => new ChatAttachmentDto(
                a.Id,
                a.MessageId,
                a.FileUrl,
                a.FileName,
                a.MimeType,
                a.FileSize))
            .ToListAsync(cancellationToken);

        var senderName = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == message.SenderId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        var dto = new ChatMessageDto(
            MessageId: message.Id,
            RoomId: message.RoomId,
            SenderId: message.SenderId,
            SenderFullName: senderName,
            Body: message.Body,
            TranslatedBody: message.TranslatedBody,
            SourceLang: message.SourceLang,
            TargetLang: message.TargetLang,
            TranslationStatus: message.TranslationStatus.ToString(),
            MessageType: message.MessageType,
            SentAt: message.SentAt,
            IsRead: message.IsRead,
            Attachments: attachments);

        return (true, dto, []);
    }
}
