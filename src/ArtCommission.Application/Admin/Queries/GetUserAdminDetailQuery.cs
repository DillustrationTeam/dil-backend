using ArtCommission.Application.Admin.DTOs;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Admin.Queries;

/// <summary>
/// Query lấy chi tiết hồ sơ người dùng, phân quyền, số dư ví và lịch sử xử phạt chế tài (SCR-23 / UC31).
/// </summary>
public record GetUserAdminDetailQuery(Guid UserId) : IRequest<UserAdminDetailDto?>;

public class GetUserAdminDetailQueryHandler : IRequestHandler<GetUserAdminDetailQuery, UserAdminDetailDto?>
{
    private readonly IApplicationDbContext _db;

    public GetUserAdminDetailQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<UserAdminDetailDto?> Handle(GetUserAdminDetailQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted, cancellationToken);

        if (user == null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var isLocked = user.LockoutEnd != null && user.LockoutEnd > now;

        // Lấy danh sách vai trò (Roles)
        var roles = await (
            from ur in _db.Set<IdentityUserRole<Guid>>()
            join r in _db.Set<IdentityRole<Guid>>() on ur.RoleId equals r.Id
            where ur.UserId == user.Id
            select r.Name
        ).Where(r => !string.IsNullOrEmpty(r)).ToListAsync(cancellationToken);

        // Lấy thông tin ví (Wallet)
        var wallet = await _db.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == user.Id && !w.IsDeleted, cancellationToken);

        UserAdminWalletDto? walletDto = null;
        if (wallet != null)
        {
            walletDto = new UserAdminWalletDto
            {
                Balance = wallet.Balance,
                LockedBalance = wallet.LockedBalance,
                Currency = wallet.Currency
            };
        }

        // Lấy lịch sử chế tài xử phạt (UserSanctions)
        var sanctions = await _db.UserSanctions
            .AsNoTracking()
            .Include(s => s.ActionByAdmin)
            .Where(s => s.UserId == user.Id && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new UserSanctionHistoryDto
            {
                Id = s.Id,
                ActionType = s.ActionType,
                Reason = s.Reason,
                DurationDays = s.DurationDays,
                ExpiresAt = s.ExpiresAt,
                ActionByAdminId = s.ActionByAdminId,
                ActionByAdminName = s.ActionByAdmin != null ? (s.ActionByAdmin.FullName ?? s.ActionByAdmin.UserName ?? "Admin") : "Admin",
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new UserAdminDetailDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            UserName = user.UserName ?? string.Empty,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            Roles = roles!,
            IsVerified = user.IsVerified,
            IsLockedOut = isLocked,
            LockoutEnd = user.LockoutEnd,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            Wallet = walletDto,
            SanctionsHistory = sanctions
        };
    }
}
