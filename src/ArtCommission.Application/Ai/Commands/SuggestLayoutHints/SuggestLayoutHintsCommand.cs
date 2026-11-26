using ArtCommission.Application.Ai.ReferenceSketch;
using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Commands.SuggestLayoutHints;

public sealed record LayoutHintDto(string Technique, string TextVi);

public sealed record SuggestLayoutHintsResultDto(LayoutHintDto[] Hints);

/// <summary>
/// Nút "🎲 Gợi ý bố cục độc đáo" cạnh ô "Gợi ý bố cục" trong form đặt commission — trả 3 gợi ý
/// bố cục ngắn tiếng Việt dựa trên 3 kỹ thuật bố cục ngẫu nhiên khác nhau. Không trừ lượt sinh
/// ảnh (3 lượt/24h chỉ tính cho <c>StartReferenceSketchGenerationCommand</c>), chỉ gắn rate
/// limiter riêng ở endpoint.
/// </summary>
public sealed record SuggestLayoutHintsCommand(
    Guid ClientId,
    Guid CreatorProfileId,
    string Brief,
    string? CurrentHint
) : IRequest<(bool Success, SuggestLayoutHintsResultDto? Data, string[] Errors)>;

public class SuggestLayoutHintsCommandValidator : AbstractValidator<SuggestLayoutHintsCommand>
{
    public SuggestLayoutHintsCommandValidator()
    {
        RuleFor(x => x.CreatorProfileId)
            .NotEqual(Guid.Empty).WithMessage("Thiếu creatorProfileId.");

        RuleFor(x => x.Brief)
            .NotEmpty().WithMessage("Vui lòng nhập mô tả yêu cầu (brief).")
            .Must(b => (b ?? string.Empty).Trim().Length >= ReferenceSketchLimits.MinBriefLength)
            .WithMessage($"Brief phải có ít nhất {ReferenceSketchLimits.MinBriefLength} ký tự.");

        RuleFor(x => x.CurrentHint)
            .Must(h => string.IsNullOrEmpty(h) || h.Trim().Length <= ReferenceSketchLimits.MaxLayoutHintLength)
            .WithMessage($"Gợi ý bố cục tối đa {ReferenceSketchLimits.MaxLayoutHintLength} ký tự.");
    }
}

public class SuggestLayoutHintsCommandHandler
    : IRequestHandler<SuggestLayoutHintsCommand, (bool Success, SuggestLayoutHintsResultDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly ReferenceSketchPromptService _promptService;

    public SuggestLayoutHintsCommandHandler(IApplicationDbContext db, ReferenceSketchPromptService promptService)
    {
        _db = db;
        _promptService = promptService;
    }

    public async Task<(bool Success, SuggestLayoutHintsResultDto? Data, string[] Errors)> Handle(
        SuggestLayoutHintsCommand request,
        CancellationToken cancellationToken)
    {
        var creator = await _db.CreatorProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.CreatorProfileId && !p.IsDeleted, cancellationToken);
        if (creator is null)
        {
            return (false, null, new[] { "Không tìm thấy họa sĩ." });
        }

        var terms = await _db.CreatorTerms.AsNoTracking()
            .FirstOrDefaultAsync(t => t.CreatorProfileId == request.CreatorProfileId && !t.IsDeleted, cancellationToken);
        if (terms is null || !terms.AllowAiReferenceSketch)
        {
            return (false, null, new[] { "Họa sĩ chưa cho phép dùng AI phác thảo bố cục." });
        }

        var wontDraw = CreatorTermsHelper.ParseExtended(terms.ExtendedTermsJson).WontDraw;
        var techniques = LayoutHintTechniques.PickRandom(3);

        var result = await _promptService.SuggestLayoutHintsAsync(
            request.Brief.Trim(), request.CurrentHint, wontDraw, techniques, cancellationToken);

        if (!result.Success)
        {
            return (false, null, new[] { result.Error ?? "Không thể tạo gợi ý bố cục lúc này." });
        }

        var hints = result.Hints!.Select(h => new LayoutHintDto(h.Technique, h.TextVi)).ToArray();
        return (true, new SuggestLayoutHintsResultDto(hints), Array.Empty<string>());
    }
}
