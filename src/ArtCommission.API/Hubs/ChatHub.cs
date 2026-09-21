using System.Security.Claims;
using ArtCommission.Application.Chat.Commands.MarkRoomRead;
using ArtCommission.Application.Chat.Commands.SendMessage;
using ArtCommission.Application.Chat.Common;
using ArtCommission.Application.Chat.Queries.GetChatRoom;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ArtCommission.API.Hubs;

/// <summary>
/// UC43 — SignalR hub cho Workroom Chat.
///
/// NGUYÊN TẮC BẮT BUỘC:
///   - JWT lấy từ query-string lúc handshake (WebSocket không gửi được header Authorization).
///   - Mọi phát tin đi vào group <c>workroom-{roomId}</c> — TUYỆT ĐỐI không <c>Clients.All</c>,
///     nếu không mọi người dùng sẽ đọc được tin nhắn riêng của phòng khác.
///   - Quyền vào group do SERVER quyết định: <see cref="JoinRoom"/> kiểm tra thành viên
///     trước khi <c>AddToGroupAsync</c>. Không tin <c>roomId</c> client gửi.
///   - Gửi tin đi qua cùng handler với REST (<see cref="SendMessageCommand"/>) để
///     luật nghiệp vụ không bị lệch giữa hai đường vào.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly ISender _mediator;
    private readonly IChatRoomService _chatRoomService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        ISender mediator,
        IChatRoomService chatRoomService,
        ILogger<ChatHub> logger)
    {
        _mediator = mediator;
        _chatRoomService = chatRoomService;
        _logger = logger;
    }

    /// <summary>Tên group của một phòng chat.</summary>
    public static string RoomGroup(Guid roomId) => $"workroom-{roomId}";

    /// <summary>
    /// Vào phòng để nhận tin real-time. Server tự kiểm tra tư cách thành viên.
    /// </summary>
    public async Task JoinRoom(Guid roomId)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return;
        }

        var (room, error) = await _chatRoomService.ResolveRoomAsync(
            roomId, userId, Context.ConnectionAborted);

        if (room is null)
        {
            // Báo lỗi riêng cho người gọi, KHÔNG ném ra ngoài để tránh ngắt kết nối.
            await Clients.Caller.SendAsync("RoomJoinFailed", error ?? "Không vào được phòng.", Context.ConnectionAborted);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(room.Id), Context.ConnectionAborted);

        await Clients.Caller.SendAsync("RoomJoined", room.Id, Context.ConnectionAborted);
    }

    public async Task LeaveRoom(Guid roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroup(roomId), Context.ConnectionAborted);
    }

    /// <summary>
    /// Gửi tin nhắn. Tham số <paramref name="messageType"/> để trống thì mặc định "Text".
    /// </summary>
    public async Task SendMessage(Guid roomId, string body, string? messageType = null)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return;
        }

        var (success, message, errors) = await _mediator.Send(
            new SendMessageCommand(userId, roomId, body ?? string.Empty, messageType),
            Context.ConnectionAborted);

        if (!success || message is null)
        {
            await Clients.Caller.SendAsync(
                "MessageFailed",
                string.Join(" ", errors),
                Context.ConnectionAborted);
            return;
        }

        var room = await ResolveRoomIdAsync(message.RoomId, roomId, userId);

        if (room == Guid.Empty)
        {
            await Clients.Caller.SendAsync(
                "MessageFailed",
                "Không xác định được phòng để phát tin nhắn.",
                Context.ConnectionAborted);
            return;
        }

        // Phát cho CẢ phòng, kể cả người gửi: nhờ vậy mọi client hiển thị tin theo
        // cùng một thứ tự do server quyết định, không phụ thuộc đồng hồ máy khách.
        await Clients.Group(RoomGroup(room)).SendAsync("ReceiveMessage", message, Context.ConnectionAborted);
    }

    /// <summary>Đánh dấu đã đọc; phát trạng thái cho cả phòng để hiện "đã xem".</summary>
    public async Task MarkRead(Guid roomId)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return;
        }

        var (success, receipt, _) = await _mediator.Send(
            new MarkRoomReadCommand(userId, roomId),
            Context.ConnectionAborted);

        if (!success || receipt is null)
        {
            return;
        }

        await Clients.Group(RoomGroup(receipt.RoomId)).SendAsync(
            "MessageRead",
            userId,
            receipt.LastReadAt,
            Context.ConnectionAborted);
    }

    /// <summary>Báo "đang gõ". Không lưu DB — chỉ là tín hiệu nhất thời.</summary>
    public async Task Typing(Guid roomId)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return;
        }

        // Chỉ phát khi người gọi thực sự là thành viên phòng.
        var isMember = await _chatRoomService.IsMemberAsync(roomId, userId, Context.ConnectionAborted);
        if (!isMember)
        {
            return;
        }

        await Clients.OthersInGroup(RoomGroup(roomId)).SendAsync(
            "UserTyping",
            userId,
            Context.ConnectionAborted);
    }

    /// <summary>
    /// Lấy Id phòng thật để phát tin. Tin nhắn luôn có RoomId, nhưng vẫn dự phòng
    /// trường hợp dữ liệu cũ chưa gán phòng.
    /// </summary>
    private async Task<Guid> ResolveRoomIdAsync(Guid? messageRoomId, Guid fallbackRoomId, Guid userId)
    {
        if (messageRoomId.HasValue && messageRoomId.Value != Guid.Empty)
        {
            return messageRoomId.Value;
        }

        var (room, _) = await _chatRoomService.ResolveRoomAsync(
            fallbackRoomId, userId, Context.ConnectionAborted);

        if (room is not null)
        {
            return room.Id;
        }

        _logger.LogWarning(
            "Không xác định được phòng chat khi phát tin: fallbackRoomId={RoomId}, userId={UserId}.",
            fallbackRoomId, userId);

        return Guid.Empty;
    }

    private Guid GetUserId()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? Context.User?.FindFirst("sub")?.Value;

        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    /// <summary>
    /// Kiểm tra quyền vào group khi client tự gọi <c>JoinRoom</c> bằng roomId bất kỳ.
    /// Giữ lại phương thức này để handler <c>GetChatRoomQuery</c> không bị "unused import".
    /// </summary>
    internal async Task<bool> CanAccessRoomAsync(Guid roomId)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return false;
        }

        var (success, _, _) = await _mediator.Send(
            new GetChatRoomQuery(userId, roomId),
            Context.ConnectionAborted);

        return success;
    }
}
