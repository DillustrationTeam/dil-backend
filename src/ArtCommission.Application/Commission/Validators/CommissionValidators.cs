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
        RuleFor(x => x.Description).MaximumLength(4000).WithMessage("Mô tả không được vượt quá 4000 ký tự.");
        RuleFor(x => x.DeadlineAt)
            .Must(deadline => !deadline.HasValue || deadline.Value > DateTimeOffset.UtcNow)
            .WithMessage("Hạn hoàn thành phải nằm trong tương lai.");
        RuleFor(x => x.VoucherCode).MaximumLength(50).WithMessage("Mã giảm giá tối đa 50 ký tự.");
        RuleFor(x => x.ReferenceImages)
            .Must(urls => urls is null || urls.Count <= 10)
            .WithMessage("Chỉ được đính kèm tối đa 10 tệp/liên kết tham chiếu.");
        RuleForEach(x => x.ReferenceImages)
            .NotEmpty().WithMessage("Đường dẫn tệp tham chiếu không được để trống.")
            .MaximumLength(2000).WithMessage("Đường dẫn tệp tham chiếu tối đa 2000 ký tự.")
            .Must(IsSafeHttpUrl).WithMessage("Đường dẫn tệp/ảnh tham chiếu phải là HTTP(S) hợp lệ và an toàn.");
        RuleFor(x => x.LayoutHint)
            .MaximumLength(ArtCommission.Application.Ai.ReferenceSketch.ReferenceSketchLimits.MaxLayoutHintLength)
            .WithMessage("Gợi ý bố cục tối đa 300 ký tự.");
        RuleFor(x => x)
            .Must(x => (x.ReferenceImages?.Count ?? 0) + (x.AiReferenceImageIds?.Count ?? 0) <= 10)
            .WithMessage("Chỉ được đính kèm tối đa 10 ảnh/tệp tham chiếu (bao gồm ảnh AI).");
        RuleFor(x => x)
            .Must(x => !x.IsFreestyle || (x.AiReferenceImageIds?.Count ?? 0) == 0)
            .WithMessage("Không thể chọn ảnh AI khi để creator tự freestyle.");
    }

    public static bool IsSafeHttpUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme is not "http" and not "https") return false;

        var host = uri.Host.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(host)) return false;

        if (host is "localhost" or "127.0.0.1" or "::1")
        {
            return uri.AbsolutePath.StartsWith("/demo-uploads/", StringComparison.OrdinalIgnoreCase)
                || uri.AbsolutePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase);
        }

        if (host is "0.0.0.0" or "169.254.169.254") return false;
        if (host.EndsWith(".local") || host.EndsWith(".internal") || host.EndsWith(".lan")) return false;

        if (System.Net.IPAddress.TryParse(host, out var ip))
        {
            if (System.Net.IPAddress.IsLoopback(ip)) return false;
            var bytes = ip.GetAddressBytes();
            if (bytes.Length == 4)
            {
                if (bytes[0] == 10) return false;
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return false;
                if (bytes[0] == 192 && bytes[1] == 168) return false;
                if (bytes[0] == 169 && bytes[1] == 254) return false;
            }
        }

        return true;
    }
}

public class RespondCommissionRequestValidator : AbstractValidator<RespondCommissionRequest>
{
    public RespondCommissionRequestValidator()
    {
        RuleFor(x => x.Action)
            .Must(a => new[] { "Accept", "Reject", "Negotiate" }.Contains(a, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Hành động xử lý chỉ có thể là Accept, Reject, hoặc Negotiate.");
        RuleFor(x => x.NegotiatePrice)
            .GreaterThan(0).LessThanOrEqualTo(500_000_000m)
            .When(x => x.NegotiatePrice.HasValue)
            .WithMessage("Giá đề xuất phải từ 1 đến 500.000.000 VND.");
        RuleFor(x => x.RejectReason)
            .NotEmpty().When(x => string.Equals(x.Action, "Reject", StringComparison.OrdinalIgnoreCase))
            .MaximumLength(500).WithMessage("Lý do từ chối tối đa 500 ký tự.");
    }
}

public class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator()
    {
        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 5)
            .WithMessage("Số sao đánh giá phải từ 1 đến 5 sao.");
        RuleFor(x => x.Comment).MaximumLength(1000).WithMessage("Nội dung đánh giá tối đa 1000 ký tự.");
        RuleFor(x => x.AttachedImageUrls)
            .Must(urls => urls is null || urls.Count <= 5)
            .WithMessage("Chỉ được đính kèm tối đa 5 ảnh.");
        RuleForEach(x => x.AttachedImageUrls)
            .MaximumLength(500)
            .Must(IsHttpUrl)
            .WithMessage("URL ảnh đính kèm phải là HTTP(S) hợp lệ và tối đa 500 ký tự.");
    }

    private static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}

public class RequestRevisionRequestValidator : AbstractValidator<RequestRevisionRequest>
{
    public RequestRevisionRequestValidator()
    {
        RuleFor(x => x.FeedbackComment).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ReferenceImages).Must(urls => urls is null || urls.Count <= 10)
            .WithMessage("Chỉ được gửi tối đa 10 ảnh tham chiếu.");
        RuleForEach(x => x.ReferenceImages).MaximumLength(500).Must(IsHttpUrl)
            .WithMessage("URL ảnh tham chiếu phải là HTTP(S) hợp lệ và tối đa 500 ký tự.");
    }

    private static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}

public class CreateDisputeRequestValidator : AbstractValidator<CreateDisputeRequest>
{
    public CreateDisputeRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.EvidenceUrls).Must(urls => urls is null || urls.Count <= 10)
            .WithMessage("Chỉ được gửi tối đa 10 bằng chứng.");
        RuleForEach(x => x.EvidenceUrls).MaximumLength(500).Must(IsHttpUrl)
            .WithMessage("URL bằng chứng phải là HTTP(S) hợp lệ và tối đa 500 ký tự.");
    }

    private static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}
