using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Revenue.Common;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Revenue.Commands.RebuildRevenueSnapshot;

/// <summary>
/// UC51 — POST /api/v1/creator/revenue/snapshots/rebuild
/// Admin hoặc job nền tính lại bản chốt doanh thu khi phát hiện lệch số.
///
/// Quyền: Administrator, HOẶC chính Creator đó chốt lại số của mình.
/// Cho Creator tự chốt lại là hợp lý vì thao tác này chỉ ĐỌC lại sổ cái của chính họ
/// và không tạo ra tiền — nó không thể bị lợi dụng để tăng doanh thu.
/// </summary>
public record RebuildRevenueSnapshotCommand(
    Guid UserId,
    bool IsAdministrator,
    Guid? CreatorId,
    string Scope,
    DateOnly? SnapshotDate
) : IRequest<(bool Success, RevenueSnapshotDto? Data, string[] Errors)>;

public class RebuildRevenueSnapshotCommandValidator : AbstractValidator<RebuildRevenueSnapshotCommand>
{
    public RebuildRevenueSnapshotCommandValidator()
    {
        RuleFor(x => x.Scope)
            .NotEmpty().WithMessage("Thiếu chu kỳ chốt doanh thu.")
            .Must(scope => Enum.TryParse<RevenueSnapshotScope>(scope, ignoreCase: true, out _))
            .WithMessage("Chu kỳ không hợp lệ (Daily / Weekly / Monthly).");

        RuleFor(x => x.SnapshotDate)
            .Must(date => !date.HasValue || date.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Ngày chốt doanh thu không được ở tương lai.");
    }
}

public class RebuildRevenueSnapshotCommandHandler
    : IRequestHandler<RebuildRevenueSnapshotCommand, (bool, RevenueSnapshotDto?, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly IRevenueQueryService _revenueService;

    public RebuildRevenueSnapshotCommandHandler(
        IApplicationDbContext db,
        IRevenueQueryService revenueService)
    {
        _db = db;
        _revenueService = revenueService;
    }

    public async Task<(bool, RevenueSnapshotDto?, string[])> Handle(
        RebuildRevenueSnapshotCommand request,
        CancellationToken cancellationToken)
    {
        var validation = new RebuildRevenueSnapshotCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        // Xác định Creator mục tiêu: Admin được chỉ định, người thường chỉ chốt số của mình.
        var targetCreatorId = request.IsAdministrator
            ? request.CreatorId ?? request.UserId
            : request.UserId;

        if (targetCreatorId == Guid.Empty)
        {
            return (false, null, ["Thiếu Creator cần chốt doanh thu."]);
        }

        if (!request.IsAdministrator && request.CreatorId.HasValue && request.CreatorId.Value != request.UserId)
        {
            return (false, null, ["Chỉ quản trị viên mới chốt được doanh thu của người khác."]);
        }

        // Admin chốt cho Creator khác thì phải bảo đảm user đó tồn tại — nếu không sẽ
        // tạo snapshot mồ côi trỏ tới Guid không có thật.
        if (request.IsAdministrator && targetCreatorId != request.UserId)
        {
            var exists = await _db.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == targetCreatorId && !u.IsDeleted, cancellationToken);

            if (!exists)
            {
                return (false, null, ["Không tìm thấy người dùng cần chốt doanh thu."]);
            }
        }

        Enum.TryParse<RevenueSnapshotScope>(request.Scope, ignoreCase: true, out var scope);

        var snapshotDate = request.SnapshotDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var snapshot = await _revenueService.RebuildSnapshotAsync(
            targetCreatorId, scope, snapshotDate, cancellationToken);

        var dto = new RevenueSnapshotDto(
            snapshot.Id,
            snapshot.Scope.ToString(),
            snapshot.SnapshotDate,
            snapshot.GrossAmount,
            snapshot.FeeAmount,
            snapshot.NetAmount,
            snapshot.CompletedOrderCount,
            snapshot.RebuiltAt,
            snapshot.RebuildCount);

        return (true, dto, []);
    }
}
