using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Payment.Commands.UpdateVoucher;

/// <summary>
/// UC50 — PUT /api/v1/vouchers/{voucherId}
/// Sửa cấu hình voucher. KHÔNG cho sửa mã (mã đã phát ra cho người dùng).
/// Admin sửa được mọi voucher; Creator chỉ sửa voucher do mình tạo.
/// </summary>
public record UpdateVoucherCommand(
    Guid UserId,
    bool IsAdmin,
    Guid VoucherId,
    string? Name,
    string? DiscountType,
    decimal? DiscountValue,
    decimal? MinOrderAmount,
    decimal? MaxDiscountAmount,
    string[]? Scope,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int? UsageLimit,
    bool? IsActive
) : IRequest<(bool Success, bool NotFound, VoucherDto? Data, string[] Errors)>;

public class UpdateVoucherCommandHandler
    : IRequestHandler<UpdateVoucherCommand, (bool, bool, VoucherDto?, string[])>
{
    private readonly IApplicationDbContext _db;

    public UpdateVoucherCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, bool, VoucherDto?, string[])> Handle(
        UpdateVoucherCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await _db.Vouchers.FirstOrDefaultAsync(
            v => v.Id == request.VoucherId && !v.IsDeleted, cancellationToken);

        if (entity is null)
        {
            return (false, true, null, ["Không tìm thấy mã giảm giá."]);
        }

        // Admin sửa được tất cả; Creator chỉ sửa voucher do chính mình tạo.
        var isOwner = entity.CreatedByUserId.HasValue && entity.CreatedByUserId == request.UserId;
        if (!request.IsAdmin && !isOwner)
        {
            return (false, true, null, ["Bạn không có quyền sửa mã giảm giá này."]);
        }

        var errors = new List<string>();

        var discountType = entity.DiscountType;
        if (!string.IsNullOrWhiteSpace(request.DiscountType))
        {
            if (Enum.TryParse<DiscountType>(request.DiscountType, ignoreCase: true, out var parsed))
            {
                discountType = parsed;
            }
            else
            {
                errors.Add($"Loại giảm giá không hợp lệ. Hợp lệ: {string.Join(", ", DiscountTypeNames.All)}.");
            }
        }

        var discountValue = request.DiscountValue ?? entity.DiscountValue;
        if (discountValue <= 0)
        {
            errors.Add("Giá trị giảm phải lớn hơn 0.");
        }
        else if (discountType == DiscountType.Percent && discountValue > 100)
        {
            errors.Add("Giảm theo phần trăm chỉ nhận giá trị từ 0 đến 100.");
        }

        if (request.MinOrderAmount is < 0)
        {
            errors.Add("Giá trị đơn tối thiểu không được âm.");
        }

        var maxDiscount = request.MaxDiscountAmount ?? entity.MaxDiscountAmount;
        if (maxDiscount is <= 0)
        {
            errors.Add("Trần giảm giá phải lớn hơn 0 (hoặc bỏ trống).");
        }

        var startDate = request.StartDate ?? entity.StartDate;
        var endDate = request.EndDate ?? entity.EndDate;
        if (endDate < startDate)
        {
            errors.Add("Ngày kết thúc phải bằng hoặc sau ngày bắt đầu.");
        }

        // Không cho hạ số lượt xuống dưới số đã tiêu thụ — sẽ phá vỡ đối soát.
        if (request.UsageLimit.HasValue)
        {
            if (request.UsageLimit.Value <= 0)
            {
                errors.Add("Số lượt sử dụng phải lớn hơn 0 (hoặc bỏ trống để không giới hạn).");
            }
            else if (request.UsageLimit.Value < entity.UsedCount)
            {
                errors.Add($"Số lượt không được nhỏ hơn số lượt đã dùng ({entity.UsedCount}).");
            }
        }

        var scope = entity.Scope;
        if (request.Scope is { Length: > 0 })
        {
            if (!VoucherScopeParser.TryParse(request.Scope, out scope, out var scopeErrors))
            {
                errors.AddRange(scopeErrors);
            }
            else if (scope == VoucherScope.None)
            {
                errors.Add($"Phải chọn ít nhất một phạm vi áp dụng: {string.Join(", ", VoucherScopeNames.All)}.");
            }
        }

        if (errors.Count > 0)
        {
            return (false, false, null, [.. errors]);
        }

        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name.Trim();
        entity.DiscountType = discountType;
        entity.DiscountValue = discountValue;
        entity.MinOrderAmount = request.MinOrderAmount ?? entity.MinOrderAmount;
        entity.MaxDiscountAmount = discountType == DiscountType.Percent ? maxDiscount : null;
        entity.Scope = scope;
        entity.StartDate = startDate;
        entity.EndDate = endDate;
        entity.UsageLimit = request.UsageLimit ?? entity.UsageLimit;
        entity.IsActive = request.IsActive ?? entity.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var today = VoucherCheckService.TodayInVietnam();
        return (true, false, VoucherResponseMapper.ToDto(entity, today, request.UserId), []);
    }
}
