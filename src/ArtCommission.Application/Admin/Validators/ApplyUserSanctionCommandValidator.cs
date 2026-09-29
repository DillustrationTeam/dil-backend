using ArtCommission.Application.Admin.Commands;
using ArtCommission.Domain.Entities.Identity;
using FluentValidation;

namespace ArtCommission.Application.Admin.Validators;

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
