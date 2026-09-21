using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Chat.Common;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Chat.Queries.GetChatRoom;

/// <summary>
/// UC43 — GET /api/v1/chat/rooms/{roomId}
/// Thông tin phòng: loại phòng, tiêu đề, danh sách thành viên và lastReadAt.
/// </summary>
public record GetChatRoomQuery(
    Guid UserId,
    Guid RoomId
) : IRequest<(bool Success, ChatRoomDto? Data, string[] Errors)>;

public class GetChatRoomQueryHandler
    : IRequestHandler<GetChatRoomQuery, (bool, ChatRoomDto?, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly IChatRoomService _chatRoomService;

    public GetChatRoomQueryHandler(
        IApplicationDbContext db,
        IChatRoomService chatRoomService)
    {
        _db = db;
        _chatRoomService = chatRoomService;
    }

    public async Task<(bool, ChatRoomDto?, string[])> Handle(
        GetChatRoomQuery request,
        CancellationToken cancellationToken)
    {
        var (room, error) = await _chatRoomService.ResolveRoomAsync(
            request.RoomId, request.UserId, cancellationToken);

        if (room is null)
        {
            return (false, null, [error ?? "Không truy cập được phòng chat."]);
        }

        var memberRows = await _db.ChatRoomMembers
            .AsNoTracking()
            .Where(m => m.RoomId == room.Id && !m.IsDeleted)
            .Select(m => new { m.UserId, m.MemberRole, m.LastReadAt })
            .ToListAsync(cancellationToken);

        // Phòng có thể chưa được ghi thành viên (tạo lazy) — bù bằng quan hệ commission.
        if (memberRows.Count == 0 && room.CommissionId.HasValue)
        {
            var commission = await _db.Commissions
                .AsNoTracking()
                .Where(c => c.Id == room.CommissionId.Value && !c.IsDeleted)
                .Select(c => new { c.ClientId, c.CreatorId })
                .FirstOrDefaultAsync(cancellationToken);

            if (commission is not null)
            {
                memberRows =
                [
                    new { UserId = commission.ClientId, MemberRole = "Client", LastReadAt = (DateTimeOffset?)null },
                    new { UserId = commission.CreatorId, MemberRole = "Creator", LastReadAt = (DateTimeOffset?)null }
                ];
            }
        }

        var userIds = memberRows.Select(m => m.UserId).Distinct().ToList();

        var names = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var members = memberRows
            .Select(m => new ChatRoomMemberDto(
                UserId: m.UserId,
                FullName: names.TryGetValue(m.UserId, out var name) ? name : null,
                MemberRole: m.MemberRole,
                LastReadAt: m.LastReadAt))
            .ToList();

        var dto = new ChatRoomDto(
            RoomId: room.Id,
            ChatRoomType: room.RoomType.ToString(),
            ChatRoomTitle: room.Title,
            LastMessageAt: room.LastMessageAt,
            IsLocked: room.IsLocked,
            Members: members);

        return (true, dto, []);
    }
}
