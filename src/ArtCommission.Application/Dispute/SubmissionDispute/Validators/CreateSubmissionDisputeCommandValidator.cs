using ArtCommission.Application.Dispute.SubmissionDispute.Commands;
using FluentValidation;

namespace ArtCommission.Application.Dispute.SubmissionDispute.Validators;

public class CreateSubmissionDisputeCommandValidator : AbstractValidator<CreateSubmissionDisputeCommand>
{
    private static readonly string[] AllowedPriorities = ["Low", "Medium", "High"];

    public CreateSubmissionDisputeCommandValidator()
    {
        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("Mã bài dự thi (SubmissionId) không được để trống.");

        RuleFor(x => x.RaisedById)
            .NotEmpty().WithMessage("Mã người gửi khiếu nại (RaisedById) không được để trống.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do khiếu nại không được để trống.")
            .MaximumLength(2000).WithMessage("Lý do khiếu nại không được vượt quá 2000 ký tự.");

        RuleFor(x => x.Priority)
            .Must(p => string.IsNullOrWhiteSpace(p) || AllowedPriorities.Contains(p, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Mức độ ưu tiên không hợp lệ (hợp lệ: Low, Medium, High).");
    }
}
