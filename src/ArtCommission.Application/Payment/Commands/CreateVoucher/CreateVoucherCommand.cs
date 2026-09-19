using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Payment.Commands.CreateVoucher;

/// <summary>
/// UC50 — POST /api/v1/vouchers
/// Admin hoặc Creator tạo mã giảm giá.
/// Admin tạo ⇒ voucher toàn sàn (<c>CreatedByUserId = null</c>);
/// Creator tạo ⇒ gắn <c>CreatedByUserId</c> để lọc "voucher của tôi".
/// </summary>
public record CreateVoucherCommand(
    Guid UserId,
    bool IsAdmin,
    string VoucherCode,
    string? Name,
    string DiscountType,
    decimal DiscountValue,
    decimal MinOrderAmount,
    decimal? MaxDiscountAmount,
    string[]? Scope,
    DateOnly StartDate,
    DateOnly EndDate,
    int? UsageLimit
) : IRequest<(bool Success, VoucherDto? Data, string[] Errors)>;

public class CreateVoucherCommandHandler
    : IRequestHandler<CreateVoucherCommand, (bool, VoucherDto?, string[])>
{
    private readonly IApplicationDbContext _db;

    public CreateVoucherCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, VoucherDto?, string[])> Handle(
        CreateVoucherCommand request,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        // --- Mã voucher ---
        var code = VoucherCheckService.Normalize(request.VoucherCode ?? string.Empty);
        if (string.IsNullOrWhiteSpace(code))
        {
            errors.Add("Vui lòng nhập mã giảm giá.");
        }
        else if (code.Length > 50)
        {
            errors.Add("Mã giảm giá tối đa 50 ký tự.");
        }
        else if (!code.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'))
        {
            errors.Add("Mã giảm giá chỉ được chứa chữ, số, dấu gạch ngang và gạch dưới.");
        }

        // --- Loại giảm giá ---
        if (!Enum.TryParse<DiscountType>(request.DiscountType, ignoreCase: true, out var discountType))
        {
            errors.Add($"Loại giảm giá không hợp lệ. Hợp lệ: {string.Join(", ", DiscountTypeNames.All)}.");
        }

        // --- Giá trị giảm ---
        if (request.DiscountValue <= 0)
        {
            errors.Add("Giá trị giảm phải lớn hơn 0.");
        }
        else if (discountType == DiscountType.Percent && request.DiscountValue > 100)
        {
            errors.Add("Giảm theo phần trăm chỉ nhận giá trị từ 0 đến 100.");
        }

        if (request.MinOrderAmount < 0)
        {
            errors.Add("Giá trị đơn tối thiểu không được âm.");
        }

        if (request.MaxDiscountAmount.HasValue && request.MaxDiscountAmount.Value <= 0)
        {
            errors.Add("Trần giảm giá phải lớn hơn 0 (hoặc bỏ trống).");
        }

        // --- Hạn dùng ---
        if (request.EndDate < request.StartDate)
        {
            errors.Add("Ngày kết thúc phải bằng hoặc sau ngày bắt đầu.");
        }

        if (request.UsageLimit.HasValue && request.UsageLimit.Value <= 0)
        {
            errors.Add("Số lượt sử dụng phải lớn hơn 0 (hoặc bỏ trống để không giới hạn).");
        }

        // --- Phạm vi áp dụng ---
        var scope = VoucherScope.All;
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
            return (false, null, [.. errors]);
        }

        var duplicated = await _db.Vouchers
            .AnyAsync(v => v.VoucherCode == code, cancellationToken);

        if (duplicated)
        {
            return (false, null, [$"Mã giảm giá '{code}' đã tồn tại."]);
        }

        var entity = new Voucher
        {
            VoucherCode = code,
            Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim(),
            DiscountType = discountType,
            DiscountValue = request.DiscountValue,
            MinOrderAmount = request.MinOrderAmount,
            // Trần giảm giá chỉ có nghĩa với voucher phần trăm.
            MaxDiscountAmount = discountType == DiscountType.Percent ? request.MaxDiscountAmount : null,
            Scope = scope,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            UsageLimit = request.UsageLimit,
            UsedCount = 0,
            IsActive = true,
            CreatedByUserId = request.IsAdmin ? null : request.UserId
        };

        _db.Vouchers.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var today = VoucherCheckService.TodayInVietnam();
        return (true, VoucherResponseMapper.ToDto(entity, today, request.UserId), []);
    }
}
