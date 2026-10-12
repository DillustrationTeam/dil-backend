using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Dispute.SubmissionDispute.Commands;

public record ResolveSubmissionDisputeCommand(
    Guid DisputeId,
    Guid ModeratorId,
    string Resolution, // Dismiss, Disqualify, AdjustScore, DeductVotes
    string? Note
) : IRequest<(bool Success, string Message, string[] Errors)>;

public class ResolveSubmissionDisputeCommandValidator : AbstractValidator<ResolveSubmissionDisputeCommand>
{
    private static readonly string[] AllowedResolutions = ["Dismiss", "Disqualify", "AdjustScore", "DeductVotes", "Other"];

    public ResolveSubmissionDisputeCommandValidator()
    {
        RuleFor(x => x.DisputeId)
            .NotEmpty().WithMessage("Mã vụ khiếu nại (DisputeId) không được để trống.");

        RuleFor(x => x.ModeratorId)
            .NotEmpty().WithMessage("Mã người xử lý (ModeratorId) không được để trống.");

        RuleFor(x => x.Resolution)
            .NotEmpty().WithMessage("Hình thức giải quyết (Resolution) không được để trống.")
            .Must(r => AllowedResolutions.Contains(r, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Hình thức giải quyết không hợp lệ (hợp lệ: Dismiss, Disqualify, AdjustScore, DeductVotes, Other).");
    }
}

public class ResolveSubmissionDisputeCommandHandler
    : IRequestHandler<ResolveSubmissionDisputeCommand, (bool Success, string Message, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public ResolveSubmissionDisputeCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, string Message, string[] Errors)> Handle(
        ResolveSubmissionDisputeCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new ResolveSubmissionDisputeCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, "Dữ liệu không hợp lệ.", validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var dispute = await _db.SubmissionDisputes
            .Include(d => d.Submission)
            .FirstOrDefaultAsync(d => d.Id == request.DisputeId, cancellationToken);

        if (dispute == null)
        {
            return (false, "Không tìm thấy khiếu nại bài dự thi.", ["Khiếu nại không tồn tại."]);
        }

        if (dispute.Status == "Resolved" || dispute.Status == "Rejected")
        {
            return (false, "Khiếu nại này đã được xử lý trước đó.", ["Khiếu nại đã đóng."]);
        }

        bool isDismissed = string.Equals(request.Resolution, "Dismiss", StringComparison.OrdinalIgnoreCase);

        dispute.Status = isDismissed ? "Rejected" : "Resolved";
        dispute.Resolution = request.Resolution.Trim();
        dispute.Note = request.Note?.Trim();
        dispute.ResolvedAt = DateTimeOffset.UtcNow;
        dispute.UpdatedAt = DateTimeOffset.UtcNow;

        // Nếu truất quyền thi đấu bài thi (Disqualify)
        if (string.Equals(request.Resolution, "Disqualify", StringComparison.OrdinalIgnoreCase) && dispute.Submission != null)
        {
            dispute.Submission.AdminNote = !string.IsNullOrWhiteSpace(request.Note)
                ? $"[Bị truất quyền do khiếu nại {dispute.Category}]: {request.Note}"
                : $"[Bị truất quyền do khiếu nại {dispute.Category}]";
        }

        await _db.SaveChangesAsync(cancellationToken);

        string message = isDismissed
            ? "Đã bác bỏ khiếu nại bài dự thi."
            : $"Đã xử lý khiếu nại bài dự thi theo phán quyết: {request.Resolution}.";

        return (true, message, Array.Empty<string>());
    }
}
