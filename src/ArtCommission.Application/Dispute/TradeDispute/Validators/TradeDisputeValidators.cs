using ArtCommission.Application.Dispute.TradeDispute.Commands;
using ArtCommission.Application.Dispute.TradeDispute.DTOs;
using FluentValidation;

namespace ArtCommission.Application.Dispute.TradeDispute.Validators;

public class CreateTradeDisputeCommandValidator : AbstractValidator<CreateTradeDisputeCommand>
{
    public CreateTradeDisputeCommandValidator()
    {
        RuleFor(x => x.ArtTradeId).NotEmpty().WithMessage("Mã giao dịch không hợp lệ.");
        RuleFor(x => x.RaisedById).NotEmpty().WithMessage("Mã người khiếu nại không hợp lệ.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Vui lòng cung cấp lý do khiếu nại.")
            .MaximumLength(2000).WithMessage("Lý do khiếu nại không được vượt quá 2000 ký tự.");
    }
}

public class ResolveTradeDisputeCommandValidator : AbstractValidator<ResolveTradeDisputeCommand>
{
    public ResolveTradeDisputeCommandValidator()
    {
        RuleFor(x => x.DisputeId).NotEmpty().WithMessage("Mã khiếu nại không hợp lệ.");
        RuleFor(x => x.AdminId).NotEmpty().WithMessage("Mã quản trị viên không hợp lệ.");
        RuleFor(x => x.Resolution)
            .NotEmpty().WithMessage("Phán quyết không được để trống.")
            .Must(r => TradeDisputeResolutions.All.Contains(r))
            .WithMessage($"Phán quyết không hợp lệ. Chỉ chấp nhận: {string.Join(", ", TradeDisputeResolutions.All)}.");

        RuleFor(x => x.AdminNote)
            .MaximumLength(2000).WithMessage("Ghi chú của Admin không được vượt quá 2000 ký tự.");
    }
}

