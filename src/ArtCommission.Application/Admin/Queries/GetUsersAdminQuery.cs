using ArtCommission.Application.Admin.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Admin.Queries;

/// <summary>
/// Query tra cứu danh sách người dùng trong hệ thống dành cho Administrator (SCR-23 / UC31).
/// Hỗ trợ tìm kiếm theo từ khóa, lọc theo vai trò, lọc trạng thái khóa và phân trang.
/// </summary>
public record GetUsersAdminQuery(
    string? Search = null,
    string? Role = null,
    string? Status = "All", // "Active", "LockedOut", "All"
    int Page = 1,
    int PageSize = 10
) : IRequest<(IReadOnlyList<UserAdminListItemDto> Items, int TotalCount, int ActiveCount, int LockedCount)>;

public class GetUsersAdminQueryHandler
    : IRequestHandler<GetUsersAdminQuery, (IReadOnlyList<UserAdminListItemDto> Items, int TotalCount, int ActiveCount, int LockedCount)>
{
    private readonly IApplicationDbContext _db;

    public GetUsersAdminQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<UserAdminListItemDto> Items, int TotalCount, int ActiveCount, int LockedCount)> Handle(
        GetUsersAdminQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var activeCount = await _db.Users
            .CountAsync(u => !u.IsDeleted && (u.LockoutEnd == null || u.LockoutEnd <= now), cancellationToken);

        var lockedCount = await _db.Users
            .CountAsync(u => !u.IsDeleted && u.LockoutEnd != null && u.LockoutEnd > now, cancellationToken);

        var query = _db.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(u =>
                (u.Email != null && u.Email.Contains(search)) ||
                (u.FullName != null && u.FullName.Contains(search)) ||
                (u.UserName != null && u.UserName.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && !request.Status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (request.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(u => u.LockoutEnd == null || u.LockoutEnd <= now);
            }
            else if (request.Status.Equals("LockedOut", StringComparison.OrdinalIgnoreCase) ||
                     request.Status.Equals("Banned", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(u => u.LockoutEnd != null && u.LockoutEnd > now);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Role) && !request.Role.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var roleId = await _db.Set<IdentityRole<Guid>>()
                .Where(r => r.Name == request.Role)
                .Select(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (roleId != Guid.Empty)
            {
                var userIdsInRole = _db.Set<IdentityUserRole<Guid>>()
                    .Where(ur => ur.RoleId == roleId)
                    .Select(ur => ur.UserId);

                query = query.Where(u => userIdsInRole.Contains(u.Id));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize is > 0 and <= 100 ? request.PageSize : 10;

        var rawUsers = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (rawUsers.Count == 0)
        {
            return (Array.Empty<UserAdminListItemDto>(), totalCount, activeCount, lockedCount);
        }

        var pageUserIds = rawUsers.Select(u => u.Id).ToList();

        // Lấy danh sách Roles của các user trong trang
        var userRolesList = await (
            from ur in _db.Set<IdentityUserRole<Guid>>()
            join r in _db.Set<IdentityRole<Guid>>() on ur.RoleId equals r.Id
            where pageUserIds.Contains(ur.UserId)
            select new { ur.UserId, RoleName = r.Name }
        ).ToListAsync(cancellationToken);

        var rolesByUser = userRolesList
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.RoleName ?? string.Empty).Where(x => !string.IsNullOrEmpty(x)).ToList());

        // Lấy thông tin Ví tiền (Wallet) nếu có
        var wallets = await _db.Wallets
            .AsNoTracking()
            .Where(w => pageUserIds.Contains(w.UserId) && !w.IsDeleted)
            .ToDictionaryAsync(w => w.UserId, w => w, cancellationToken);

        var items = rawUsers.Select(u =>
        {
            var isLocked = u.LockoutEnd != null && u.LockoutEnd > now;
            wallets.TryGetValue(u.Id, out var wallet);
            rolesByUser.TryGetValue(u.Id, out var userRoles);

            return new UserAdminListItemDto
            {
                Id = u.Id,
                Email = u.Email ?? string.Empty,
                UserName = u.UserName ?? string.Empty,
                FullName = u.FullName,
                Roles = userRoles ?? new List<string>(),
                IsVerified = u.IsVerified,
                IsLockedOut = isLocked,
                LockoutEnd = u.LockoutEnd,
                WalletBalance = wallet?.Balance ?? 0m,
                WalletLockedBalance = wallet?.LockedBalance ?? 0m,
                CreatedAt = u.CreatedAt
            };
        }).ToList();

        return (items, totalCount, activeCount, lockedCount);
    }
}
