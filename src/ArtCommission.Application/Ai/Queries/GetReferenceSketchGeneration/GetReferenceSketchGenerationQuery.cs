using System.Text.Json;
using ArtCommission.Application.Ai.ReferenceSketch;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Queries.GetReferenceSketchGeneration;

public sealed record ReferenceSketchGenerationDto(
    Guid GenerationId,
    string Status,
    int Percent,
    ReferenceSketchImageDto[]? Images,
    string? Error,
    string? BlockReasonVi,
    string? BlockReasonEn);

/// <summary>
/// GET /api/v1/ai/reference-sketches/{id} — trạng thái + % + ảnh hiện tại của 1 lượt sinh.
/// Dùng làm fallback khi kết nối SignalR rớt giữa lúc đang sinh ảnh.
/// </summary>
public sealed record GetReferenceSketchGenerationQuery(Guid GenerationId, Guid RequesterId)
    : IRequest<(bool Success, ReferenceSketchGenerationDto? Data, string[] Errors)>;

public class GetReferenceSketchGenerationQueryHandler
    : IRequestHandler<GetReferenceSketchGenerationQuery, (bool Success, ReferenceSketchGenerationDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly IStorageService _storage;

    public GetReferenceSketchGenerationQueryHandler(IApplicationDbContext db, IStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<(bool Success, ReferenceSketchGenerationDto? Data, string[] Errors)> Handle(
        GetReferenceSketchGenerationQuery request,
        CancellationToken cancellationToken)
    {
        var generation = await _db.AiReferenceGenerations.AsNoTracking()
            .Include(g => g.Images)
            .FirstOrDefaultAsync(g => g.Id == request.GenerationId && !g.IsDeleted, cancellationToken);

        if (generation is null || generation.ClientId != request.RequesterId)
        {
            return (false, null, new[] { "Không tìm thấy lượt sinh ảnh phác thảo." });
        }

        string? blockReasonVi = null;
        string? blockReasonEn = null;
        if (!string.IsNullOrWhiteSpace(generation.BlockReason))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<BlockReasonJson>(generation.BlockReason);
                blockReasonVi = parsed?.Vi;
                blockReasonEn = parsed?.En;
            }
            catch (JsonException)
            {
                blockReasonVi = generation.BlockReason;
            }
        }

        var images = generation.Images.Count > 0
            ? generation.Images
                .OrderBy(i => i.VariantIndex)
                .Select(i => new ReferenceSketchImageDto(
                    i.Id,
                    i.VariantIndex,
                    _storage.GeneratePresignedDownloadUrl(i.TempStorageKey, TimeSpan.FromHours(1)),
                    i.LabelVi,
                    i.LabelEn))
                .ToArray()
            : null;

        var dto = new ReferenceSketchGenerationDto(
            generation.Id,
            generation.Status.ToString(),
            generation.ProgressPercent,
            images,
            generation.Error,
            blockReasonVi,
            blockReasonEn);

        return (true, dto, Array.Empty<string>());
    }

    private sealed record BlockReasonJson(string? Vi, string? En);
}
