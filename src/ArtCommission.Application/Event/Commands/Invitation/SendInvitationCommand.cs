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

public record SendInvitationCommand(
    Guid EventId,
    Guid AdminId,
    string Email
) : IRequest<(bool Success, InvitationDto? Data, string[] Errors)>
{
    public SendInvitationCommand(Guid eventId, Guid adminId, InviteJuryDto dto)
        : this(eventId, adminId, dto.Email) { }

    public SendInvitationCommand(Guid adminId, SendInvitationDto dto)
        : this(dto.EventId, adminId, dto.Email) { }
}

public class SendInvitationCommandHandler
    : IRequestHandler<SendInvitationCommand, (bool Success, InvitationDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<SendInvitationCommandHandler> _logger;

    public SendInvitationCommandHandler(
        IApplicationDbContext db,
        INotificationPublisher notifications,
        ILogger<SendInvitationCommandHandler> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<(bool Success, InvitationDto? Data, string[] Errors)> Handle(
        SendInvitationCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new SendInvitationCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, null, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        // 1. Kiểm tra sự kiện
        var platformEvent = await _db.PlatformEvents
            .FirstOrDefaultAsync(e => e.Id == request.EventId && !e.IsDeleted, cancellationToken);

        if (platformEvent == null)
        {
            return (false, null, ["Sự kiện không tồn tại hoặc đã bị xóa."]);
        }

        var now = DateTimeOffset.UtcNow;
        if (now >= platformEvent.JudgingStartAt)
        {
            return (false, null, [$"Không thể gửi lời mời làm giám khảo khi giai đoạn chấm thi đã bắt đầu (JudgingStartAt: {platformEvent.JudgingStartAt:u})."]);
        }

        var normalizedEmail = request.Email.Trim().ToLower();

        // 3. Tìm tài khoản người dùng theo email
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalizedEmail && !u.IsDeleted, cancellationToken);

        if (user == null)
        {
            return (false, null, [$"Không tìm thấy tài khoản người dùng với email '{request.Email}'."]);
        }

        // 4. Tìm hồ sơ Creator tương ứng và kiểm tra vai trò CREATOR
        var creator = await _db.CreatorProfiles
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == user.Id && !c.IsDeleted, cancellationToken);

        if (creator == null)
        {
            return (false, null, ["Người dùng này chưa đăng ký hoặc chưa có hồ sơ Creator."]);
        }

        var creatorRoleId = await _db.Set<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>()
            .Where(r => r.Name == UserRoleNames.Creator)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var hasCreatorRole = await _db.Set<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>()
            .AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == creatorRoleId, cancellationToken);

        if (!hasCreatorRole)
        {
            return (false, null, ["Người dùng được mời làm giám khảo phải có vai trò CREATOR."]);
        }

        // 5. Kiểm tra xem đã là giám khảo của sự kiện chưa
        var isAlreadyJury = await _db.Juries
            .AnyAsync(j => j.EventId == request.EventId && j.CreatorId == creator.Id && !j.IsDeleted, cancellationToken);

        if (isAlreadyJury)
        {
            return (false, null, ["Creator này hiện đã là thành viên ban giám khảo của sự kiện."]);
        }

        // 6. Kiểm tra xem đã có lời mời Pending chưa
        var hasPendingInvitation = await _db.Invitations
            .AnyAsync(i => i.EventId == request.EventId && i.SentToCreatorId == creator.Id && i.Status == InvitationStatus.Pending && !i.IsDeleted, cancellationToken);

        if (hasPendingInvitation)
        {
            return (false, null, ["Đã tồn tại một lời mời đang chờ phản hồi cho Creator này trong sự kiện."]);
        }

        // 7. Logic Trưởng ban giám khảo (Head Jury):
        // Nếu sự kiện chưa có bất kỳ giám khảo nào và chưa có ai được mời làm Head Jury, người đầu tiên được mời sẽ là Head Jury.
        var hasAnyJury = await _db.Juries
            .AnyAsync(j => j.EventId == request.EventId && !j.IsDeleted, cancellationToken);

        var hasHeadJury = await _db.Juries
            .AnyAsync(j => j.EventId == request.EventId && j.IsHeadJury && !j.IsDeleted, cancellationToken);

        var hasPendingHeadJury = await _db.Invitations
            .AnyAsync(i => i.EventId == request.EventId && i.IsHeadJury && i.Status == InvitationStatus.Pending && !i.IsDeleted, cancellationToken);

        bool isHeadJury = !hasAnyJury && !hasHeadJury && !hasPendingHeadJury;

        // 8. Tạo lời mời mới
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            EventId = request.EventId,
            SentFromAdminId = request.AdminId,
            SentToCreatorId = creator.Id,
            IsHeadJury = isHeadJury,
            Status = InvitationStatus.Pending,
            CreatedAt = now,
            IsDeleted = false
        };

        _db.Invitations.Add(invitation);
        await _db.SaveChangesAsync(cancellationToken);

        // 9. Lấy tên Admin gửi
        var admin = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.AdminId, cancellationToken);

        // 10. Gửi thông báo đến Creator
        try
        {
            var juryTitle = invitation.IsHeadJury ? "Trưởng ban giám khảo" : "Giám khảo";
            var adminName = admin?.FullName ?? "Quản trị viên";
            await _notifications.PublishAsync(
                userId: user.Id,
                notificationType: NotificationType.InvitationReceived,
                title: "Lời mời tham gia Ban giám khảo sự kiện",
                body: $"{adminName} đã gửi lời mời bạn tham gia làm {juryTitle} cho sự kiện '{platformEvent.Title}'.",
                refType: "Invitation",
                refId: invitation.Id,
                channel: NotificationChannel.InApp,
                dedupKey: $"InvitationReceived:{invitation.Id}:{user.Id}",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể gửi thông báo lời mời giám khảo cho userId={UserId}, invitationId={InvitationId}.", user.Id, invitation.Id);
        }

        var data = new InvitationDto
        {
            Id = invitation.Id,
            EventId = invitation.EventId,
            EventTitle = platformEvent.Title,
            EventBannerUrl = platformEvent.BannerUrl,
            SentFromAdminId = invitation.SentFromAdminId,
            SentFromAdminName = admin?.FullName,
            SentToCreatorId = creator.Id,
            SentToCreatorName = !string.IsNullOrEmpty(user.FullName) ? user.FullName : creator.DisplayName,
            SentToCreatorEmail = user.Email,
            SentToCreatorAvatarUrl = creator.BannerUrl,
            IsHeadJury = invitation.IsHeadJury,
            Status = invitation.Status.ToString(),
            CreatedAt = invitation.CreatedAt,
            RespondedAt = invitation.RespondedAt
        };

        return (true, data, []);
    }
}
