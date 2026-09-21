using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Queries.GetAiMessages;

/// <summary>
/// UC44 — GET /api/v1/ai/conversations/{conversationId}/messages
/// Tải lại nội dung một phiên hội thoại AI, cursor theo thời điểm tạo.
/// </summary>
public record GetAiMessagesQuery(
    Guid UserId,
    Guid ConversationId,
    string? Cursor = null,
    int Limit = 50
) : IRequest<(bool Success, IReadOnlyList<AiMessageDto>? Data, object? Meta, string[] Errors)>;

public class GetAiMessagesQueryHandler
    : IRequestHandler<GetAiMessagesQuery, (bool, IReadOnlyList<AiMessageDto>?, object?, string[])>
{
    private const int MaxLimit = 200;
    private const int DefaultLimit = 50;

    private readonly IApplicationDbContext _db;

    public GetAiMessagesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, IReadOnlyList<AiMessageDto>?, object?, string[])> Handle(
        GetAiMessagesQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ConversationId == Guid.Empty)
        {
            return (false, null, null, ["Thiếu mã phiên hội thoại."]);
        }

        var conversation = await _db.AiConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == request.ConversationId && !c.IsDeleted,
                cancellationToken);

        if (conversation is null)
        {
            return (false, null, null, ["Không tìm thấy phiên hội thoại."]);
        }

        if (conversation.UserId != request.UserId)
        {
            return (false, null, null, ["Bạn không có quyền truy cập phiên hội thoại này."]);
        }

        var limit = request.Limit <= 0 ? DefaultLimit : Math.Min(request.Limit, MaxLimit);

        var query = _db.AiMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id && !m.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            if (!AiMessageCursor.TryDecode(request.Cursor, out var cursorCreatedAt, out var cursorId))
            {
                return (false, null, null, ["Cursor không hợp lệ."]);
            }

            query = query.Where(m => m.CreatedAt > cursorCreatedAt
                                     || (m.CreatedAt == cursorCreatedAt && m.Id.CompareTo(cursorId) > 0));
        }

        // Tăng dần theo thời gian: hội thoại phải đọc từ trên xuống như khi chat.
        //
        // LỖI ĐÃ SỬA: trước đây trả `m.Role.ToString()` ⇒ "User"/"Assistant" (PascalCase),
        // trong khi POST cùng module trả "user"/"assistant" qua ToContractName.
        // Cùng một module mà hai endpoint trả hai kiểu chữ ⇒ FE phải xử lý 2 trường hợp
        // và rất dễ vỡ khi render bong bóng chat.
        var rows = await query
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Take(limit + 1)
            .Select(m => new
            {
                m.Id,
                m.ConversationId,
                Role = m.Role,
                m.Content,
                m.TokenCount,
                m.FeedbackHelpful,
                m.FeedbackNote,
                m.ModelVersion,
                m.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        var rowPage = hasMore ? rows.Take(limit).ToList() : rows;

        var page = rowPage
            .Select(m => new AiMessageDto(
                m.Id,
                m.ConversationId,
                AiAndRevenueEnumNames.ToContractName(m.Role),
                m.Content,
                m.TokenCount,
                m.FeedbackHelpful,
                m.FeedbackNote,
                m.ModelVersion,
                m.CreatedAt))
            .ToList();

        var last = page.LastOrDefault();
        var nextCursor = hasMore && last is not null
            ? AiMessageCursor.Encode(last.CreatedAt, last.AiMessageId)
            : null;

        return (true, page, new { nextCursor, count = page.Count }, []);
    }
}

/// <summary>Mã hoá cursor phân trang tin nhắn AI theo (CreatedAt, Id).</summary>
public static class AiMessageCursor
{
    public static string Encode(DateTimeOffset createdAt, Guid id)
    {
        var payload = string.Join('|',
            createdAt.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            id.ToString("N"));

        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryDecode(string cursor, out DateTimeOffset createdAt, out Guid id)
    {
        createdAt = default;
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

            createdAt = DateTimeOffset.FromUnixTimeMilliseconds(ms);
            id = parsedId;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
