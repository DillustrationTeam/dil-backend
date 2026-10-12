using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ArtCommission.Application.Dispute.SubmissionDispute.Commands;

public record CreateSubmissionDisputeCommand(
    Guid SubmissionId,
    Guid RaisedById,
    string Category,
    string Reason,
    string? Description = null,
    List<string>? EvidenceUrls = null,
    string Priority = "Medium"
) : IRequest<(bool Success, string Message, Guid? DisputeId, string[] Errors)>;

public class CreateSubmissionDisputeCommandValidator : AbstractValidator<CreateSubmissionDisputeCommand>
{
    private static readonly string[] AllowedCategories = ["Copyright", "ScoreAppeal", "AiArt", "VoteFraud", "Other"];
    private static readonly string[] AllowedPriorities = ["Low", "Medium", "High"];

    public CreateSubmissionDisputeCommandValidator()
    {
        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("Mã bài dự thi (SubmissionId) không được để trống.");

        RuleFor(x => x.RaisedById)
            .NotEmpty().WithMessage("Mã người gửi khiếu nại (RaisedById) không được để trống.");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Danh mục khiếu nại không được để trống.")
            .Must(c => AllowedCategories.Contains(c, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Danh mục khiếu nại không hợp lệ (hợp lệ: Copyright, ScoreAppeal, AiArt, VoteFraud, Other).");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do khiếu nại không được để trống.")
            .MaximumLength(2000).WithMessage("Lý do khiếu nại không được vượt quá 2000 ký tự.");

        RuleFor(x => x.Priority)
            .Must(p => string.IsNullOrWhiteSpace(p) || AllowedPriorities.Contains(p, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Mức độ ưu tiên không hợp lệ (hợp lệ: Low, Medium, High).");
    }
}

public class CreateSubmissionDisputeCommandHandler
    : IRequestHandler<CreateSubmissionDisputeCommand, (bool Success, string Message, Guid? DisputeId, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public CreateSubmissionDisputeCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, string Message, Guid? DisputeId, string[] Errors)> Handle(
        CreateSubmissionDisputeCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new CreateSubmissionDisputeCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, "Dữ liệu không hợp lệ.", null, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var submission = await _db.EventSubmissions
            .Include(s => s.Event)
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId, cancellationToken);

        if (submission == null)
        {
            return (false, "Không tìm thấy bài dự thi.", null, ["Bài dự thi không tồn tại."]);
        }

        if (submission.Event == null || submission.Event.IsDeleted)
        {
            return (false, "Sự kiện của bài dự thi không tồn tại hoặc đã bị xóa.", null, ["Sự kiện không hợp lệ."]);
        }

        // Tránh spam: Kiểm tra xem người dùng đã gửi khiếu nại đang chờ xử lý cho bài thi này chưa
        var existingDispute = await _db.SubmissionDisputes
            .FirstOrDefaultAsync(d =>
                d.SubmissionId == request.SubmissionId &&
                d.RaisedById == request.RaisedById &&
                (d.Status == "Pending" || d.Status == "UnderReview"),
                cancellationToken);

        if (existingDispute != null)
        {
            return (false, "Bạn đã gửi khiếu nại cho bài dự thi này và hiện đang được ban tổ chức xử lý.", null, ["Khiếu nại đang chờ xử lý đã tồn tại."]);
        }

        string fullReason = string.IsNullOrWhiteSpace(request.Description)
            ? request.Reason.Trim()
            : $"{request.Reason.Trim()}\nChi tiết: {request.Description.Trim()}";

        if (fullReason.Length > 2000)
        {
            fullReason = fullReason[..2000];
        }

        string? serializedEvidence = null;
        if (request.EvidenceUrls != null && request.EvidenceUrls.Count > 0)
        {
            serializedEvidence = JsonSerializer.Serialize(request.EvidenceUrls);
        }

        var dispute = new Domain.Entities.Dispute.SubmissionDispute
        {
            EventId = submission.EventId,
            SubmissionId = submission.Id,
            RaisedById = request.RaisedById,
            Category = request.Category.Trim(),
            Priority = string.IsNullOrWhiteSpace(request.Priority) ? "Medium" : request.Priority.Trim(),
            Reason = fullReason,
            EvidenceUrls = serializedEvidence,
            Status = "Pending",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.SubmissionDisputes.Add(dispute);
        await _db.SaveChangesAsync(cancellationToken);

        return (true, "Gửi khiếu nại bài dự thi thành công.", dispute.Id, Array.Empty<string>());
    }
}
