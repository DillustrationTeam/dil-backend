using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.CreatorApplication.DTOs;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.CreatorApplication.Queries;

public record GetCreatorApplicationsQuery(
    ApplicationStatus? Status = null,
    int Page = 1,
    int PageSize = 10
) : IRequest<(List<CreatorApplicationResponseDto> Items, int TotalCount, int PendingCount)>;

public class GetCreatorApplicationsQueryHandler 
    : IRequestHandler<GetCreatorApplicationsQuery, (List<CreatorApplicationResponseDto> Items, int TotalCount, int PendingCount)>
{
    private const int MaxPageSize = 50;

    private readonly IApplicationDbContext _db;

    public GetCreatorApplicationsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(List<CreatorApplicationResponseDto> Items, int TotalCount, int PendingCount)> Handle(
        GetCreatorApplicationsQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 10 : Math.Min(request.PageSize, MaxPageSize);

        var pendingCount = await _db.CreatorApplications
            .CountAsync(c => c.Status == ApplicationStatus.Pending && !c.IsDeleted, cancellationToken);

        var query = _db.CreatorApplications
            .AsNoTracking()
            .Include(c => c.Applicant)
            .Include(c => c.ReviewedByMod)
            .Where(c => !c.IsDeleted);

        if (request.Status.HasValue)
        {
            query = query.Where(c => c.Status == request.Status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.SubmittedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CreatorApplicationResponseDto
            {
                Id = c.Id,
                ApplicantName = c.Applicant != null ? c.Applicant.FullName : string.Empty,
                ApplicantUsername = c.Applicant != null ? (c.Applicant.UserName ?? string.Empty) : string.Empty,
                ApplicantEmail = c.Applicant != null ? (c.Applicant.Email ?? string.Empty) : string.Empty,
                PrimaryStyle = c.PrimaryStyle,
                PortfolioLinks = c.PortfolioLinks,
                SpeedpaintVideoUrls = c.SpeedpaintVideoUrls,
                SocialLinks = c.SocialLinks,
                IdProofUrl = c.IdProofUrl,
                IdProofBackUrl = c.IdProofBackUrl,
                IsNationalIdVerified = c.IsNationalIdVerified,
                Status = c.Status.ToString(), 
                ReviewedByModId = c.ReviewedByModId,
                ReviewedByModName = c.ReviewedByMod != null ? c.ReviewedByMod.FullName : null,
                ReviewNote = c.ReviewNote,
                SubmittedAt = c.SubmittedAt,
                ReviewedAt = c.ReviewedAt
            })
            .ToListAsync(cancellationToken);

        return (items, totalCount, pendingCount);
    }
}