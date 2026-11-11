using ArtCommission.Application.Dispute.SubmissionDispute.Commands;
using FluentValidation;

namespace ArtCommission.Application.Dispute.SubmissionDispute.Validators;

public class CreateSubmissionDisputeCommandValidator : AbstractValidator<CreateSubmissionDisputeCommand>
{
    private static readonly string[] AllowedCategories =
        ["AiArt", "Copyright", "VoteFraud", "RuleViolation", "ScoreAppeal", "Other"];
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

        RuleFor(x => x.Category)
            .Must(c => string.IsNullOrWhiteSpace(c) || AllowedCategories.Contains(c.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Loại khiếu nại không hợp lệ (hợp lệ: AiArt, Copyright, VoteFraud, RuleViolation, ScoreAppeal, Other).");

        RuleFor(x => x.Priority)
            .Must(p => string.IsNullOrWhiteSpace(p) || AllowedPriorities.Contains(p.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Mức độ ưu tiên không hợp lệ (hợp lệ: Low, Medium, High).");
    }

    public static (string Category, string Priority) ResolveCategoryAndPriority(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return ("Other", "Low");
        }

        var normalized = AllowedCategories.FirstOrDefault(
            c => string.Equals(c, category.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "Other";

        var derivedPriority = normalized switch
        {
            "AiArt" or "Copyright" or "VoteFraud" => "High",
            "RuleViolation" or "ScoreAppeal" => "Medium",
            _ => "Low"
        };

        return (normalized, derivedPriority);
    }

    public static string GetCategoryDisplayName(string? category) => category switch
    {
        "AiArt" => "Nghi vấn AI Art",
        "Copyright" => "Vi phạm bản quyền",
        "VoteFraud" => "Gian lận bình chọn",
        "RuleViolation" => "Vi phạm thể lệ",
        "ScoreAppeal" => "Khiếu nại điểm số",
        _ => "Nội dung khác"
    };
}
