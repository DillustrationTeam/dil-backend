using ArtCommission.Application.Commission.DTOs;
using FluentValidation;

namespace ArtCommission.Application.Commission.Validators;

public class CreateCommissionRequestValidator : AbstractValidator<CreateCommissionRequest>
{
    public CreateCommissionRequestValidator()
    {
        RuleFor(x => x.CreatorId).NotEmpty().WithMessage("CreatorId không được để trống.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).WithMessage("Tiêu đề không được vượt quá 200 ký tự.");
        RuleFor(x => x.TotalPrice).GreaterThan(0).WithMessage("Tổng giá trị đơn vẽ phải lớn hơn 0.");
        RuleFor(x => x.Milestones).NotEmpty().WithMessage("Cần có ít nhất 1 cột mốc tiến độ (Milestone).");
    }
}

public class RespondCommissionRequestValidator : AbstractValidator<RespondCommissionRequest>
{
    public RespondCommissionRequestValidator()
    {
        RuleFor(x => x.Action)
            .Must(a => new[] { "Accept", "Reject", "Negotiate" }.Contains(a, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Hành động xử lý chỉ có thể là Accept, Reject, hoặc Negotiate.");
    }
}

public class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator()
    {
        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 5)
            .WithMessage("Số sao đánh giá phải từ 1 đến 5 sao.");
    }
}
