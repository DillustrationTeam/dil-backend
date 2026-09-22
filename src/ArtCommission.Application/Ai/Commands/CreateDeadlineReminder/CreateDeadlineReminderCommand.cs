using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Ai;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Commands.CreateDeadlineReminder;

/// <summary>
/// UC46 — POST /api/v1/commissions/{commissionId}/deadline-reminders
/// Người dùng tạo nhắc nhở thủ công cho một mốc sắp tới hạn (in-app + email).
///
/// Khác <c>predict</c>: đây là nhắc do NGƯỜI tạo, nên không ghi đè điểm rủi ro
/// và được đánh dấu <c>IsManual = true</c> để phân biệt khi thống kê.
/// </summary>
public record CreateDeadlineReminderCommand(
    Guid UserId,
    Guid CommissionId,
    Guid? MilestoneId,
    DateTimeOffset RemindAt,
    string? Channel
) : IRequest<(bool Success, DeadlineReminderDto? Data, string[] Errors)>;

public class CreateDeadlineReminderCommandValidator : AbstractValidator<CreateDeadlineReminderCommand>
{
    public CreateDeadlineReminderCommandValidator()
    {
        RuleFor(x => x.CommissionId).NotEmpty().WithMessage("Thiếu mã đơn đặt vẽ.");

        RuleFor(x => x.RemindAt)
            .Must(remindAt => remindAt > DateTimeOffset.UtcNow)
            .WithMessage("Thời điểm nhắc phải ở tương lai.");

        RuleFor(x => x.Channel)
            .Must(channel => string.IsNullOrWhiteSpace(channel)
                             || Enum.TryParse<ReminderChannel>(channel, ignoreCase: true, out _))
            .WithMessage("Kênh nhắc không hợp lệ (InApp / Email / Push).");
    }
}

public class CreateDeadlineReminderCommandHandler
    : IRequestHandler<CreateDeadlineReminderCommand, (bool, DeadlineReminderDto?, string[])>
{
    /// <summary>Đặt nhắc xa quá mức thường là nhập nhầm — chặn ở 1 năm.</summary>
    private static readonly TimeSpan MaxLeadTime = TimeSpan.FromDays(365);

    private readonly IApplicationDbContext _db;

    public CreateDeadlineReminderCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, DeadlineReminderDto?, string[])> Handle(
        CreateDeadlineReminderCommand request,
        CancellationToken cancellationToken)
    {
        var validation = new CreateDeadlineReminderCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var commission = await _db.Commissions
            .AsNoTracking()
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
            return (false, null, ["Bạn không có quyền tạo nhắc nhở cho đơn này."]);
        }

        var now = DateTimeOffset.UtcNow;

        if (request.RemindAt - now > MaxLeadTime)
        {
            return (false, null, ["Chỉ đặt nhắc nhở trong vòng 1 năm tới."]);
        }

        // Mốc công việc (nếu có) phải thuộc đúng đơn này — nếu không, nhắc nhở sẽ
        // trỏ sang mốc của đơn khác.
        if (request.MilestoneId.HasValue)
        {
            var milestoneExists = await _db.Milestones
                .AsNoTracking()
                .AnyAsync(
                    m => m.Id == request.MilestoneId.Value
                         && m.CommissionId == request.CommissionId
                         && !m.IsDeleted,
                    cancellationToken);

            if (!milestoneExists)
            {
                return (false, null, ["Mốc công việc không thuộc đơn đặt vẽ này."]);
            }
        }

        var channel = ReminderChannel.InApp;
        if (!string.IsNullOrWhiteSpace(request.Channel)
            && Enum.TryParse<ReminderChannel>(request.Channel, ignoreCase: true, out var parsed))
        {
            channel = parsed;
        }

        var reminder = new DeadlineReminder
        {
            CommissionId = request.CommissionId,
            MilestoneId = request.MilestoneId,
            RemindAt = request.RemindAt,
            Channel = channel,
            // Điểm rủi ro để 0 vì đây là nhắc thủ công, không phải kết quả dự đoán.
            RiskScore = 0m,
            PredictedAt = null,
            ModelVersion = null,
            IsManual = true,
            SentAt = null
        };

        _db.DeadlineReminders.Add(reminder);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = new DeadlineReminderDto(
            reminder.Id,
            reminder.CommissionId,
            reminder.MilestoneId,
            reminder.RemindAt,
            reminder.Channel.ToString(),
            reminder.SentAt,
            reminder.RiskScore,
            reminder.IsManual);

        return (true, dto, []);
    }
}
