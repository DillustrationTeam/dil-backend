using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Domain.Entities.Ai;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// MediatR cũng có INotificationPublisher — alias để không lấy nhầm của thư viện.
using INotificationPublisher = ArtCommission.Application.Notifications.Common.INotificationPublisher;

namespace ArtCommission.Application.Ai.Commands.PredictDeadlineRisk;

/// <summary>
/// UC46 — POST /api/v1/commissions/{commissionId}/deadline-risks/predict
/// Tính lại điểm rủi ro từ hạn, số mốc trễ và số lần sửa, rồi ghi một bản ghi nhắc mới.
///
/// Luôn GHI THÊM một bản ghi thay vì sửa bản cũ: chuỗi bản ghi là lịch sử dự đoán,
/// cần thiết để đánh giá công thức có cải thiện theo thời gian hay không.
/// </summary>
public record PredictDeadlineRiskCommand(
    Guid UserId,
    Guid CommissionId,
    // ReSharper disable once IdentifierTypo
    string? ModelVersion = null
) : IRequest<(bool Success, DeadlineRiskDto? Data, string[] Errors)>;

public class PredictDeadlineRiskCommandHandler
    : IRequestHandler<PredictDeadlineRiskCommand, (bool, DeadlineRiskDto?, string[])>
{
    /// <summary>Rủi ro từ mức này trở lên thì phát cảnh báo cho người tham gia.</summary>
    private const decimal NotifyThreshold = 60m;

    private readonly IApplicationDbContext _db;
    private readonly IDeadlineRiskPredictor _predictor;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<PredictDeadlineRiskCommandHandler> _logger;

    public PredictDeadlineRiskCommandHandler(
        IApplicationDbContext db,
        IDeadlineRiskPredictor predictor,
        INotificationPublisher notifications,
        ILogger<PredictDeadlineRiskCommandHandler> logger)
    {
        _db = db;
        _predictor = predictor;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<(bool, DeadlineRiskDto?, string[])> Handle(
        PredictDeadlineRiskCommand request,
        CancellationToken cancellationToken)
    {
        if (request.CommissionId == Guid.Empty)
        {
            return (false, null, ["Thiếu mã đơn đặt vẽ."]);
        }

        var commission = await _db.Commissions
            .FirstOrDefaultAsync(
                c => c.Id == request.CommissionId && !c.IsDeleted,
                cancellationToken);

        if (commission is null)
        {
            return (false, null, ["Không tìm thấy đơn đặt vẽ."]);
        }

        var creatorProfileId = await _db.CreatorProfiles
            .AsNoTracking()
            .Where(p => p.UserId == request.UserId && !p.IsDeleted)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (commission.ClientId != request.UserId && commission.CreatorId != request.UserId && (creatorProfileId == Guid.Empty || commission.CreatorId != creatorProfileId))
        {
            return (false, null, ["Bạn không có quyền cập nhật tiến độ của đơn này."]);
        }

        var assessment = await _predictor.AssessAsync(request.CommissionId, cancellationToken);

        if (assessment is null)
        {
            return (false, null, ["Không tính được rủi ro cho đơn này."]);
        }

        var now = DateTimeOffset.UtcNow;

        // ModelVersion: ưu tiên giá trị client gửi để so sánh công thức, nhưng luôn
        // ghi rõ phiên bản thực tế đã dùng khi client không truyền.
        var modelVersion = string.IsNullOrWhiteSpace(request.ModelVersion)
            ? assessment.ModelVersion
            : request.ModelVersion.Trim();

        var reminder = new DeadlineReminder
        {
            CommissionId = request.CommissionId,
            MilestoneId = null,
            // Đơn không có hạn thì NextRemindAt = null; lùi lại 1 ngày thay vì dùng
            // "now" — mốc nhắc đã ở quá khứ sẽ khiến job gửi ngay lập tức mỗi lần chạy.
            RemindAt = assessment.NextRemindAt ?? now.AddDays(1),
            Channel = ReminderChannel.InApp,
            RiskScore = assessment.RiskScore,
            PredictedAt = now,
            ModelVersion = modelVersion,
            IsManual = false,
            SentAt = null
        };

        _db.DeadlineReminders.Add(reminder);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Dự đoán rủi ro trễ hạn cho đơn {CommissionId}: điểm {Score} ({Level}), model {Model}.",
            request.CommissionId, assessment.RiskScore, assessment.Level, modelVersion);

        // Cảnh báo khi rủi ro cao — phát sau commit, lỗi thông báo không làm hỏng nghiệp vụ.
        if (assessment.RiskScore >= NotifyThreshold)
        {
            await NotifyHighRiskAsync(commission, assessment, cancellationToken);
        }

        var reminders = await _db.DeadlineReminders
            .AsNoTracking()
            .Where(r => r.CommissionId == request.CommissionId && !r.IsDeleted)
            .OrderByDescending(r => r.RemindAt)
            .Select(r => new DeadlineReminderDto(
                r.Id,
                r.CommissionId,
                r.MilestoneId,
                r.RemindAt,
                r.Channel.ToString(),
                r.SentAt,
                r.RiskScore,
                r.IsManual))
            .ToListAsync(cancellationToken);

        var dto = new DeadlineRiskDto(
            CommissionId: request.CommissionId,
            RiskScore: assessment.RiskScore,
            RiskLevel: AiAndRevenueEnumNames.ToContractName(assessment.Level),
            Deadline: assessment.Deadline,
            CurrentStage: assessment.CurrentStage,
            PredictedAt: now,
            NextRemindAt: assessment.NextRemindAt,
            ModelVersion: modelVersion,
            Reminders: reminders);

        return (true, dto, []);
    }

    /// <summary>
    /// Phát cảnh báo cho cả client và creator. Khoá chống trùng gồm cả ngày nên
    /// mỗi ngày tối đa một cảnh báo cho mỗi người, không dội tin khi job chạy nhiều lần.
    /// </summary>
    private async Task NotifyHighRiskAsync(
        Domain.Entities.Commission.Commission commission,
        DeadlineRiskAssessment assessment,
        CancellationToken cancellationToken)
    {
        var dayKey = DateTimeOffset.UtcNow.ToString("yyyyMMdd");
        var body =
            $"Đơn \"{commission.Title}\" đang ở mức rủi ro trễ hạn {AiAndRevenueEnumNames.ToContractName(assessment.Level)} " +
            $"(điểm {assessment.RiskScore}/100).";

        foreach (var userId in new[] { commission.ClientId, commission.CreatorId }.Distinct())
        {
            try
            {
                await _notifications.PublishAsync(
                    userId,
                    NotificationType.DeadlineRiskWarning,
                    "Cảnh báo rủi ro trễ hạn",
                    body,
                    nameof(Domain.Entities.Commission.Commission),
                    commission.Id,
                    NotificationChannel.InApp,
                    $"DeadlineRisk:{commission.Id}:{userId}:{dayKey}",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Không phát được cảnh báo rủi ro trễ hạn cho userId={UserId}, commissionId={CommissionId}.",
                    userId, commission.Id);
            }
        }
    }
}
