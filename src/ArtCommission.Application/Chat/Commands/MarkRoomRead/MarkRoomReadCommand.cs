using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Chat.Common;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Chat.Commands.MarkRoomRead;

/// <summary>
/// UC43 — POST /api/v1/chat/rooms/{roomId}/read
/// Đánh dấu đã đọc tới thời điểm hiện tại bằng <c>ChatRoomMember.LastReadAt</c>.
///
/// VÌ SAO dùng LastReadAt thay vì set <c>IsRead</c> cho từng tin: với phòng nhiều
/// nghìn tin, cập nhật từng dòng vừa chậm vừa tạo tranh chấp ghi. Một mốc thời gian
/// trên thành viên là đủ để tính số chưa đọc.
/// </summary>
public record MarkRoomReadCommand(
    Guid UserId,
    Guid RoomId
) : IRequest<(bool Success, ChatReadReceiptDto? Data, string[] Errors)>;

public class MarkRoomReadCommandHandler
    : IRequestHandler<MarkRoomReadCommand, (bool, ChatReadReceiptDto?, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly IChatRoomService _chatRoomService;

    public MarkRoomReadCommandHandler(
        IApplicationDbContext db,
        IChatRoomService chatRoomService)
    {
        _db = db;
        _chatRoomService = chatRoomService;
    }

    public async Task<(bool, ChatReadReceiptDto?, string[])> Handle(
        MarkRoomReadCommand request,
        CancellationToken cancellationToken)
    {
        var (room, error) = await _chatRoomService.ResolveRoomAsync(
            request.RoomId, request.UserId, cancellationToken);

        if (room is null)
        {
            return (false, null, [error ?? "Không truy cập được phòng chat."]);
        }

        var now = DateTimeOffset.UtcNow;

        var membership = await _db.ChatRoomMembers
            .FirstOrDefaultAsync(
                m => m.RoomId == room.Id && m.UserId == request.UserId,
                cancellationToken);

        if (membership is null)
        {
            // Thành viên suy ra từ commission nhưng chưa có dòng — tạo để lần sau
            // không phải suy diễn lại và mốc đã đọc được lưu thật.
            membership = new Domain.Entities.Chat.ChatRoomMember
            {
                RoomId = room.Id,
                UserId = request.UserId,
                MemberRole = await _chatRoomService.ResolveMemberRoleAsync(room, request.UserId, cancellationToken),
                LastReadAt = now
            };

            _db.ChatRoomMembers.Add(membership);
        }
        else
        {
            // Không lùi mốc đã đọc: nếu vì lý do nào đó thời điểm hiện tại cũ hơn
            // (đồng hồ lệch), giữ nguyên giá trị cũ thay vì làm badge chưa đọc tăng lại.
            if (!membership.LastReadAt.HasValue || membership.LastReadAt.Value < now)
            {
                membership.LastReadAt = now;
            }

            membership.UpdatedAt = now;
        }

        // Đánh dấu cờ nhanh trên các tin chưa đọc của phòng để endpoint đếm
        // không phải quét toàn bộ lịch sử.
        var unread = await _db.Messages
            .Where(m => m.RoomId == room.Id
                        && !m.IsRead
                        && !m.IsDeleted
                        && m.SenderId != request.UserId)
            .ToListAsync(cancellationToken);

        foreach (var message in unread)
        {
            message.IsRead = true;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return (true, new ChatReadReceiptDto(room.Id, membership.LastReadAt ?? now), []);
    }
}
