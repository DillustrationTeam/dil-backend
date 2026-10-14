using ArtCommission.Application.Commission.DTOs;
using FluentValidation;

namespace ArtCommission.Application.Commission.Validators;

public class CreateCommissionRequestValidator : AbstractValidator<CreateCommissionRequest>
{
    public CreateCommissionRequestValidator()
    {
        RuleFor(x => x.CreatorId).NotEmpty().WithMessage("CreatorId không được để trống.");
        RuleFor(x => x.PackageId).NotEmpty().WithMessage("Vui lòng chọn gói giá của Creator.");
        RuleFor(x => x.LicenseType)
            .Must(x => x is "Personal" or "Commercial")
            .WithMessage("LicenseType chỉ có thể là Personal hoặc Commercial.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).WithMessage("Tiêu đề không được vượt quá 200 ký tự.");
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

public class RequestRevisionRequestValidator : AbstractValidator<RequestRevisionRequest>
{
    public RequestRevisionRequestValidator()
    {
        RuleFor(x => x.FeedbackComment)
            .NotEmpty()
            .WithMessage("Feedback is required.");
    }
}

public class CreateDisputeRequestValidator : AbstractValidator<CreateDisputeRequest>
{
    public CreateDisputeRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(2000)
            .WithMessage("Dispute reason is required and cannot exceed 2000 characters.");
    }
}
