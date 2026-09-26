using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Entities.Notifications;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Admin.Commands;

/// <summary>
/// Command áp dụng chế tài xử phạt (Cảnh cáo, Đình chỉ, Ban, Mở khóa) cho tài khoản người dùng (SCR-23 / UC31).
/// </summary>
public record ApplyUserSanctionCommand(
    Guid UserId,
    string ActionType, // Warn, Suspend, Ban, Unban
    string Reason,
    int? DurationDays,
    Guid AdminId
) : IRequest<(bool Success, string Message, string[] Errors)>;

public class ApplyUserSanctionCommandValidator : AbstractValidator<ApplyUserSanctionCommand>
{
    public ApplyUserSanctionCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Mã người dùng (UserId) không được để trống.");

        RuleFor(x => x.AdminId)
            .NotEmpty().WithMessage("Mã quản trị viên (AdminId) không được để trống.");

        RuleFor(x => x.ActionType)
            .NotEmpty().WithMessage("Loại chế tài không được để trống.")
            .Must(a => SanctionActionTypes.All.Contains(a.Trim()))
            .WithMessage("Loại chế tài không hợp lệ. Cho phép: Warn, Suspend, Ban, Unban.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do xử phạt không được để trống.")
            .MaximumLength(1000).WithMessage("Lý do xử phạt không được vượt quá 1000 ký tự.");

        When(x => x.ActionType.Trim().Equals(SanctionActionTypes.Suspend, StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.DurationDays)
                .NotNull().WithMessage("Vui lòng nhập số ngày đình chỉ tạm thời.")
                .InclusiveBetween(1, 365).WithMessage("Số ngày đình chỉ phải từ 1 đến 365 ngày.");
        });
    }
}

