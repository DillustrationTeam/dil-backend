using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Queries;

public record GetMyVotesQuery(
    Guid VoterId,
    Guid? EventId = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<(IReadOnlyList<EventVoteDto> Items, int TotalCount)>;

public class GetMyVotesQueryHandler
    : IRequestHandler<GetMyVotesQuery, (IReadOnlyList<EventVoteDto> Items, int TotalCount)>
{
    private readonly IApplicationDbContext _db;

    public GetMyVotesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<EventVoteDto> Items, int TotalCount)> Handle(
        GetMyVotesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _db.EventVotes
            .AsNoTracking()
            .Include(v => v.Voter)
            .Include(v => v.Submission)
                .ThenInclude(s => s!.Event)
            .Include(v => v.Submission)
                .ThenInclude(s => s!.Artwork)
            .Where(v => v.VoterId == request.VoterId);

        if (request.EventId.HasValue && request.EventId.Value != Guid.Empty)
        {
            query = query.Where(v => v.Submission != null && v.Submission.EventId == request.EventId.Value);
        }

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
