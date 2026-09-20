using System.Globalization;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.PlatformConfig.DTOs;
using ArtCommission.Domain.Entities.Payment;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Payment.PlatformConfig.Queries;

/// <summary>
/// Query lấy thông tin cấu hình phí sàn và các chính sách vận hành (SCR-18 / UC33).
/// </summary>
public record GetPlatformFeePolicyQuery : IRequest<PlatformFeePolicyDto>;

public class GetPlatformFeePolicyQueryHandler : IRequestHandler<GetPlatformFeePolicyQuery, PlatformFeePolicyDto>
{
    private readonly IApplicationDbContext _db;

    public GetPlatformFeePolicyQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PlatformFeePolicyDto> Handle(GetPlatformFeePolicyQuery request, CancellationToken cancellationToken)
    {
        var targetKeys = new[]
        {
            PlatformConfigKeys.PlatformFeePercent,
            PlatformConfigKeys.PlatformFeeRateSqlKey,
            PlatformConfigKeys.MilestoneAutoApprovalDays,
            PlatformConfigKeys.EscrowHoldDaysSqlKey,
            PlatformConfigKeys.DefaultFreeRevisionLimit,
            PlatformConfigKeys.MaxRevisionCountSqlKey,
            PlatformConfigKeys.PresignedUrlExpirationMinutes
        };

        var configs = await _db.PlatformConfigs
            .AsNoTracking()
            .Where(c => targetKeys.Contains(c.Key))
            .ToListAsync(cancellationToken);

        var configDict = configs.ToDictionary(c => c.Key, c => c.Value);

        decimal platformFeePercent = 10.0m;
        if (configDict.TryGetValue(PlatformConfigKeys.PlatformFeePercent, out var feeStr) &&
            decimal.TryParse(feeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedFee))
        {
            platformFeePercent = parsedFee;
        }
        else if (configDict.TryGetValue(PlatformConfigKeys.PlatformFeeRateSqlKey, out var rateStr) &&
            decimal.TryParse(rateStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedRate))
        {
            platformFeePercent = parsedRate <= 1.0m ? parsedRate * 100m : parsedRate;
        }

        int milestoneAutoApprovalDays = 7;
        if ((configDict.TryGetValue(PlatformConfigKeys.MilestoneAutoApprovalDays, out var daysStr) ||
             configDict.TryGetValue(PlatformConfigKeys.EscrowHoldDaysSqlKey, out daysStr)) &&
            int.TryParse(daysStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDays))
        {
            milestoneAutoApprovalDays = parsedDays;
        }

        int defaultFreeRevisionLimit = 2;
        if ((configDict.TryGetValue(PlatformConfigKeys.DefaultFreeRevisionLimit, out var revStr) ||
             configDict.TryGetValue(PlatformConfigKeys.MaxRevisionCountSqlKey, out revStr)) &&
            int.TryParse(revStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedRev))
        {
            defaultFreeRevisionLimit = parsedRev;
        }

        int presignedUrlExpirationMinutes = 15;
        if (configDict.TryGetValue(PlatformConfigKeys.PresignedUrlExpirationMinutes, out var expStr) &&
            int.TryParse(expStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedExp))
        {
            presignedUrlExpirationMinutes = parsedExp;
        }

        DateTimeOffset? latestUpdatedAt = configs.Count > 0
            ? configs.Max(c => c.UpdatedAt ?? c.CreatedAt)
            : null;

        return new PlatformFeePolicyDto
        {
            PlatformFeePercent = platformFeePercent,
            MilestoneAutoApprovalDays = milestoneAutoApprovalDays,
            DefaultFreeRevisionLimit = defaultFreeRevisionLimit,
            PresignedUrlExpirationMinutes = presignedUrlExpirationMinutes,
            UpdatedAt = latestUpdatedAt
        };
    }
}
