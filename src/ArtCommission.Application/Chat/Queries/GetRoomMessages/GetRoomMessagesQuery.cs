using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Chat.Common;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Chat.Queries.GetRoomMessages;

/// <summary>
/// UC43 — GET /api/v1/chat/rooms/{roomId}/messages
/// Tải lịch sử chat của phòng, cursor theo <c>sent_at</c>.
///
/// Chiều mặc định là MỚI NHẤT TRƯỚC (giảm dần): mở workroom thì tin gần nhất
/// là thứ người dùng cần thấy ngay; FE tự đảo chiều khi render.
/// </summary>
public record GetRoomMessagesQuery(
    Guid UserId,
    Guid RoomId,
    string? Cursor = null,
    int Limit = 30
) : IRequest<(bool Success, IReadOnlyList<ChatMessageDto>? Data, object? Meta, string[] Errors)>;

public class GetRoomMessagesQueryHandler
    : IRequestHandler<GetRoomMessagesQuery, (bool, IReadOnlyList<ChatMessageDto>?, object?, string[])>
{
    private const int MaxLimit = 100;
    private const int DefaultLimit = 30;

    private readonly IApplicationDbContext _db;
    private readonly IChatRoomService _chatRoomService;

    public GetRoomMessagesQueryHandler(
        IApplicationDbContext db,
        IChatRoomService chatRoomService)
    {
        _db = db;
        _chatRoomService = chatRoomService;
    }

    public async Task<(bool, IReadOnlyList<ChatMessageDto>?, object?, string[])> Handle(
        GetRoomMessagesQuery request,
        CancellationToken cancellationToken)
    {
        var (room, error) = await _chatRoomService.ResolveRoomAsync(
            request.RoomId, request.UserId, cancellationToken);

        if (room is null)
        {
            return (false, null, null, [error ?? "Không truy cập được phòng chat."]);
        }

        var limit = request.Limit <= 0 ? DefaultLimit : Math.Min(request.Limit, MaxLimit);

        var query = _db.Messages
            .AsNoTracking()
            .Where(m => m.RoomId == room.Id && !m.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            if (!ChatMessageCursor.TryDecode(request.Cursor, out var cursorSentAt, out var cursorId))
            {
                return (false, null, null, ["Cursor không hợp lệ."]);
            }

            query = query.Where(m => m.SentAt < cursorSentAt
                                     || (m.SentAt == cursorSentAt && m.Id.CompareTo(cursorId) > 0));
        }

        // Lấy dư 1 dòng để biết còn trang sau hay không.
        var rows = await query
            .OrderByDescending(m => m.SentAt)
            .ThenBy(m => m.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        var page = hasMore ? rows.Take(limit).ToList() : rows;

        var messageIds = page.Select(m => m.Id).ToList();

        var attachments = await _db.MessageAttachments
            .AsNoTracking()
            .Where(a => messageIds.Contains(a.MessageId) && !a.IsDeleted)
            .Select(a => new ChatAttachmentDto(
                a.Id,
                a.MessageId,
                a.FileUrl,
                a.FileName,
                a.MimeType,
                a.FileSize))
            .ToListAsync(cancellationToken);

        var attachmentsByMessage = attachments
            .GroupBy(a => a.MessageId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ChatAttachmentDto>)g.ToList());

        var senderIds = page.Select(m => m.SenderId).Distinct().ToList();

        var senderNames = await _db.Users
            .AsNoTracking()
            .Where(u => senderIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var items = page.Select(m => new ChatMessageDto(
            MessageId: m.Id,
            RoomId: m.RoomId,
            SenderId: m.SenderId,
            SenderFullName: senderNames.TryGetValue(m.SenderId, out var name) ? name : null,
            Body: m.Body,
            TranslatedBody: m.TranslatedBody,
            SourceLang: m.SourceLang,
            TargetLang: m.TargetLang,
            TranslationStatus: m.TranslationStatus.ToString(),
            MessageType: m.MessageType,
            SentAt: m.SentAt,
            IsRead: m.IsRead,
            Attachments: attachmentsByMessage.TryGetValue(m.Id, out var list)
                ? list
                : [])).ToList();

        var last = page.LastOrDefault();
        var nextCursor = hasMore && last is not null
            ? ChatMessageCursor.Encode(last.SentAt, last.Id)
            : null;

        return (true, items, new { nextCursor, count = items.Count }, []);
    }
}

/// <summary>Mã hoá cursor phân trang tin nhắn theo (SentAt, Id).</summary>
public static class ChatMessageCursor
{
    public static string Encode(DateTimeOffset sentAt, Guid id)
    {
        var payload = string.Join('|',
            sentAt.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            id.ToString("N"));

        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryDecode(string cursor, out DateTimeOffset sentAt, out Guid id)
    {
        sentAt = default;
        id = Guid.Empty;

        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|');

            if (parts.Length != 2)
            {
                return false;
            }

            if (!long.TryParse(parts[0], System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var ms))
            {
                return false;
            }

            if (!Guid.TryParseExact(parts[1], "N", out var parsedId))
            {
                return false;
            }

            sentAt = DateTimeOffset.FromUnixTimeMilliseconds(ms);
            id = parsedId;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
