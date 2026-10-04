using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Queries;

public record GetVotesBySubmissionQuery(
    Guid SubmissionId,
    int Page = 1,
    int PageSize = 20
) : IRequest<(IReadOnlyList<EventVoteDto> Items, int TotalCount)>;

public class GetVotesBySubmissionQueryHandler
    : IRequestHandler<GetVotesBySubmissionQuery, (IReadOnlyList<EventVoteDto> Items, int TotalCount)>
{
    private readonly IApplicationDbContext _db;

    public GetVotesBySubmissionQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<EventVoteDto> Items, int TotalCount)> Handle(
        GetVotesBySubmissionQuery request,
        CancellationToken cancellationToken)
    {
        var query = _db.EventVotes
            .AsNoTracking()
            .Include(v => v.Voter)
            .Include(v => v.Submission)
                .ThenInclude(s => s!.Event)
            .Include(v => v.Submission)
                .ThenInclude(s => s!.Artwork)
            .Where(v => v.SubmissionId == request.SubmissionId);

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(v => v.VotedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new EventVoteDto
            {
                SubmissionId = v.SubmissionId,
                SubmissionTitle = v.Submission != null ? v.Submission.Title : null,
                EventId = v.Submission != null ? v.Submission.EventId : null,
                EventTitle = v.Submission != null && v.Submission.Event != null ? v.Submission.Event.Title : null,
                ArtworkId = v.Submission != null ? v.Submission.ArtworkId : null,
                ArtworkImageUrl = v.Submission != null && v.Submission.Artwork != null ? (v.Submission.Artwork.ThumbnailUrl ?? v.Submission.Artwork.ImageUrl) : null,
                VoterId = v.VoterId,
                VoterName = v.Voter != null ? v.Voter.FullName : null,
                VoterUsername = v.Voter != null ? v.Voter.UserName : null,
                VotedAt = v.VotedAt
            })
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
