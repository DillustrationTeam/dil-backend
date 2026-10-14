using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Application.Event.Validators;
using ArtCommission.Domain.Entities.Event;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Commands;

public record CreateVoteCommand(
    Guid SubmissionId,
    Guid VoterId
) : IRequest<(bool Success, EventVoteResultDto? Data, string[] Errors)>;

public class CreateVoteCommandHandler
    : IRequestHandler<CreateVoteCommand, (bool Success, EventVoteResultDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public CreateVoteCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, EventVoteResultDto? Data, string[] Errors)> Handle(
        CreateVoteCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new CreateVoteCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, null, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var submission = await _db.EventSubmissions
            .Include(s => s.Event)
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId, cancellationToken);

        if (submission == null)
        {
            return (false, null, ["Event submission not found."]);
        }

        var platformEvent = submission.Event;
        if (platformEvent == null || platformEvent.IsDeleted)
        {
            return (false, null, ["Event associated with this submission not found or deleted."]);
        }

        var now = DateTimeOffset.UtcNow;

        if (now < platformEvent.VotingStartAt)
        {
            return (false, null, [$"Voting for this event has not started yet. Voting starts at {platformEvent.VotingStartAt:u}."]);
        }

        if (now > platformEvent.VotingEndAt)
        {
            return (false, null, [$"Voting for this event has already ended at {platformEvent.VotingEndAt:u}."]);
        }

        if (platformEvent.Status == EventStatus.Draft || platformEvent.Status == EventStatus.Ended)
        {
            return (false, null, ["Event is not currently open for voting."]);
        }

        // Rule 0: Creator không được tự vote cho bài nộp của chính mình
        if (submission.SubmitterId == request.VoterId)
        {
            return (false, null, ["Creator (tác giả) không thể tự bình chọn cho bài dự thi của chính mình."]);
        }

        // Rule 1: 1 User chỉ được vote cho 1 bức tranh 1 lần
        var alreadyVoted = await _db.EventVotes
            .AnyAsync(v => v.SubmissionId == request.SubmissionId && v.VoterId == request.VoterId, cancellationToken);

        if (alreadyVoted)
        {
            return (false, null, ["You have already voted for this submission. Each user can only vote for a submission once."]);
        }

        // Rule 2: Tổng số vote của 1 User trong 1 event không được vượt quá MaxVote của event đó
        var userVoteCountInEvent = await _db.EventVotes
            .CountAsync(v => v.Submission != null && v.Submission.EventId == submission.EventId && v.VoterId == request.VoterId, cancellationToken);

        if (userVoteCountInEvent >= platformEvent.MaxVote)
        {
            return (false, null, [$"You have reached the maximum allowed votes ({platformEvent.MaxVote}) for this event."]);
        }

        var vote = new EventVote
        {
            SubmissionId = request.SubmissionId,
            VoterId = request.VoterId,
            VotedAt = now
        };

        _db.EventVotes.Add(vote);

        submission.VoteCount += 1;

        await _db.SaveChangesAsync(cancellationToken);

        var remainingVotes = Math.Max(0, platformEvent.MaxVote - (userVoteCountInEvent + 1));

        var result = new EventVoteResultDto
        {
            SubmissionId = request.SubmissionId,
            VoterId = request.VoterId,
            VotedAt = now,
            UpdatedVoteCount = submission.VoteCount,
            IsVoted = true,
            RemainingVotesInEvent = remainingVotes
        };

        return (true, result, Array.Empty<string>());
    }
}
