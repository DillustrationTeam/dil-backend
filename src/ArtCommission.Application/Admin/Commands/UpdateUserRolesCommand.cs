using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Admin.Commands;

/// <summary>
/// Command cập nhật phân quyền Roles cho tài khoản người dùng (SCR-23 / UC31).
/// </summary>
public record UpdateUserRolesCommand(
    Guid UserId,
    List<string> Roles,
    Guid AdminId
) : IRequest<(bool Success, string Message, string[] Errors)>;

public class UpdateUserRolesCommandValidator : AbstractValidator<UpdateUserRolesCommand>
{
    public UpdateUserRolesCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Mã người dùng (UserId) không được để trống.");

        RuleFor(x => x.AdminId)
            .NotEmpty().WithMessage("Mã quản trị viên (AdminId) không được để trống.");

        RuleFor(x => x.Roles)
            .NotNull().WithMessage("Danh sách vai trò không được null.")
            .Must(r => r != null && r.Count > 0).WithMessage("Phải chọn ít nhất 1 vai trò cho người dùng.")
            .Must(roles => roles.All(r => UserRoleNames.All.Contains(r.Trim())))
            .WithMessage($"Các vai trò chỉ được thuộc: {string.Join(", ", UserRoleNames.All)}.");
    }
}

public class UpdateUserRolesCommandHandler
    : IRequestHandler<UpdateUserRolesCommand, (bool Success, string Message, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public UpdateUserRolesCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, string Message, string[] Errors)> Handle(
        UpdateUserRolesCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new UpdateUserRolesCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, string.Empty, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        // An toàn: Không cho phép Admin tự tước quyền Administrator của chính mình
        if (request.UserId == request.AdminId && !request.Roles.Contains(UserRoleNames.Administrator, StringComparer.OrdinalIgnoreCase))
        {
            return (false, string.Empty, new[] { "Không thể tự tước quyền Administrator của chính mình để đảm bảo quyền quản trị hệ thống." });
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted, cancellationToken);

        if (user == null)
        {
            return (false, string.Empty, new[] { "Không tìm thấy người dùng tương ứng." });
        }

        var normalizedRequestedRoles = request.Roles
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Lấy danh sách tất cả các Roles trong hệ thống
        var allRoles = await _db.Set<IdentityRole<Guid>>()
            .ToListAsync(cancellationToken);

        // Tạo vai trò nếu hệ thống chưa có
        foreach (var reqRole in normalizedRequestedRoles)
        {
            if (!allRoles.Any(r => string.Equals(r.Name, reqRole, StringComparison.OrdinalIgnoreCase)))
            {
                var newRole = new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = reqRole,
                    NormalizedName = reqRole.ToUpperInvariant()
                };
                _db.Set<IdentityRole<Guid>>().Add(newRole);
                allRoles.Add(newRole);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Lấy danh sách quan hệ UserRoles hiện tại của User
        var currentUserRoles = await _db.Set<IdentityUserRole<Guid>>()
            .Where(ur => ur.UserId == user.Id)
            .ToListAsync(cancellationToken);

        var currentRoleIds = currentUserRoles.Select(ur => ur.RoleId).ToHashSet();

        var requestedRoleIds = allRoles
            .Where(r => normalizedRequestedRoles.Any(nr => string.Equals(nr, r.Name, StringComparison.OrdinalIgnoreCase)))
            .Select(r => r.Id)
            .ToHashSet();

        // Xóa các roles không còn nằm trong danh sách mới
        var rolesToRemove = currentUserRoles
            .Where(ur => !requestedRoleIds.Contains(ur.RoleId))
            .ToList();

        if (rolesToRemove.Count > 0)
        {
            _db.Set<IdentityUserRole<Guid>>().RemoveRange(rolesToRemove);
        }

        // Thêm các roles mới
        var rolesToAdd = requestedRoleIds
            .Where(rid => !currentRoleIds.Contains(rid))
            .Select(rid => new IdentityUserRole<Guid>
            {
                UserId = user.Id,
                RoleId = rid
            })
            .ToList();

        if (rolesToAdd.Count > 0)
        {
            _db.Set<IdentityUserRole<Guid>>().AddRange(rolesToAdd);
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return (true, $"Đã cập nhật phân quyền người dùng thành công: [{string.Join(", ", normalizedRequestedRoles)}].", Array.Empty<string>());
    }
}
