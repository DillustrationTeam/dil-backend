using ArtCommission.API.Hubs;
using ArtCommission.Application.Ai.ReferenceSketch;
using Microsoft.AspNetCore.SignalR;

namespace ArtCommission.API.Common;

/// <summary>
/// Cài đặt <see cref="IReferenceSketchProgressPublisher"/>: đẩy tiến trình sinh ảnh phác thảo
/// bố cục real-time qua <see cref="NotificationHub"/>, event <c>AiReferenceSketchProgress</c>,
/// vào đúng group <c>user-{clientUserId}</c> (xem PLAN.md T04).
///
/// Đặt ở tầng API (không phải Infrastructure, nơi job thực thi) vì gắn chặt với
/// <see cref="NotificationHub"/> — Infrastructure không được tham chiếu API.
/// </summary>
public class SignalRReferenceSketchProgressPublisher : IReferenceSketchProgressPublisher
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<SignalRReferenceSketchProgressPublisher> _logger;

    public SignalRReferenceSketchProgressPublisher(
        IHubContext<NotificationHub> hub,
        ILogger<SignalRReferenceSketchProgressPublisher> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task PublishAsync(
        Guid clientUserId,
        ReferenceSketchProgressEvent progressEvent,
        CancellationToken cancellationToken = default)
    {
        if (clientUserId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _hub.Clients
                .Group(NotificationHub.UserGroup(clientUserId))
                .SendAsync("AiReferenceSketchProgress", progressEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            // Khách offline/mất kết nối không được làm hỏng job — endpoint GET trạng thái
            // (fallback poll phía FE) vẫn đọc được tiến trình mới nhất từ DB.
            _logger.LogWarning(
                ex, "Không đẩy được tiến trình sinh ảnh phác thảo. ClientId={ClientId} GenerationId={GenerationId}",
                clientUserId, progressEvent.GenerationId);
        }
    }
}
