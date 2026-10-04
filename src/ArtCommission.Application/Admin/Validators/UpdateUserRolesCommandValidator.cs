using ArtCommission.Application.Admin.Commands;
using ArtCommission.Domain.Enums;
using FluentValidation;

namespace ArtCommission.Application.Admin.Validators;

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
