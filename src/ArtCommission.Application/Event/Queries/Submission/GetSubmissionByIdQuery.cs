using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Queries;

public record GetSubmissionByIdQuery(
    Guid SubmissionId,
    Guid? CurrentUserId = null,
    bool IsAdmin = false
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
        var submission = await _db.EventSubmissions
            .AsNoTracking()
            .Include(s => s.Event)
            .Include(s => s.Submitter)
            .Include(s => s.Artwork)
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId, cancellationToken);

        if (submission == null || (submission.Event != null && submission.Event.IsDeleted))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var isAnnounced = submission.Event != null &&
                          (submission.Event.Status == Domain.Enums.EventStatus.Ended ||
                           (submission.Event.ResultAnnouncementAt != default && now >= submission.Event.ResultAnnouncementAt));

        var canViewSecretScores = request.IsAdmin;
        if (!canViewSecretScores && request.CurrentUserId.HasValue && request.CurrentUserId.Value != Guid.Empty && submission.EventId != Guid.Empty)
        {
            canViewSecretScores = await _db.Juries
                .Include(j => j.Creator)
                .AnyAsync(j =>
                    j.EventId == submission.EventId &&
                    !j.IsDeleted &&
                    (j.CreatorId == request.CurrentUserId.Value || (j.Creator != null && j.Creator.UserId == request.CurrentUserId.Value)),
                    cancellationToken);
        }

        var canViewScores = isAnnounced || canViewSecretScores;

        return new EventSubmissionDto
        {
            Id = submission.Id,
            EventId = submission.EventId,
            EventTitle = submission.Event != null ? submission.Event.Title : null,
            SubmitterId = submission.SubmitterId,
            SubmitterName = submission.Submitter != null ? submission.Submitter.FullName : null,
            SubmitterUsername = submission.Submitter != null ? submission.Submitter.UserName : null,
            SubmitterAvatarUrl = null,
            ArtworkId = submission.ArtworkId,
            Title = submission.Title,
            Description = submission.Description,
            ArtworkTitle = submission.Artwork != null ? submission.Artwork.Title : null,
            ArtworkImageUrl = submission.Artwork != null ? submission.Artwork.ImageUrl : null,
            ArtworkThumbnailUrl = submission.Artwork != null ? submission.Artwork.ThumbnailUrl : null,
            AiScanPassed = submission.AiScanPassed,
            VoteCount = submission.VoteCount,
            Score = canViewScores ? submission.Score : null,
            AdminNote = canViewScores ? submission.AdminNote : null,
            IsDisqualified = submission.IsDisqualified,
            SubmittedAt = submission.SubmittedAt
        };
    }
}
