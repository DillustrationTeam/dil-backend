using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Moderation.Commands;

/// <summary>
/// Command thực thi quyết định kiểm duyệt tác phẩm (SCR-20 / UC28).
/// </summary>
public record ExecuteModerationDecisionCommand(
    Guid ArtworkId,
    Guid ModeratorId,
    string Action,
    string? ModerationNote
) : IRequest<(bool Success, string Message, string[] Errors)>;

public class ExecuteModerationDecisionCommandValidator : AbstractValidator<ExecuteModerationDecisionCommand>
{
    private static readonly string[] AllowedActions = { "approve", "reject", "hide", "flag_ai" };

    public ExecuteModerationDecisionCommandValidator()
    {
        RuleFor(x => x.ArtworkId)
            .NotEmpty().WithMessage("Mã tác phẩm (ArtworkId) không được để trống.");

        RuleFor(x => x.ModeratorId)
            .NotEmpty().WithMessage("Mã kiểm duyệt viên (ModeratorId) không được để trống.");

        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("Hành động kiểm duyệt không được để trống.")
            .Must(a => AllowedActions.Contains(a.Trim().ToLowerInvariant()))
            .WithMessage("Hành động kiểm duyệt không hợp lệ. Các lựa chọn cho phép: approve, reject, hide, flag_ai.");

        RuleFor(x => x.ModerationNote)
            .MaximumLength(1000).WithMessage("Ghi chú kiểm duyệt không được vượt quá 1000 ký tự.");

        When(x => x.Action.Trim().Equals("reject", StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.ModerationNote)
                .NotEmpty().WithMessage("Vui lòng nhập lý do/ghi chú khi từ chối và xóa tác phẩm.");
        });
    }
}

public class ExecuteModerationDecisionCommandHandler 
    : IRequestHandler<ExecuteModerationDecisionCommand, (bool Success, string Message, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public ExecuteModerationDecisionCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, string Message, string[] Errors)> Handle(
        ExecuteModerationDecisionCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new ExecuteModerationDecisionCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, string.Empty, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var artwork = await _db.Artworks
            .Include(a => a.ArtworkTags)
                .ThenInclude(at => at.Tag)
            .FirstOrDefaultAsync(a => a.Id == request.ArtworkId, cancellationToken);

        if (artwork == null || artwork.IsDeleted)
        {
            return (false, string.Empty, new[] { "Không tìm thấy tác phẩm hoặc tác phẩm đã bị xóa." });
        }

        var action = request.Action.Trim().ToLowerInvariant();
        string resultMessage;

        switch (action)
        {
            case "approve":
                artwork.ModerationStatus = "Approved";
                artwork.IsDeleted = false;
                artwork.FlagReason = null;
                resultMessage = "Tác phẩm đã được phê duyệt và hiển thị công khai trên Feed.";
                break;

            case "reject":
                artwork.ModerationStatus = "Rejected";
                artwork.IsDeleted = true;
                artwork.FlagReason = null;
                resultMessage = "Tác phẩm đã bị từ chối và xóa khỏi hệ thống do vi phạm.";
                break;

            case "hide":
                artwork.ModerationStatus = "Hidden";
                artwork.FlagReason = null;
                resultMessage = "Tác phẩm đã được ẩn khỏi danh sách gợi ý và khám phá.";
                break;

            case "flag_ai":
                artwork.IsAiGenerated = true;
                artwork.AiDetectionScore = artwork.AiDetectionScore ?? 1.0m;
                artwork.ModerationStatus = "Approved";
                artwork.FlagReason = null;
                foreach (var at in artwork.ArtworkTags.Where(at => at.Tag != null))
                {
                    at.Tag!.IsAiGenerated = true;
                }
                resultMessage = "Tác phẩm đã được gắn cờ là tranh do AI tạo và cập nhật các tag liên quan.";
                break;

            default:
                return (false, string.Empty, new[] { "Hành động kiểm duyệt không hợp lệ." });
        }

        artwork.ModeratorId = request.ModeratorId;
        artwork.ModerationNote = request.ModerationNote;
        artwork.ModeratedAt = DateTimeOffset.UtcNow;
        artwork.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return (true, resultMessage, Array.Empty<string>());
    }
}
