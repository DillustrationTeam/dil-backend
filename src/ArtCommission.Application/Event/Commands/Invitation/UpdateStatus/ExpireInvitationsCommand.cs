using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.Validators;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using INotificationPublisher = ArtCommission.Application.Notifications.Common.INotificationPublisher;

namespace ArtCommission.Application.Event.Commands;

public record ExpireInvitationsCommand(
    TimeSpan? MaxLifetime = null,
    bool ExpireWhenEventEnded = true,
    int BatchSize = 100
) : IRequest<(int ExpiredCount, string[] Errors)>;

public class ExpireInvitationsCommandHandler
    : IRequestHandler<ExpireInvitationsCommand, (int ExpiredCount, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<ExpireInvitationsCommandHandler> _logger;

    public ExpireInvitationsCommandHandler(
        IApplicationDbContext db,
        INotificationPublisher notifications,
        ILogger<ExpireInvitationsCommandHandler> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<(int ExpiredCount, string[] Errors)> Handle(
        ExpireInvitationsCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new ExpireInvitationsCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (0, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var now = DateTimeOffset.UtcNow;
        var maxLifetime = request.MaxLifetime ?? TimeSpan.FromDays(7);
        var lifetimeThreshold = now - maxLifetime;
        var batchSize = request.BatchSize;

        var pendingQuery = _db.Invitations
            .Include(i => i.Event)
            .Include(i => i.SentToCreator)
            .Where(i => !i.IsDeleted && i.Status == InvitationStatus.Pending);

        var expiredInvitations = await pendingQuery
            .Where(i => i.CreatedAt <= lifetimeThreshold ||
                        (request.ExpireWhenEventEnded && i.Event != null &&
                         (i.Event.ResultAnnouncementAt <= now ||
                          i.Event.Status == EventStatus.Ended ||
                          i.Event.IsDeleted)))
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (expiredInvitations.Count == 0)
        {
            return (0, Array.Empty<string>());
        }

        foreach (var invitation in expiredInvitations)
        {
            invitation.Status = InvitationStatus.Expired;
            invitation.RespondedAt ??= now;
            invitation.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Gửi thông báo đến các Creator có lời mời bị hết hạn
        foreach (var invitation in expiredInvitations)
        {
            var creatorUserId = invitation.SentToCreator?.UserId;
            if (creatorUserId.HasValue)
            {
                try
                {
                    await _notifications.PublishAsync(
                        userId: creatorUserId.Value,
                        notificationType: NotificationType.InvitationExpired,
                        title: "Lời mời giám khảo đã hết hạn",
                        body: $"Lời mời tham gia Ban giám khảo sự kiện '{invitation.Event?.Title}' đã hết hạn phản hồi.",
                        refType: "Invitation",
                        refId: invitation.Id,
                        channel: NotificationChannel.InApp,
                        dedupKey: $"InvitationExpired:{invitation.Id}:{creatorUserId.Value}",
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Không thể gửi thông báo hết hạn lời mời cho creatorUserId={UserId}, invitationId={InvitationId}.", creatorUserId.Value, invitation.Id);
                }
            }
        }

        return (expiredInvitations.Count, Array.Empty<string>());
    }
}