public class ApplyUserSanctionCommandHandler
    : IRequestHandler<ApplyUserSanctionCommand, (bool Success, string Message, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public ApplyUserSanctionCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, string Message, string[] Errors)> Handle(
        ApplyUserSanctionCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new ApplyUserSanctionCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, string.Empty, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        // An toàn 1: Không cho phép tự phạt tài khoản của chính mình
        if (request.UserId == request.AdminId)
        {
            return (false, string.Empty, new[] { "Không thể tự áp dụng chế tài xử phạt lên tài khoản của chính mình." });
        }

        // Chạy trong 1 EF Core DB Transaction
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted, cancellationToken);

            if (user == null)
            {
                return (false, string.Empty, new[] { "Không tìm thấy người dùng tương ứng." });
            }

            // An toàn 2: Không cho phép áp dụng chế tài lên tài khoản Administrator khác
            var adminRoleId = await _db.Set<IdentityRole<Guid>>()
                .Where(r => r.Name == UserRoleNames.Administrator)
                .Select(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (adminRoleId != Guid.Empty)
            {
                var isTargetAdmin = await _db.Set<IdentityUserRole<Guid>>()
                    .AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == adminRoleId, cancellationToken);

                if (isTargetAdmin)
                {
                    return (false, string.Empty, new[] { "Không thể áp dụng chế tài xử phạt lên tài khoản Administrator khác." });
                }
            }

            var actionType = request.ActionType.Trim();
            var reason = request.Reason.Trim();
            string resultSummary;

            switch (actionType)
            {
                case SanctionActionTypes.Warn:
                    var warnSanction = new UserSanction
                    {
                        UserId = user.Id,
                        ActionType = SanctionActionTypes.Warn,
                        Reason = reason,
                        ActionByAdminId = request.AdminId,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    _db.UserSanctions.Add(warnSanction);

                    _db.Notifications.Add(new Notification
                    {
                        UserId = user.Id,
                        NotificationType = NotificationType.UserSanctionAlert,
                        NotificationTitle = "Cảnh báo vi phạm tài khoản",
                        Body = $"Tài khoản của bạn nhận được cảnh cáo từ Quản trị viên: {reason}",
                        Channel = NotificationChannel.InApp,
                        RefType = "UserSanction",
                        RefId = warnSanction.Id,
                        CreatedAt = DateTimeOffset.UtcNow
                    });

                    resultSummary = "Đã gửi cảnh cáo vi phạm chính sách tới người dùng.";
                    break;

                case SanctionActionTypes.Suspend:
                    var days = request.DurationDays ?? 7;
                    var lockoutEnd = DateTimeOffset.UtcNow.AddDays(days);

                    user.LockoutEnabled = true;
                    user.LockoutEnd = lockoutEnd;
                    user.UpdatedAt = DateTimeOffset.UtcNow;

                    // Thu hồi tất cả refresh token đang hiệu lực để ép đăng xuất tức thì
                    var activeTokensSuspend = await _db.RefreshTokens
                        .Where(t => t.UserId == user.Id && t.RevokedAt == null)
                        .ToListAsync(cancellationToken);

                    foreach (var token in activeTokensSuspend)
                    {
                        token.RevokedAt = DateTimeOffset.UtcNow;
                    }

                    var suspendSanction = new UserSanction
                    {
                        UserId = user.Id,
                        ActionType = SanctionActionTypes.Suspend,
                        Reason = reason,
                        DurationDays = days,
                        ExpiresAt = lockoutEnd,
                        ActionByAdminId = request.AdminId,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    _db.UserSanctions.Add(suspendSanction);

                    _db.Notifications.Add(new Notification
                    {
                        UserId = user.Id,
                        NotificationType = NotificationType.UserSanctionAlert,
                        NotificationTitle = "Thông báo đình chỉ tài khoản",
                        Body = $"Tài khoản của bạn đã bị đình chỉ hoạt động trong {days} ngày (đến {lockoutEnd:dd/MM/yyyy HH:mm}). Lý do: {reason}",
                        Channel = NotificationChannel.InApp,
                        RefType = "UserSanction",
                        RefId = suspendSanction.Id,
                        CreatedAt = DateTimeOffset.UtcNow
                    });

                    resultSummary = $"Đã đình chỉ tài khoản người dùng trong {days} ngày (đến {lockoutEnd:dd/MM/yyyy HH:mm}). Toàn bộ phiên đăng nhập đã được thu hồi.";
                    break;

                case SanctionActionTypes.Ban:
                    user.LockoutEnabled = true;
                    user.LockoutEnd = DateTimeOffset.MaxValue;
                    user.UpdatedAt = DateTimeOffset.UtcNow;

                    // Thu hồi toàn bộ refresh token
                    var activeTokensBan = await _db.RefreshTokens
                        .Where(t => t.UserId == user.Id && t.RevokedAt == null)
                        .ToListAsync(cancellationToken);

                    foreach (var token in activeTokensBan)
                    {
                        token.RevokedAt = DateTimeOffset.UtcNow;
                    }

                    var banSanction = new UserSanction
                    {
                        UserId = user.Id,
                        ActionType = SanctionActionTypes.Ban,
                        Reason = reason,
                        ExpiresAt = null,
                        ActionByAdminId = request.AdminId,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    _db.UserSanctions.Add(banSanction);

                    _db.Notifications.Add(new Notification
                    {
                        UserId = user.Id,
                        NotificationType = NotificationType.UserSanctionAlert,
                        NotificationTitle = "Thông báo khóa tài khoản vĩnh viễn",
                        Body = $"Tài khoản của bạn đã bị khóa vĩnh viễn do vi phạm nghiêm trọng: {reason}",
                        Channel = NotificationChannel.InApp,
                        RefType = "UserSanction",
                        RefId = banSanction.Id,
                        CreatedAt = DateTimeOffset.UtcNow
                    });

                    resultSummary = "Đã khóa vĩnh viễn tài khoản người dùng và thu hồi toàn bộ phiên đăng nhập.";
                    break;

                case SanctionActionTypes.Unban:
                    user.LockoutEnd = null;
                    user.UpdatedAt = DateTimeOffset.UtcNow;

                    var unbanSanction = new UserSanction
                    {
                        UserId = user.Id,
                        ActionType = SanctionActionTypes.Unban,
                        Reason = reason,
                        ActionByAdminId = request.AdminId,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    _db.UserSanctions.Add(unbanSanction);

                    _db.Notifications.Add(new Notification
                    {
                        UserId = user.Id,
                        NotificationType = NotificationType.UserSanctionAlert,
                        NotificationTitle = "Thông báo mở khóa tài khoản",
                        Body = "Tài khoản của bạn đã được Quản trị viên mở khóa. Bạn có thể tiếp tục sử dụng hệ thống.",
                        Channel = NotificationChannel.InApp,
                        RefType = "UserSanction",
                        RefId = unbanSanction.Id,
                        CreatedAt = DateTimeOffset.UtcNow
                    });

                    resultSummary = "Đã mở khóa tài khoản người dùng thành công.";
                    break;

                default:
                    return (false, string.Empty, new[] { "Loại chế tài không hợp lệ." });
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return (true, resultSummary, Array.Empty<string>());
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (false, string.Empty, new[] { $"Lỗi khi thực thi chế tài xử phạt: {ex.Message}" });
        }
    }
}
