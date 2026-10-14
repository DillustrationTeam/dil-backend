using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Application.Event.Validators;
using ArtCommission.Domain.Entities.Event;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using INotificationPublisher = ArtCommission.Application.Notifications.Common.INotificationPublisher;

namespace ArtCommission.Application.Event.Commands;

public record UpdateInvitationStatusCommand(
    Guid InvitationId,
    Guid CurrentUserId,
    InvitationStatus Status,
    bool IsAdmin = false
) : IRequest<(bool Success, InvitationDto? Data, string[] Errors)>;

public class UpdateInvitationStatusCommandHandler
    : IRequestHandler<UpdateInvitationStatusCommand, (bool Success, InvitationDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<UpdateInvitationStatusCommandHandler> _logger;

    public UpdateInvitationStatusCommandHandler(
        IApplicationDbContext db,
        INotificationPublisher notifications,
        ILogger<UpdateInvitationStatusCommandHandler> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<(bool Success, InvitationDto? Data, string[] Errors)> Handle(
        UpdateInvitationStatusCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new UpdateInvitationStatusCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, null, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var invitation = await _db.Invitations
            .Include(i => i.Event)
            .Include(i => i.SentFromAdmin)
            .Include(i => i.SentToCreator)
                .ThenInclude(c => c!.User)
            .FirstOrDefaultAsync(i => i.Id == request.InvitationId && !i.IsDeleted, cancellationToken);

        if (invitation == null)
        {
            return (false, null, ["Không tìm thấy lời mời hoặc lời mời đã bị xóa."]);
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            return (false, null, [$"Lời mời này đã được xử lý trước đó với trạng thái '{invitation.Status}'."]);
        }

        var now = DateTimeOffset.UtcNow;
        var creatorUserId = invitation.SentToCreator?.UserId;
        var isTargetCreator = invitation.SentToCreatorId == request.CurrentUserId || creatorUserId == request.CurrentUserId;
        var isSenderAdmin = invitation.SentFromAdminId == request.CurrentUserId || request.IsAdmin;

        switch (request.Status)
        {
            case InvitationStatus.Accepted:
                if (!isTargetCreator)
                {
                    return (false, null, ["Chỉ Creator được mời mới có quyền chấp nhận lời mời này."]);
                }

                if (invitation.Event != null && now >= invitation.Event.JudgingStartAt)
                {
                    return (false, null, [$"Không thể chấp nhận lời mời làm giám khảo khi giai đoạn chấm thi đã bắt đầu (JudgingStartAt: {invitation.Event.JudgingStartAt:u})."]);
                }

                var existingJury = await _db.Juries
                    .FirstOrDefaultAsync(j => j.EventId == invitation.EventId && j.CreatorId == invitation.SentToCreatorId && !j.IsDeleted, cancellationToken);

                var hasHeadJury = await _db.Juries
                    .AnyAsync(j => j.EventId == invitation.EventId && j.IsHeadJury && !j.IsDeleted, cancellationToken);

                var hasAnyJury = await _db.Juries
                    .AnyAsync(j => j.EventId == invitation.EventId && !j.IsDeleted, cancellationToken);

                bool isHeadJuryToSet = invitation.IsHeadJury;
                if (!hasAnyJury && !hasHeadJury)
                {
                    isHeadJuryToSet = true;
                }
                else if (isHeadJuryToSet && hasHeadJury)
                {
                    isHeadJuryToSet = false;
                }

                if (existingJury == null)
                {
                    var jury = new Jury
                    {
                        Id = Guid.NewGuid(),
                        EventId = invitation.EventId,
                        CreatorId = invitation.SentToCreatorId,
                        IsHeadJury = isHeadJuryToSet,
                        CreatedAt = now,
                        IsDeleted = false
                    };
                    _db.Juries.Add(jury);
                }
                else if (isHeadJuryToSet && !existingJury.IsHeadJury)
                {
                    existingJury.IsHeadJury = true;
                    existingJury.UpdatedAt = now;
                }
                break;

            case InvitationStatus.Declined:
                if (!isTargetCreator)
                {
                    return (false, null, ["Chỉ Creator được mời mới có quyền từ chối lời mời này."]);
                }
                break;

            case InvitationStatus.Canceled:
                if (!isSenderAdmin)
                {
                    return (false, null, ["Chỉ Quản trị viên mới có quyền hủy lời mời này."]);
                }
                break;

            default:
                return (false, null, [$"Trạng thái '{request.Status}' không hợp lệ để cập nhật lời mời."]);
        }

        // 4. Cập nhật trạng thái lời mời
        invitation.Status = request.Status;
        invitation.RespondedAt = now;
        invitation.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        // 5. Chuẩn bị DTO trả về
        var creatorUser = invitation.SentToCreator?.User;
        var creator = invitation.SentToCreator;

        var data = new InvitationDto
        {
            Id = invitation.Id,
            EventId = invitation.EventId,
            EventTitle = invitation.Event?.Title,
            EventBannerUrl = invitation.Event?.BannerUrl,
            SentFromAdminId = invitation.SentFromAdminId,
            SentFromAdminName = invitation.SentFromAdmin?.FullName,
            SentToCreatorId = invitation.SentToCreatorId,
            SentToCreatorName = creatorUser != null && !string.IsNullOrEmpty(creatorUser.FullName)
                ? creatorUser.FullName
                : creator?.DisplayName,
            SentToCreatorEmail = creatorUser?.Email,
            SentToCreatorAvatarUrl = creator?.BannerUrl,
            IsHeadJury = invitation.IsHeadJury,
            Status = invitation.Status.ToString(),
            CreatedAt = invitation.CreatedAt,
            RespondedAt = invitation.RespondedAt
        };

        // 6. Gửi thông báo tương ứng với trạng thái thay đổi
        try
        {
            var eventTitle = invitation.Event?.Title ?? "Sự kiện";
            var creatorName = data.SentToCreatorName ?? "Creator";

            switch (request.Status)
            {
                case InvitationStatus.Accepted:
                    await _notifications.PublishAsync(
                        userId: invitation.SentFromAdminId,
                        notificationType: NotificationType.InvitationAccepted,
                        title: "Lời mời giám khảo đã được chấp nhận",
                        body: $"Creator '{creatorName}' đã đồng ý tham gia Ban giám khảo sự kiện '{eventTitle}'.",
                        refType: "Invitation",
                        refId: invitation.Id,
                        channel: NotificationChannel.InApp,
                        dedupKey: $"InvitationAccepted:{invitation.Id}:{invitation.SentFromAdminId}",
                        cancellationToken: cancellationToken);
                    break;

                case InvitationStatus.Declined:
                    await _notifications.PublishAsync(
                        userId: invitation.SentFromAdminId,
                        notificationType: NotificationType.InvitationDeclined,
                        title: "Lời mời giám khảo đã bị từ chối",
                        body: $"Creator '{creatorName}' đã từ chối lời mời tham gia Ban giám khảo sự kiện '{eventTitle}'.",
                        refType: "Invitation",
                        refId: invitation.Id,
                        channel: NotificationChannel.InApp,
                        dedupKey: $"InvitationDeclined:{invitation.Id}:{invitation.SentFromAdminId}",
                        cancellationToken: cancellationToken);
                    break;

                case InvitationStatus.Canceled:
                    if (creatorUserId.HasValue)
                    {
                        await _notifications.PublishAsync(
                            userId: creatorUserId.Value,
                            notificationType: NotificationType.InvitationCanceled,
                            title: "Lời mời giám khảo đã bị hủy",
                            body: $"Lời mời tham gia Ban giám khảo sự kiện '{eventTitle}' đã bị hủy bởi Quản trị viên.",
                            refType: "Invitation",
                            refId: invitation.Id,
                            channel: NotificationChannel.InApp,
                            dedupKey: $"InvitationCanceled:{invitation.Id}:{creatorUserId.Value}",
                            cancellationToken: cancellationToken);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể gửi thông báo cập nhật lời mời cho invitationId={InvitationId}, status={Status}.", invitation.Id, request.Status);
        }

        return (true, data, []);
    }
}
