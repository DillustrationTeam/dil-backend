using System.Globalization;
using ArtCommission.Application.Ai.ReferenceSketch;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Ai;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Ai.Commands.StartReferenceSketchGeneration;

public sealed record StartReferenceSketchResultDto(Guid GenerationId);

/// <summary>
/// AI phác thảo bố cục — khởi tạo 1 lượt sinh ảnh: kiểm tra điều kiện bật tính năng + quota,
/// tạo bản ghi <see cref="AiReferenceGeneration"/> Pending rồi đưa job vào hàng đợi nền.
/// Job (xem <see cref="IReferenceSketchGenerationJob"/>) mới thực sự gọi AI.
/// </summary>
public sealed record StartReferenceSketchGenerationCommand(
    Guid ClientId,
    Guid CreatorProfileId,
    string Brief,
    string? LayoutHint
) : IRequest<(bool Success, StartReferenceSketchResultDto? Data, string[] Errors)>;

public class StartReferenceSketchGenerationCommandValidator : AbstractValidator<StartReferenceSketchGenerationCommand>
{
    public StartReferenceSketchGenerationCommandValidator()
    {
        RuleFor(x => x.CreatorProfileId)
            .NotEqual(Guid.Empty).WithMessage("Thiếu creatorProfileId.");

        RuleFor(x => x.Brief)
            .NotEmpty().WithMessage("Vui lòng nhập mô tả yêu cầu (brief).")
            .Must(b => (b ?? string.Empty).Trim().Length >= ReferenceSketchLimits.MinBriefLength)
            .WithMessage($"Brief phải có ít nhất {ReferenceSketchLimits.MinBriefLength} ký tự.");

        RuleFor(x => x.LayoutHint)
            .Must(h => string.IsNullOrEmpty(h) || h.Trim().Length <= ReferenceSketchLimits.MaxLayoutHintLength)
            .WithMessage($"Gợi ý bố cục tối đa {ReferenceSketchLimits.MaxLayoutHintLength} ký tự.");
    }
}

public class StartReferenceSketchGenerationCommandHandler
    : IRequestHandler<StartReferenceSketchGenerationCommand, (bool Success, StartReferenceSketchResultDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly IReferenceSketchGenerationQueue _queue;
    private readonly ILogger<StartReferenceSketchGenerationCommandHandler> _logger;

    public StartReferenceSketchGenerationCommandHandler(
        IApplicationDbContext db,
        IReferenceSketchGenerationQueue queue,
        ILogger<StartReferenceSketchGenerationCommandHandler> logger)
    {
        _db = db;
        _queue = queue;
        _logger = logger;
    }

    public async Task<(bool Success, StartReferenceSketchResultDto? Data, string[] Errors)> Handle(
        StartReferenceSketchGenerationCommand request,
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

        var windowStart = DateTimeOffset.UtcNow - ReferenceSketchLimits.QuotaWindow;
        var used = await _db.AiReferenceGenerations.AsNoTracking()
            .CountAsync(g =>
                g.ClientId == request.ClientId &&
                g.CreatorProfileId == request.CreatorProfileId &&
                g.Status == AiReferenceGenerationStatus.Succeeded &&
                g.CreatedAt >= windowStart,
                cancellationToken);

        // Giới hạn lượt sinh ảnh — admin cấu hình tại /admin/fee-policy (PlatformConfig), fallback
        // về ReferenceSketchLimits.MaxGenerationsPerWindow nếu admin chưa từng cấu hình.
        var limitConfig = await _db.PlatformConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == PlatformConfigKeys.AiReferenceSketchMaxGenerationsPerWindow && !c.IsDeleted, cancellationToken);
        var maxGenerationsPerWindow = limitConfig is not null && int.TryParse(limitConfig.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedLimit)
            ? parsedLimit
            : ReferenceSketchLimits.MaxGenerationsPerWindow;

        if (used >= maxGenerationsPerWindow)
        {
            return (false, null, new[] { "Đã dùng hết lượt phác thảo AI trong 24 giờ. Vui lòng thử lại sau." });
        }

        var generation = new AiReferenceGeneration
        {
            ClientId = request.ClientId,
            CreatorProfileId = request.CreatorProfileId,
            BriefText = request.Brief.Trim(),
            LayoutHint = string.IsNullOrWhiteSpace(request.LayoutHint) ? null : request.LayoutHint.Trim(),
            Status = AiReferenceGenerationStatus.Pending,
            ProgressPercent = 0
        };

        _db.AiReferenceGenerations.Add(generation);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Khởi tạo lượt sinh ảnh phác thảo bố cục {GenerationId} cho khách {ClientId} / creator {CreatorProfileId}.",
            generation.Id, request.ClientId, request.CreatorProfileId);

        _queue.Enqueue(generation.Id);

        return (true, new StartReferenceSketchResultDto(generation.Id), Array.Empty<string>());
    }
}
