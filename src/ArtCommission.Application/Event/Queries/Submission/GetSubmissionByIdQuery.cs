using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Queries;

public record GetSubmissionByIdQuery(
    Guid SubmissionId
) : IRequest<EventSubmissionDto?>;

public class GetSubmissionByIdQueryHandler 
    : IRequestHandler<GetSubmissionByIdQuery, EventSubmissionDto?>
{
    private readonly IApplicationDbContext _db;

    public GetSubmissionByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<EventSubmissionDto?> Handle(
        GetSubmissionByIdQuery request,
        CancellationToken cancellationToken)
    {
        return await _db.EventSubmissions
            .AsNoTracking()
            .Where(s => s.Id == request.SubmissionId)
            .Where(s => s.Event == null || !s.Event.IsDeleted)
            .Select(s => new EventSubmissionDto
            {
                Id = s.Id,
                EventId = s.EventId,
                EventTitle = s.Event != null ? s.Event.Title : null,
                SubmitterId = s.SubmitterId,
                SubmitterName = s.Submitter != null ? s.Submitter.FullName : null,
                SubmitterUsername = s.Submitter != null ? s.Submitter.UserName : null,
                SubmitterAvatarUrl = null,
                ArtworkId = s.ArtworkId,
                Title = s.Title,
                Description = s.Description,
                ArtworkTitle = s.Artwork != null ? s.Artwork.Title : null,
                ArtworkImageUrl = s.Artwork != null ? s.Artwork.ImageUrl : null,
                ArtworkThumbnailUrl = s.Artwork != null ? s.Artwork.ThumbnailUrl : null,
                AiScanPassed = s.AiScanPassed,
                VoteCount = s.VoteCount,
                Score = s.Score,
                AdminNote = s.AdminNote,
                SubmittedAt = s.SubmittedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
