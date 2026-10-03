using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Application.Event.Validators;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Commands;

public record DeleteVoteCommand(
    Guid SubmissionId,
    Guid VoterId
) : IRequest<(bool Success, EventVoteResultDto? Data, string[] Errors)>;

public class DeleteVoteCommandHandler
    : IRequestHandler<DeleteVoteCommand, (bool Success, EventVoteResultDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public DeleteVoteCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, EventVoteResultDto? Data, string[] Errors)> Handle(
        DeleteVoteCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new DeleteVoteCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, null, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var vote = await _db.EventVotes
            .Include(v => v.Submission)
                .ThenInclude(s => s!.Event)
            .FirstOrDefaultAsync(v => v.SubmissionId == request.SubmissionId && v.VoterId == request.VoterId, cancellationToken);

        if (vote == null)
        {
            return (false, null, ["Vote not found."]);
        }

        var platformEvent = vote.Submission?.Event;
        var now = DateTimeOffset.UtcNow;

        if (platformEvent != null && now > platformEvent.VotingEndAt)
        {
            return (false, null, ["Cannot withdraw vote after the event voting period has ended."]);
        }

        _db.EventVotes.Remove(vote);

        if (vote.Submission != null)
        {
            vote.Submission.VoteCount = Math.Max(0, vote.Submission.VoteCount - 1);
        }

        await _db.SaveChangesAsync(cancellationToken);

        int remainingVotes = 0;
        if (platformEvent != null)
        {
            var userVoteCountInEvent = await _db.EventVotes
                .CountAsync(v => v.Submission != null && v.Submission.EventId == platformEvent.Id && v.VoterId == request.VoterId, cancellationToken);
            remainingVotes = Math.Max(0, platformEvent.MaxVote - userVoteCountInEvent);
        }

        var result = new EventVoteResultDto
        {
            SubmissionId = request.SubmissionId,
            VoterId = request.VoterId,
            VotedAt = now,
            UpdatedVoteCount = vote.Submission?.VoteCount ?? 0,
            IsVoted = false,
            RemainingVotesInEvent = remainingVotes
        };

        return (true, result, Array.Empty<string>());
    }
}
