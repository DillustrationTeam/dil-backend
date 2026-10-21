using System.Data;
using ArtCommission.Application.Commission.DTOs;
using FluentValidation;

namespace ArtCommission.Application.Commission.Validators;

public class CreateCommissionRequestValidator : AbstractValidator<CreateCommissionRequest>
{
    public CreateCommissionRequestValidator()
    {
        RuleFor(x => x.CreatorId).NotEmpty();
        RuleFor(x => x.PackageId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.LicenseType).Must(x => x is "Personal" or "Commercial")
            .WithMessage("Loại giấy phép phải là Personal hoặc Commercial.");
        RuleFor(x => x.DeadlineAt)
            .Must(d => !d.HasValue || d.Value > DateTimeOffset.UtcNow)
            .WithMessage("Hạn chót phải ở trong tương lai.");
    }
}

public class RespondCommissionRequestValidator : AbstractValidator<RespondCommissionRequest>
{
    public RespondCommissionRequestValidator()
    {
        RuleFor(x => x.Action).Must(x => x is "Accept" or "Reject" or "Negotiate")
            .WithMessage("Hành động phải là Accept, Reject hoặc Negotiate.");
        When(x => x.Action == "Negotiate", () =>
        {
            RuleFor(x => x.NegotiatePrice).NotNull().GreaterThan(0)
                .WithMessage("Giá thương lượng phải lớn hơn 0.");
        });
        When(x => x.Action == "Reject", () =>
        {
            RuleFor(x => x.RejectReason).NotEmpty().MaximumLength(500)
                .WithMessage("Lý do từ chối không được để trống và tối đa 500 ký tự.");
        });
    }
}

public class SubmitMilestoneRequestValidator : AbstractValidator<SubmitMilestoneRequest>
{
    public SubmitMilestoneRequestValidator()
    {
        RuleFor(x => x.WipFileUrl).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.CreatorNote).MaximumLength(1000);
    }
}

public class RequestRevisionRequestValidator : AbstractValidator<RequestRevisionRequest>
{
    public RequestRevisionRequestValidator()
    {
        RuleFor(x => x.FeedbackComment).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ReferenceImages).Must(images => images is null || images.Count <= 5)
            .WithMessage("Chỉ được đính kèm tối đa 5 ảnh tham khảo.");
    }
}

public class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5).WithMessage("Đánh giá phải từ 1 đến 5 sao.");
        RuleFor(x => x.Comment).MaximumLength(1000).WithMessage("Nhận xét tối đa 1000 ký tự.");
    }
}

public class CreateDisputeRequestValidator : AbstractValidator<CreateDisputeRequest>
{
    public CreateDisputeRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ClaimantRefundPercent).InclusiveBetween(0, 100)
            .When(x => x.ClaimantRefundPercent.HasValue)
            .WithMessage("Tỷ lệ hoàn tiền yêu cầu phải từ 0% đến 100%.");
        RuleFor(x => x.EvidenceUrls).Must(urls => urls is null || urls.Count <= 10)
            .WithMessage("Chỉ được gửi tối đa 10 bằng chứng.");
        RuleForEach(x => x.EvidenceUrls).MaximumLength(500).Must(IsHttpUrl)
            .WithMessage("URL bằng chứng phải là HTTP(S) hợp lệ và tối đa 500 ký tự.");
    }

    private static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}

public class RespondDisputeRequestValidator : AbstractValidator<RespondDisputeRequest>
{
    public RespondDisputeRequestValidator()
    {
        RuleFor(x => x.Action).Must(a => a is "Accept" or "Counter")
            .WithMessage("Hành động phải là Accept hoặc Counter.");
        When(x => x.Action == "Counter", () =>
        {
            RuleFor(x => x.CounterRefundPercent).NotNull().InclusiveBetween(0, 100)
                .WithMessage("Mức hoàn đề xuất phải từ 0% đến 100%.");
            RuleFor(x => x.Response).NotEmpty().MaximumLength(2000)
                .WithMessage("Vui lòng nhập lý do/phản hồi chi tiết (tối đa 2000 ký tự).");
        });
    }
}
