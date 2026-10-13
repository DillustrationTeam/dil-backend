using ArtCommission.Application.Dispute.CommissionDispute.Commands;
using FluentValidation;

namespace ArtCommission.Application.Dispute.CommissionDispute.Validators;

public class ResolveDisputeArbitrationCommandValidator : AbstractValidator<ResolveDisputeArbitrationCommand>
{
    public ResolveDisputeArbitrationCommandValidator()
    {
        RuleFor(x => x.DisputeId)
            .NotEmpty().WithMessage("Mã vụ tranh chấp (DisputeId) không được để trống.");

        RuleFor(x => x.ClientRefundPercent)
            .InclusiveBetween(0m, 100m)
            .WithMessage("Tỷ lệ hoàn tiền cho Client phải nằm trong khoảng từ 0% đến 100%.");

        RuleFor(x => x.AdminNote)
            .NotEmpty().WithMessage("Vui lòng nhập lý do/căn cứ phán quyết tranh chấp của trọng tài.")
            .MaximumLength(2000).WithMessage("Ghi chú phán quyết không được vượt quá 2000 ký tự.");
    }
}
