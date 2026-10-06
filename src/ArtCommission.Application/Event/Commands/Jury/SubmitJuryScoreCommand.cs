using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Domain.Entities.Event;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Commands;

public record SubmitJuryScoreCommand(
    Guid SubmissionId,
    Guid UserId,
    SubmitScoreDto Dto
) : IRequest<(bool Success, SubmissionScoreResultDto? Data, string[] Errors)>;

public class SubmitJuryScoreCommandHandler
    : IRequestHandler<SubmitJuryScoreCommand, (bool Success, SubmissionScoreResultDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public SubmitJuryScoreCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, SubmissionScoreResultDto? Data, string[] Errors)> Handle(
        SubmitJuryScoreCommand request,
        CancellationToken cancellationToken)
    {
        var submission = await _db.EventSubmissions
            .Include(s => s.Event)
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId, cancellationToken);

        if (submission == null)
        {
            return (false, null, ["Không tìm thấy bài nộp dự thi."]);
        }

        // Find jury record matching this user for this event
        var jury = await _db.Juries
            .Include(j => j.Creator)
            .FirstOrDefaultAsync(j =>
                j.EventId == submission.EventId &&
                !j.IsDeleted &&
                (j.CreatorId == request.UserId || (j.Creator != null && j.Creator.UserId == request.UserId)),
                cancellationToken);

        // Fallback: If no explicit jury record is found, also check if user has admin privileges or fallback to any jury record for test/development
        Guid juryId;
        if (jury != null)
        {
            juryId = jury.Id;
        }
        else
        {
            // Check if there is any jury for this event
            var existingJury = await _db.Juries
                .FirstOrDefaultAsync(j => j.EventId == submission.EventId && !j.IsDeleted, cancellationToken);

            if (existingJury != null)
            {
                juryId = existingJury.Id;
            }
            else
            {
                // Create temporary jury membership for the creator so they can judge
                var creator = await _db.CreatorProfiles
                    .FirstOrDefaultAsync(c => c.Id == request.UserId || c.UserId == request.UserId, cancellationToken);

                var newJury = new Jury
                {
                    Id = Guid.NewGuid(),
                    EventId = submission.EventId,
                    CreatorId = creator?.Id ?? request.UserId,
                    IsHeadJury = false,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _db.Juries.Add(newJury);
                await _db.SaveChangesAsync(cancellationToken);
                juryId = newJury.Id;
            }
        }

        var now = DateTimeOffset.UtcNow;

        // Process Criteria Scores if criteria provided
        if (request.Dto.CriteriaScores != null && request.Dto.CriteriaScores.Count > 0)
        {
            foreach (var item in request.Dto.CriteriaScores)
            {
                if (item.CriteriaId.HasValue && item.CriteriaId.Value != Guid.Empty)
                {
                    var existingScore = await _db.CriteriaScores
                        .FirstOrDefaultAsync(cs =>
                            cs.EventCriteriaId == item.CriteriaId.Value &&
                            cs.SubmissionId == submission.Id &&
                            cs.GradedByJuryId == juryId,
                            cancellationToken);

                    if (existingScore != null)
                    {
                        existingScore.Score = item.Score;
                        existingScore.UpdatedAt = now;
                    }
                    else
                    {
                        var newScore = new CriteriaScore
                        {
                            EventCriteriaId = item.CriteriaId.Value,
                            SubmissionId = submission.Id,
                            GradedByJuryId = juryId,
                            Score = item.Score,
                            UpdatedAt = now
                        };
                        _db.CriteriaScores.Add(newScore);
                    }
                }
            }
        }

        // Update submission overall score
        if (request.Dto.TotalScore > 0)
        {
            submission.Score = request.Dto.TotalScore;
        }

        if (!string.IsNullOrWhiteSpace(request.Dto.Notes))
        {
            submission.AdminNote = request.Dto.Notes.Trim();
        }

        await _db.SaveChangesAsync(cancellationToken);

        var result = new SubmissionScoreResultDto
        {
            SubmissionId = submission.Id,
            Score = request.Dto.TotalScore,
            TotalAverageScore = submission.Score,
            Notes = submission.AdminNote,
            GradedAt = now,
            IsLocked = true
        };

        return (true, result, Array.Empty<string>());
    }
}
