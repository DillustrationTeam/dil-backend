using System.Globalization;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.PlatformConfig.DTOs;
using ArtCommission.Domain.Entities.Payment;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Payment.PlatformConfig.Commands;

/// <summary>
/// Command cập nhật cấu hình tỷ lệ phí sàn và các chính sách vận hành (SCR-18 / UC33).
/// </summary>
public record UpdatePlatformFeePolicyCommand(
    decimal PlatformFeePercent,
    int MilestoneAutoApprovalDays,
    int DefaultFreeRevisionLimit,
    int PresignedUrlExpirationMinutes,
    Guid? AdminId = null
) : IRequest<(bool Success, PlatformFeePolicyDto? Data, string[] Errors)>;

public class UpdatePlatformFeePolicyCommandValidator : AbstractValidator<UpdatePlatformFeePolicyCommand>
{
    public UpdatePlatformFeePolicyCommandValidator()
    {
        RuleFor(x => x.PlatformFeePercent)
            .InclusiveBetween(5.0m, 15.0m)
            .WithMessage("Tỷ lệ phí sàn (PlatformFeePercent) phải nằm trong khoảng từ 5.0% đến 15.0% (BR-36).");

        RuleFor(x => x.MilestoneAutoApprovalDays)
            .InclusiveBetween(1, 30)
            .WithMessage("Số ngày tự động duyệt cột mốc (MilestoneAutoApprovalDays) phải từ 1 đến 30 ngày.");

        RuleFor(x => x.DefaultFreeRevisionLimit)
            .InclusiveBetween(0, 10)
            .WithMessage("Số lượt sửa đổi miễn phí mặc định (DefaultFreeRevisionLimit) phải từ 0 đến 10 lần.");

        RuleFor(x => x.PresignedUrlExpirationMinutes)
            .InclusiveBetween(5, 120)
            .WithMessage("Thời gian hết hạn Presigned URL (PresignedUrlExpirationMinutes) phải từ 5 đến 120 phút.");
    }
}

public class UpdatePlatformFeePolicyCommandHandler 
    : IRequestHandler<UpdatePlatformFeePolicyCommand, (bool Success, PlatformFeePolicyDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public UpdatePlatformFeePolicyCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, PlatformFeePolicyDto? Data, string[] Errors)> Handle(
        UpdatePlatformFeePolicyCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new UpdatePlatformFeePolicyCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToArray();
            return (false, null, errors);
        }

        var keyValues = new Dictionary<string, (string Value, string Description)>
        {
            [PlatformConfigKeys.PlatformFeePercent] = (
                request.PlatformFeePercent.ToString("0.##", CultureInfo.InvariantCulture),
                "Phần trăm phí nền tảng trên mỗi giao dịch (5.0% - 15.0%)"
            ),
            [PlatformConfigKeys.PlatformFeeRateSqlKey] = (
                (request.PlatformFeePercent / 100m).ToString("0.####", CultureInfo.InvariantCulture),
                "Tỷ lệ phí sàn áp dụng cho mọi giao dịch commission (database.sql)"
            ),
            [PlatformConfigKeys.MilestoneAutoApprovalDays] = (
                request.MilestoneAutoApprovalDays.ToString(CultureInfo.InvariantCulture),
                "Số ngày tự động duyệt cột mốc nếu Client không phản hồi (BR-37)"
            ),
            [PlatformConfigKeys.EscrowHoldDaysSqlKey] = (
                request.MilestoneAutoApprovalDays.ToString(CultureInfo.InvariantCulture),
                "Số ngày tạm giữ tiền escrow sau khi hoàn thành commission (database.sql)"
            ),
            [PlatformConfigKeys.DefaultFreeRevisionLimit] = (
                request.DefaultFreeRevisionLimit.ToString(CultureInfo.InvariantCulture),
                "Số lượt yêu cầu sửa đổi miễn phí mặc định cho mỗi dịch vụ/cột mốc"
            ),
            [PlatformConfigKeys.MaxRevisionCountSqlKey] = (
                request.DefaultFreeRevisionLimit.ToString(CultureInfo.InvariantCulture),
                "Số lần yêu cầu sửa đổi tối đa mặc định cho 1 milestone (database.sql)"
            ),
            [PlatformConfigKeys.PresignedUrlExpirationMinutes] = (
                request.PresignedUrlExpirationMinutes.ToString(CultureInfo.InvariantCulture),
                "Thời gian hết hạn của AWS S3 / Cloudinary presigned URL (phút, BR-39)"
            )
        };

        var targetKeys = keyValues.Keys.ToList();
        var existingConfigs = await _db.PlatformConfigs
            .Where(c => targetKeys.Contains(c.Key))
            .ToListAsync(cancellationToken);

        var existingDict = existingConfigs.ToDictionary(c => c.Key, c => c);
        var now = DateTimeOffset.UtcNow;

        foreach (var (key, (val, desc)) in keyValues)
        {
            if (existingDict.TryGetValue(key, out var config))
            {
                config.Value = val;
                config.Description = desc;
                config.UpdatedAt = now;
                config.UpdatedByAdminId = request.AdminId ?? config.UpdatedByAdminId;
            }
            else
            {
                var newConfig = new Domain.Entities.Payment.PlatformConfig
                {
                    Id = Guid.NewGuid(),
                    Key = key,
                    Value = val,
                    Description = desc,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    UpdatedByAdminId = request.AdminId
                };
                _db.PlatformConfigs.Add(newConfig);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        var resultDto = new PlatformFeePolicyDto
        {
            PlatformFeePercent = request.PlatformFeePercent,
            MilestoneAutoApprovalDays = request.MilestoneAutoApprovalDays,
            DefaultFreeRevisionLimit = request.DefaultFreeRevisionLimit,
            PresignedUrlExpirationMinutes = request.PresignedUrlExpirationMinutes,
            UpdatedAt = now
        };

        return (true, resultDto, Array.Empty<string>());
    }
}
