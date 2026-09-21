using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Queries.GetAiConversations;

/// <summary>
/// UC44 — GET /api/v1/ai/conversations
/// Danh sách phiên hội thoại AI của người dùng, cursor theo <c>last_message_at</c>.
/// </summary>
public record GetAiConversationsQuery(
    Guid UserId,
    string? Cursor = null,
    int Limit = 20
) : IRequest<(bool Success, IReadOnlyList<AiConversationDto>? Data, object? Meta, string[] Errors)>;

public class GetAiConversationsQueryHandler
    : IRequestHandler<GetAiConversationsQuery, (bool, IReadOnlyList<AiConversationDto>?, object?, string[])>
{
    private const int MaxLimit = 100;
    private const int DefaultLimit = 20;

    private readonly IApplicationDbContext _db;

    public GetAiConversationsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, IReadOnlyList<AiConversationDto>?, object?, string[])> Handle(
        GetAiConversationsQuery request,
        CancellationToken cancellationToken)
    {
        var limit = request.Limit <= 0 ? DefaultLimit : Math.Min(request.Limit, MaxLimit);

        var query = _db.AiConversations
            .AsNoTracking()
            .Where(c => c.UserId == request.UserId && !c.IsDeleted && !c.IsArchived);

        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            if (!AiCursor.TryDecode(request.Cursor, out var cursorLastMessageAt, out var cursorId))
            {
                return (false, null, null, ["Cursor không hợp lệ."]);
            }

            // Phiên chưa có tin nhắn (LastMessageAt null) nằm cuối danh sách; cursor
            // chỉ áp cho phần đã có LastMessageAt.
            query = query.Where(c =>
                c.LastMessageAt != null
                && (c.LastMessageAt < cursorLastMessageAt
                    || (c.LastMessageAt == cursorLastMessageAt && c.Id.CompareTo(cursorId) > 0)));
        }

        var rows = await query
            .OrderByDescending(c => c.LastMessageAt)
            .ThenBy(c => c.Id)
            .Take(limit + 1)
            .Select(c => new AiConversationDto(
                c.Id,
                c.Topic,
                c.ContextType.ToString(),
                c.ContextId,
                c.CreatedAt,
                c.LastMessageAt,
                c.MessageCount))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        var page = hasMore ? rows.Take(limit).ToList() : rows;

        var last = page.LastOrDefault();
        var nextCursor = hasMore && last?.LastMessageAt is not null
            ? AiCursor.Encode(last.LastMessageAt.Value, last.AiConversationId)
            : null;

        return (true, page, new { nextCursor, count = page.Count }, []);
    }
}

/// <summary>Mã hoá cursor phân trang phiên hội thoại theo (LastMessageAt, Id).</summary>
public static class AiCursor
{
    public static string Encode(DateTimeOffset lastMessageAt, Guid id)
    {
        var payload = string.Join('|',
            lastMessageAt.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            id.ToString("N"));

        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryDecode(string cursor, out DateTimeOffset lastMessageAt, out Guid id)
    {
        lastMessageAt = default;
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

            lastMessageAt = DateTimeOffset.FromUnixTimeMilliseconds(ms);
            id = parsedId;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
