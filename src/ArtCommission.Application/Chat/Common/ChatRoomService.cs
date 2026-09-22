using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Chat;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Chat.Common;

/// <summary>
/// Nghiệp vụ phòng chat dùng chung cho REST và SignalR hub.
///
/// VÌ SAO cần lớp này: quyền vào phòng là điều kiện bảo mật lặp lại ở MỌI endpoint chat
/// (đọc tin, gửi tin, đánh dấu đã đọc, upload file). Nếu mỗi nơi tự kiểm tra,
/// sớm muộn có endpoint quên và tin nhắn riêng tư của người khác bị lộ.
///
/// Quy ước quan trọng: <c>roomId</c> trong URL có thể là:
///   - Id của một <see cref="ChatRoom"/> đã có, HOẶC
///   - Id của một <c>Commission</c> — FE gọi /workroom/{commissionId}.
/// Lớp này tự phân giải và TẠO PHÒNG khi cần (lazy provisioning), nhờ vậy không cần
/// endpoint "tạo phòng" riêng và phòng luôn tồn tại đúng lúc người dùng mở workroom.
/// </summary>
public interface IChatRoomService
{
    /// <summary>
    /// Phân giải roomId thành phòng chat và kiểm tra quyền truy cập của user.
    /// </summary>
    /// <returns>
    /// <c>(Room, Error)</c>: <c>Room</c> null nghĩa là không truy cập được, khi đó
    /// <c>Error</c> là thông báo tiếng Việt để trả thẳng cho client.
    /// </returns>
    Task<(ChatRoom? Room, string? Error)> ResolveRoomAsync(
        Guid roomId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra user có phải thành viên phòng không.</summary>
    Task<bool> IsMemberAsync(
        Guid roomId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Vai trò của user trong phòng: "Client", "Creator", "Moderator".</summary>
    Task<string> ResolveMemberRoleAsync(
        ChatRoom room,
        Guid userId,
        CancellationToken cancellationToken = default);
}

public class ChatRoomService : IChatRoomService
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<ChatRoomService> _logger;

    public ChatRoomService(IApplicationDbContext db, ILogger<ChatRoomService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<(ChatRoom? Room, string? Error)> ResolveRoomAsync(
        Guid roomId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (roomId == Guid.Empty)
        {
            return (null, "Thiếu mã phòng chat.");
        }

        if (userId == Guid.Empty)
        {
            return (null, "Bạn cần đăng nhập để truy cập phòng chat.");
        }

        // 1) Thử hiểu roomId là Id của phòng chat.
        var room = await _db.ChatRooms
            .FirstOrDefaultAsync(r => r.Id == roomId && !r.IsDeleted, cancellationToken);

        // 2) Không có thì thử hiểu là Id của commission rồi lấy/tạo phòng tương ứng.
        room ??= await FindOrCreateCommissionRoomAsync(roomId, cancellationToken);

        if (room is null)
        {
            return (null, "Không tìm thấy phòng chat.");
        }

        var isMember = await IsMemberAsync(room.Id, userId, cancellationToken);
        if (!isMember)
        {
            // Trả cùng một thông báo cho "không tồn tại" và "không có quyền" để không
            // tiết lộ sự tồn tại của phòng cho người ngoài.
            return (null, "Bạn không phải thành viên của phòng chat này.");
        }

        return (room, null);
    }

    public async Task<bool> IsMemberAsync(
        Guid roomId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var isExplicitMember = await _db.ChatRoomMembers
            .AsNoTracking()
            .AnyAsync(
                m => m.RoomId == roomId && m.UserId == userId && !m.HasLeft && !m.IsDeleted,
                cancellationToken);

        if (isExplicitMember)
        {
            return true;
        }

        // Fallback: thành viên theo quan hệ nghiệp vụ (client/creator của commission).
        // Cần thiết vì phòng có thể mới được tạo và chưa kịp ghi bảng thành viên.
        var room = await _db.ChatRooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roomId && !r.IsDeleted, cancellationToken);

        if (room?.CommissionId is null)
        {
            return false;
        }

        var creatorProfileId = await _db.CreatorProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.IsDeleted)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return await _db.Commissions
            .AsNoTracking()
            .AnyAsync(
                c => c.Id == room.CommissionId
                     && (c.ClientId == userId || c.CreatorId == userId || (creatorProfileId != Guid.Empty && c.CreatorId == creatorProfileId))
                     && !c.IsDeleted,
                cancellationToken);
    }

    public async Task<string> ResolveMemberRoleAsync(
        ChatRoom room,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var stored = await _db.ChatRoomMembers
            .AsNoTracking()
            .Where(m => m.RoomId == room.Id && m.UserId == userId && !m.IsDeleted)
            .Select(m => m.MemberRole)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(stored))
        {
            return stored;
        }

        if (room.CommissionId is null)
        {
            return "Client";
        }

        var commission = await _db.Commissions
            .AsNoTracking()
            .Where(c => c.Id == room.CommissionId)
            .Select(c => new { c.ClientId, c.CreatorId })
            .FirstOrDefaultAsync(cancellationToken);

        if (commission is null)
        {
            return "Client";
        }

        var creatorProfileId = await _db.CreatorProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.IsDeleted)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (commission.CreatorId == userId || (creatorProfileId != Guid.Empty && commission.CreatorId == creatorProfileId))
        {
            return "Creator";
        }

        return commission.ClientId == userId ? "Client" : "Moderator";
    }

    /// <summary>
    /// Tìm phòng của một commission, tạo mới nếu chưa có.
    ///
    /// Chống trùng: dùng lại phòng đã có bất kể trạng thái, và bọc trong try/catch
    /// để hai request đồng thời không tạo 2 phòng (một request sẽ thua và đọc lại).
    /// </summary>
    private async Task<ChatRoom?> FindOrCreateCommissionRoomAsync(
        Guid commissionId,
        CancellationToken cancellationToken)
    {
        var existing = await _db.ChatRooms
            .FirstOrDefaultAsync(
                r => r.CommissionId == commissionId && !r.IsDeleted,
                cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var commission = await _db.Commissions
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == commissionId && !c.IsDeleted, cancellationToken);

        if (commission is null)
        {
            return null;
        }

        var creatorUserId = await _db.CreatorProfiles
            .AsNoTracking()
            .Where(p => p.Id == commission.CreatorId && !p.IsDeleted)
            .Select(p => p.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        var room = new ChatRoom
        {
            RoomType = ChatRoomType.Commission,
            Title = commission.Title,
            CommissionId = commission.Id
        };

        _db.ChatRooms.Add(room);

        // Thêm sẵn 2 thành viên để lần sau không phải suy ra từ commission.
        var members = new List<ChatRoomMember>
        {
            new() { RoomId = room.Id, UserId = commission.ClientId, MemberRole = "Client" },
            new() { RoomId = room.Id, UserId = creatorUserId != Guid.Empty ? creatorUserId : commission.CreatorId, MemberRole = "Creator" }
        };

        _db.ChatRoomMembers.AddRange(members);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return room;
        }
        catch (DbUpdateException ex)
        {
            // Thua cuộc đua tạo phòng — đọc lại bản ghi đã có.
            _logger.LogWarning(ex,
                "Tạo phòng chat cho commission {CommissionId} thất bại, có thể đã tồn tại.",
                commissionId);

            _db.Entry(room).State = EntityState.Detached;
            foreach (var member in members)
            {
                _db.Entry(member).State = EntityState.Detached;
            }

            return await _db.ChatRooms
                .FirstOrDefaultAsync(
                    r => r.CommissionId == commissionId && !r.IsDeleted,
                    cancellationToken);
        }
    }
}
