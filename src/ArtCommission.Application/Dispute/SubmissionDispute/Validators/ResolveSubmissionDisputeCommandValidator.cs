using ArtCommission.Application.Dispute.SubmissionDispute.Commands;
using FluentValidation;

namespace ArtCommission.Application.Dispute.SubmissionDispute.Validators;

public class ResolveSubmissionDisputeCommandValidator : AbstractValidator<ResolveSubmissionDisputeCommand>
{
    private static readonly string[] AllowedResolutions = ["Dismiss", "Disqualify", "RequestReEvaluation", "Other"];

    public ResolveSubmissionDisputeCommandValidator()
    {
        RuleFor(x => x.DisputeId)
            .NotEmpty().WithMessage("Mã vụ khiếu nại (DisputeId) không được để trống.");

        RuleFor(x => x.ModeratorId)
            .NotEmpty().WithMessage("Mã người xử lý (ModeratorId) không được để trống.");

        RuleFor(x => x.Resolution)
            .NotEmpty().WithMessage("Hình thức giải quyết (Resolution) không được để trống.")
            .Must(r => AllowedResolutions.Contains(r, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Hình thức giải quyết không hợp lệ (hợp lệ: Dismiss, Disqualify, RequestReEvaluation, Other).");
    }
}
