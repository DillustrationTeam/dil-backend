using System.Security.Claims;
using ArtCommission.API.Hubs;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Domain.Entities.Notifications;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.API.Common;

/// <summary>
/// Cài đặt <see cref="INotificationPublisher"/> (UC45):
/// ghi thông báo vào DB rồi đẩy real-time vào group <c>user-{userId}</c>.
///
/// Đặt ở tầng API (không phải Infrastructure) vì nó gắn chặt với
/// <see cref="NotificationHub"/> — mà Infrastructure không được tham chiếu API
/// (xem <c>docs/01-architecture.md</c> — chiều phụ thuộc chỉ đi vào trong).
///
/// Thứ tự cố ý: GHI DB TRƯỚC, ĐẨY SIGNALR SAU.
/// Nếu đẩy trước rồi ghi lỗi, người dùng thấy thông báo nhưng mở drawer lại không có.
/// Đẩy thất bại (người dùng offline) chỉ ghi log — thông báo vẫn nằm trong DB để đọc lại.
/// </summary>
public class SignalRNotificationPublisher : INotificationPublisher
{
    /// <summary>Khớp <c>NotificationConfiguration</c> — Title 200, Body 1000 ký tự.</summary>
    private const int MaxTitleLength = 200;
    private const int MaxBodyLength = 1000;
    private const int MaxDedupKeyLength = 200;

    private readonly AppDbContext _db;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<SignalRNotificationPublisher> _logger;

    public SignalRNotificationPublisher(
        AppDbContext db,
        IHubContext<NotificationHub> hub,
        ILogger<SignalRNotificationPublisher> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    public async Task<NotificationDto?> PublishAsync(
        Guid userId,
        NotificationType notificationType,
        string title,
        string body,
        string? refType = null,
        Guid? refId = null,
        NotificationChannel channel = NotificationChannel.InApp,
        string? dedupKey = null,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            _logger.LogWarning("Bỏ qua phát thông báo: userId rỗng. Type={Type}", notificationType);
            return null;
        }

        var dedup = string.IsNullOrWhiteSpace(dedupKey) ? null : dedupKey.Trim();
        dedup = dedup is { Length: > MaxDedupKeyLength } ? dedup[..MaxDedupKeyLength] : dedup;

        // Đã phát rồi thì trả bản ghi cũ — chống trùng khi job/retry chạy lại.
        if (dedup is not null)
        {
            var duplicated = await _db.Notifications
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    n => n.UserId == userId && n.DedupKey == dedup && !n.IsDeleted,
                    cancellationToken);

            if (duplicated is not null)
            {
                _logger.LogDebug(
                    "Thông báo trùng, bỏ qua. UserId={UserId} DedupKey={DedupKey}", userId, dedup);
                return NotificationMapper.ToDto(duplicated);
            }
        }

        var entity = new Notification
        {
            UserId = userId,
            NotificationType = notificationType,
            NotificationTitle = Truncate(title, MaxTitleLength),
            Body = Truncate(body, MaxBodyLength),
            RefType = string.IsNullOrWhiteSpace(refType) ? null : refType.Trim(),
            RefId = refId,
            Channel = channel,
            DedupKey = dedup
        };

        _db.Notifications.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = NotificationMapper.ToDto(entity);

        await PushToUserAsync(userId, dto, cancellationToken);

        _logger.LogInformation(
            "Đã phát thông báo. UserId={UserId} Type={Type} NotificationId={Id} RefType={RefType} RefId={RefId}",
            userId, notificationType, entity.Id, entity.RefType, entity.RefId);

        return dto;
    }

    /// <summary>
    /// Đẩy real-time vào ĐÚNG group của người nhận.
    /// TUYỆT ĐỐI không dùng <c>Clients.All</c> — sẽ lộ thông báo cho người khác.
    /// </summary>
    private async Task PushToUserAsync(Guid userId, NotificationDto dto, CancellationToken cancellationToken)
    {
        try
        {
            await _hub.Clients
                .Group(NotificationHub.UserGroup(userId))
                .SendAsync("ReceiveNotification", dto, cancellationToken);
        }
        catch (Exception ex)
        {
            // Người dùng đang offline không được làm hỏng nghiệp vụ gọi vào đây.
            _logger.LogWarning(
                ex, "Không đẩy được thông báo real-time. UserId={UserId} NotificationId={Id}",
                userId, dto.NotificationId);
        }
    }

    private static string Truncate(string? value, int maxLength)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
