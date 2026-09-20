using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.CreatorApplication.DTOs;
using ArtCommission.Domain.Enums;

using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.CreatorApplication.Queries;

public record GetCreatorApplicationByIdQuery(
    Guid ApplicationId
) : IRequest<CreatorApplicationResponseDto?>;

public class GetCreatorApplicationByIdQueryHandler 
    : IRequestHandler<GetCreatorApplicationByIdQuery, CreatorApplicationResponseDto?>
{

    private readonly IApplicationDbContext _db;

    public GetCreatorApplicationByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CreatorApplicationResponseDto?> Handle(
        GetCreatorApplicationByIdQuery request,
        CancellationToken cancellationToken)
    {
        return await _db.CreatorApplications
            .AsNoTracking()
            .Include(c => c.Applicant)
            .Include(c => c.ReviewedByMod)
            .Where(c => c.Id == request.ApplicationId && !c.IsDeleted)
            .Select(c => new CreatorApplicationResponseDto
            {
                Id = c.Id,
                ApplicantName = c.Applicant != null ? c.Applicant.FullName : string.Empty,
                ApplicantEmail = c.Applicant != null ? (c.Applicant.Email ?? string.Empty) : string.Empty,
                PortfolioLinks = c.PortfolioLinks,
                SocialLinks = c.SocialLinks,
                IdProofUrl = c.IdProofUrl,
                Status = c.Status.ToString(),
                ReviewedByModId = c.ReviewedByModId,
                ReviewedByModName = c.ReviewedByMod != null ? c.ReviewedByMod.FullName : null,
                ReviewNote = c.ReviewNote,
                SubmittedAt = c.SubmittedAt,
                ReviewedAt = c.ReviewedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
