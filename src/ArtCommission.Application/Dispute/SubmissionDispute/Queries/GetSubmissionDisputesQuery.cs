using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Dispute.SubmissionDispute.DTOs;
using ArtCommission.Domain.Entities.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ArtCommission.Application.Dispute.SubmissionDispute.Queries;

public record GetSubmissionDisputesQuery(
    Guid? EventId = null,
    Guid? SubmissionId = null,
    Guid? RaisedById = null,
    string? Status = null,
    string? Category = null,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<(List<SubmissionDisputeDto> Items, int TotalCount)>;

public class GetSubmissionDisputesQueryHandler
    : IRequestHandler<GetSubmissionDisputesQuery, (List<SubmissionDisputeDto> Items, int TotalCount)>
{
    private readonly IApplicationDbContext _db;

    public GetSubmissionDisputesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(List<SubmissionDisputeDto> Items, int TotalCount)> Handle(
        GetSubmissionDisputesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _db.SubmissionDisputes
            .AsNoTracking()
            .Include(d => d.Event)
            .Include(d => d.Submission)
                .ThenInclude(s => s.Artwork)
            .AsQueryable();

        if (request.EventId.HasValue)
        {
            query = query.Where(d => d.EventId == request.EventId.Value);
        }

        if (request.SubmissionId.HasValue)
        {
            query = query.Where(d => d.SubmissionId == request.SubmissionId.Value);
        }

        if (request.RaisedById.HasValue)
        {
            query = query.Where(d => d.RaisedById == request.RaisedById.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(d => d.Status == request.Status.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            query = query.Where(d => d.Category == request.Category.Trim());
        }

        int totalCount = await query.CountAsync(cancellationToken);

        int pageNumber = request.PageNumber > 0 ? request.PageNumber : 1;
        int pageSize = request.PageSize > 0 ? Math.Min(request.PageSize, 100) : 20;

        var rawDisputes = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (rawDisputes.Count == 0)
        {
            return (new List<SubmissionDisputeDto>(), totalCount);
        }

        var userIds = rawDisputes.Select(d => d.RaisedById).Distinct().ToList();
        var users = await _db.Set<ApplicationUser>()
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u, cancellationToken);

        var items = rawDisputes.Select(d =>
        {
            users.TryGetValue(d.RaisedById, out var user);

            var evidenceList = ParseEvidenceUrls(d.EvidenceUrls);

            return new SubmissionDisputeDto
            {
                Id = d.Id,
                EventId = d.EventId,
                EventTitle = d.Event?.Title,
                SubmissionId = d.SubmissionId,
                SubmissionTitle = d.Submission?.Title,
                ArtworkImageUrl = d.Submission?.Artwork?.ImageUrl ?? d.Submission?.Artwork?.ThumbnailUrl,
                RaisedById = d.RaisedById,
                RaisedByName = user?.FullName ?? user?.UserName ?? "Unknown",
                Category = d.Category,
                Priority = d.Priority,
                Reason = d.Reason,
                EvidenceUrls = d.EvidenceUrls,
                EvidenceUrlList = evidenceList,
                Status = d.Status,
                Resolution = d.Resolution,
                Note = d.Note,
                ResolvedAt = d.ResolvedAt,
                CreatedAt = d.CreatedAt
            };
        }).ToList();

        return (items, totalCount);
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
