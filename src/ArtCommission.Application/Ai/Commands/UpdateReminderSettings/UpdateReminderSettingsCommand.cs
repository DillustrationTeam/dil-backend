using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Ai;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Commands.UpdateReminderSettings;

/// <summary>
/// UC46 — PUT /api/v1/users/me/reminder-settings
/// Bật/tắt kênh nhận nhắc nhở deadline và số giờ nhắc trước hạn.
///
/// Upsert: user chưa từng cấu hình thì tạo mới với giá trị gửi lên; đã có thì cập nhật.
/// Không trả 404 vì "chưa cấu hình" là trạng thái hợp lệ, không phải lỗi.
/// </summary>
public record UpdateReminderSettingsCommand(
    Guid UserId,
    bool EmailEnabled,
    bool PushEnabled,
    int LeadHours
) : IRequest<(bool Success, ReminderSettingDto? Data, string[] Errors)>;

public class UpdateReminderSettingsCommandValidator : AbstractValidator<UpdateReminderSettingsCommand>
{
    /// <summary>Nhắc trước ít nhất 1 giờ — nhắc sát quá thì không kịp xử lý.</summary>
    public const int MinLeadHours = 1;

    /// <summary>Nhắc trước tối đa 30 ngày.</summary>
    public const int MaxLeadHours = 720;

    public UpdateReminderSettingsCommandValidator()
    {
        RuleFor(x => x.LeadHours)
            .InclusiveBetween(MinLeadHours, MaxLeadHours)
            .WithMessage($"Số giờ nhắc trước hạn phải trong khoảng {MinLeadHours}–{MaxLeadHours} giờ.");
    }
}

public class UpdateReminderSettingsCommandHandler
    : IRequestHandler<UpdateReminderSettingsCommand, (bool, ReminderSettingDto?, string[])>
{
    private readonly IApplicationDbContext _db;

    public UpdateReminderSettingsCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, ReminderSettingDto?, string[])> Handle(
        UpdateReminderSettingsCommand request,
        CancellationToken cancellationToken)
    {
        var validation = new UpdateReminderSettingsCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        if (request.UserId == Guid.Empty)
        {
            return (false, null, ["Bạn cần đăng nhập để đổi cấu hình nhắc nhở."]);
        }

        var settings = await _db.UserReminderSettings
            .FirstOrDefaultAsync(
                s => s.UserId == request.UserId && !s.IsDeleted,
                cancellationToken);

        if (settings is null)
        {
            settings = new UserReminderSetting
            {
                UserId = request.UserId,
                EmailEnabled = request.EmailEnabled,
                PushEnabled = request.PushEnabled,
                LeadHours = request.LeadHours
            };

            _db.UserReminderSettings.Add(settings);
        }
        else
        {
            settings.EmailEnabled = request.EmailEnabled;
            settings.PushEnabled = request.PushEnabled;
            settings.LeadHours = request.LeadHours;
            settings.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var dto = new ReminderSettingDto(
            settings.EmailEnabled,
            settings.PushEnabled,
            settings.LeadHours);

        return (true, dto, []);
    }
}
