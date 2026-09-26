using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ArtCommission.API.Hubs;

/// <summary>
/// UC45 — SignalR hub đẩy thông báo real-time tới đúng người dùng.
///
/// Nguyên tắc bắt buộc (theo <c>.agents/05-playbook.md</c> Recipe 4):
///   - JWT lấy từ query-string lúc handshake (WebSocket không gửi được header).
///   - Mọi thông báo đi vào group <c>user-{userId}</c> — TUYỆT ĐỐI không dùng <c>Clients.All</c>,
///     nếu không mọi người sẽ nhận được thông báo của người khác.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    /// <summary>Tên group chứa mọi connection của một người dùng.</summary>
    public static string UserGroup(Guid userId) => $"user-{userId}";

    /// <summary>
    /// Client gọi ngay sau khi kết nối. Không nhận tham số userId từ client —
    /// lấy từ token, nếu không người dùng có thể đăng ký nhận thông báo của người khác.
    /// </summary>
    public async Task Subscribe()
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
    }

    public async Task Unsubscribe()
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroup(userId));
    }

    private Guid GetUserId()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? Context.User?.FindFirst("sub")?.Value;

        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}
