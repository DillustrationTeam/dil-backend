using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Dispute.SubmissionDispute.Validators;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using INotificationPublisher = ArtCommission.Application.Notifications.Common.INotificationPublisher;

namespace ArtCommission.Application.Dispute.SubmissionDispute.Commands;

public record CreateSubmissionDisputeCommand(
    Guid SubmissionId,
    Guid RaisedById,
    string Reason,
    string? Description = null,
    List<string>? EvidenceUrls = null,
    string Priority = "Medium",
    string? Category = null
) : IRequest<(bool Success, string Message, Guid? DisputeId, string[] Errors)>;

public class CreateSubmissionDisputeCommandHandler
    : IRequestHandler<CreateSubmissionDisputeCommand, (bool Success, string Message, Guid? DisputeId, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly INotificationPublisher? _notifPublisher;

    public CreateSubmissionDisputeCommandHandler(
        IApplicationDbContext db,
        INotificationPublisher? notifPublisher = null)
    {
        _db = db;
        _notifPublisher = notifPublisher;
    }

    public async Task<(bool Success, string Message, Guid? DisputeId, string[] Errors)> Handle(
        CreateSubmissionDisputeCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new CreateSubmissionDisputeCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, "Dữ liệu không hợp lệ.", null, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var submission = await _db.EventSubmissions
            .Include(s => s.Event)
            .Include(s => s.Artwork)
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId, cancellationToken);

        if (submission == null)
        {
            return (false, "Không tìm thấy bài dự thi.", null, ["Bài dự thi không tồn tại."]);
        }

        if (submission.Event == null || submission.Event.IsDeleted)
        {
            return (false, "Sự kiện của bài dự thi không tồn tại hoặc đã bị xóa.", null, ["Sự kiện không hợp lệ."]);
        }

        if (submission.IsDisqualified)
        {
            return (false, "Bài dự thi này đã bị truất quyền thi đấu.", null, ["Bài dự thi đã bị truất quyền thi đấu."]);
        }

        var (category, derivedPriority) = CreateSubmissionDisputeCommandValidator.ResolveCategoryAndPriority(request.Category);
        string trimmedReason = request.Reason.Trim();

        var currentArtworkImageUrl = submission.Artwork?.ImageUrl ?? submission.Artwork?.ThumbnailUrl;

        // Tránh spam trùng lặp trên cùng danh mục đang chờ xử lý (vẫn cho phép khiếu nại danh mục khác, sau khi cập nhật bài hoặc sau khi đã xử lý)
        var activeDisputes = await _db.SubmissionDisputes
            .AsNoTracking()
            .Where(d =>
                !d.IsDeleted &&
                d.SubmissionId == request.SubmissionId &&
                d.RaisedById == request.RaisedById &&
                (d.Status == "Pending" || d.Status == "UnderReview"))
            .ToListAsync(cancellationToken);

        bool hasDuplicatePending = activeDisputes.Any(d =>
        {
            // Nếu bài dự thi đã thay đổi tác phẩm kể từ khi khiếu nại trước được gửi, cho phép gửi khiếu nại mới
            if (!string.IsNullOrWhiteSpace(currentArtworkImageUrl) &&
                !string.IsNullOrWhiteSpace(d.ArtworkImageUrlSnapshot) &&
                !string.Equals(d.ArtworkImageUrlSnapshot, currentArtworkImageUrl, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var existingCategory = string.IsNullOrWhiteSpace(d.Category) ? "Other" : d.Category.Trim();
            if (!string.Equals(existingCategory, category, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(category, "Other", StringComparison.OrdinalIgnoreCase))
            {
                var existingFirstLine = (d.Reason ?? string.Empty).Split('\n', 2)[0].Trim();
                return string.Equals(existingFirstLine, trimmedReason, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        });

        if (hasDuplicatePending)
        {
            return (false, "Bạn đã gửi khiếu nại cùng nội dung cho bài dự thi này và hiện đang được ban tổ chức xử lý.", null, ["Khiếu nại đang chờ xử lý đã tồn tại."]);
        }

        string fullReason = string.IsNullOrWhiteSpace(request.Description)
            ? trimmedReason
            : $"{trimmedReason}\nChi tiết: {request.Description.Trim()}";

        if (fullReason.Length > 2000)
        {
            fullReason = fullReason[..2000];
        }

        string? serializedEvidence = null;
        if (request.EvidenceUrls != null && request.EvidenceUrls.Count > 0)
        {
            serializedEvidence = JsonSerializer.Serialize(request.EvidenceUrls);
        }

        var dispute = new Domain.Entities.Dispute.SubmissionDispute
        {
            EventId = submission.EventId,
            SubmissionId = submission.Id,
            SubmissionTitleSnapshot = submission.Title,
            ArtworkImageUrlSnapshot = submission.Artwork?.ImageUrl ?? submission.Artwork?.ThumbnailUrl,
            RaisedById = request.RaisedById,
            Category = category,
            Priority = derivedPriority,
            Reason = fullReason,
            EvidenceUrls = serializedEvidence,
            Status = "Pending",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.SubmissionDisputes.Add(dispute);
        await _db.SaveChangesAsync(cancellationToken);

        if (_notifPublisher != null)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                var adminRoleIds = await _db.Set<IdentityRole<Guid>>()
                    .AsNoTracking()
                    .Where(r => r.Name == UserRoleNames.Administrator || r.Name == UserRoleNames.Moderator)
                    .Select(r => r.Id)
                    .ToListAsync(cancellationToken);

                if (adminRoleIds.Count > 0)
                {
                    var recipientUserIds = await _db.Set<IdentityUserRole<Guid>>()
                        .AsNoTracking()
                        .Where(ur => adminRoleIds.Contains(ur.RoleId))
                        .Select(ur => ur.UserId)
                        .Distinct()
                        .Join(
                            _db.Users.AsNoTracking().Where(u => !u.IsDeleted && (u.LockoutEnd == null || u.LockoutEnd <= now)),
                            uid => uid,
                            u => u.Id,
                            (uid, _) => uid)
                        .Where(uid => uid != request.RaisedById)
                        .ToListAsync(cancellationToken);

                    var categoryLabel = CreateSubmissionDisputeCommandValidator.GetCategoryDisplayName(category);
                    var subTitle = string.IsNullOrWhiteSpace(submission.Title) ? "Bài dự thi" : submission.Title;
                    var eventTitle = submission.Event.Title;

                    foreach (var recipientId in recipientUserIds)
                    {
                        await _notifPublisher.PublishAsync(
                            userId: recipientId,
                            notificationType: NotificationType.SubmissionDisputeCreated,
                            title: $"Khiếu nại mới ({categoryLabel}) — {eventTitle}",
                            body: $"Bài dự thi \"{subTitle}\" vừa nhận một khiếu nại mới (Mức độ: {derivedPriority}): {trimmedReason}",
                            refType: "SubmissionDispute",
                            refId: dispute.Id,
                            channel: NotificationChannel.InApp,
                            dedupKey: $"SubmissionDisputeCreated:{dispute.Id}:{recipientId}",
                            cancellationToken: cancellationToken);
                    }
                }
            }
            catch
            {
                // Không làm gián đoạn luồng gửi khiếu nại nếu việc phát thông báo gặp sự cố
            }
        }

        return (true, "Gửi khiếu nại bài dự thi thành công.", dispute.Id, Array.Empty<string>());
    }
}
