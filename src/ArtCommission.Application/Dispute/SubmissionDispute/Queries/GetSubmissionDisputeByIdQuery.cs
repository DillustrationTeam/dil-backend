using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Dispute.SubmissionDispute.DTOs;
using ArtCommission.Domain.Entities.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ArtCommission.Application.Dispute.SubmissionDispute.Queries;

public record GetSubmissionDisputeByIdQuery(Guid Id) : IRequest<SubmissionDisputeDto?>;

public class GetSubmissionDisputeByIdQueryHandler
    : IRequestHandler<GetSubmissionDisputeByIdQuery, SubmissionDisputeDto?>
{
    private readonly IApplicationDbContext _db;

    public GetSubmissionDisputeByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SubmissionDisputeDto?> Handle(
        GetSubmissionDisputeByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dispute = await _db.SubmissionDisputes
            .AsNoTracking()
            .Include(d => d.Event)
            .Include(d => d.Submission)
                .ThenInclude(s => s.Artwork)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);

        if (dispute == null)
        {
            return null;
        }

        var user = await _db.Set<ApplicationUser>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == dispute.RaisedById, cancellationToken);

        var evidenceList = ParseEvidenceUrls(dispute.EvidenceUrls);

        return new SubmissionDisputeDto
        {
            Id = dispute.Id,
            EventId = dispute.EventId,
            EventTitle = dispute.Event?.Title,
            SubmissionId = dispute.SubmissionId,
            SubmissionTitle = dispute.Submission?.Title,
            ArtworkImageUrl = dispute.Submission?.Artwork?.ImageUrl ?? dispute.Submission?.Artwork?.ThumbnailUrl,
            RaisedById = dispute.RaisedById,
            RaisedByName = user?.FullName ?? user?.UserName ?? "Unknown",
            Priority = dispute.Priority,
            Reason = dispute.Reason,
            EvidenceUrls = dispute.EvidenceUrls,
            EvidenceUrlList = evidenceList,
            Status = dispute.Status,
            Resolution = dispute.Resolution,
            Note = dispute.Note,
            ResolvedAt = dispute.ResolvedAt,
            CreatedAt = dispute.CreatedAt
        };
    }

    private static List<string>? ParseEvidenceUrls(string? evidenceUrls)
    {
        if (string.IsNullOrWhiteSpace(evidenceUrls)) return null;

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(evidenceUrls);
            if (parsed != null && parsed.Count > 0) return parsed;
        }
        catch
        {
            var split = evidenceUrls.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (split.Length > 0) return split.ToList();
        }

        return null;
    }
}
