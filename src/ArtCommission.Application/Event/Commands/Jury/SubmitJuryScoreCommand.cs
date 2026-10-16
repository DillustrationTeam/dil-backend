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

        var platformEvent = submission.Event;
        if (platformEvent == null || platformEvent.IsDeleted)
        {
            return (false, null, ["Không tìm thấy sự kiện của bài nộp dự thi hoặc sự kiện đã bị xóa."]);
        }

        if (platformEvent.IsHeadJurySignedOff)
        {
            return (false, null, ["Sự kiện đã được Trưởng ban giám khảo ký duyệt kết quả, không thể tiếp tục chấm điểm."]);
        }

        if (platformEvent.Status == Domain.Enums.EventStatus.Draft)
        {
            return (false, null, ["Sự kiện đang ở trạng thái bản nháp (Draft), chưa thể chấm điểm."]);
        }

        if (platformEvent.Status == Domain.Enums.EventStatus.Ended)
        {
            return (false, null, ["Sự kiện đã kết thúc (Ended), không thể chấm điểm."]);
        }

        // Prevent conflict of interest: submitter cannot score their own artwork
        if (submission.SubmitterId == request.UserId)
        {
            return (false, null, ["Giám khảo không được phép tự chấm điểm bài dự thi của chính mình."]);
        }

        // Find jury record matching this user for this event
        var jury = await _db.Juries
            .Include(j => j.Creator)
            .FirstOrDefaultAsync(j =>
                j.EventId == submission.EventId &&
                !j.IsDeleted &&
                (j.CreatorId == request.UserId || (j.Creator != null && j.Creator.UserId == request.UserId)),
                cancellationToken);

        if (jury == null)
        {
            return (false, null, ["Bạn không có quyền chấm điểm vì không được phân công làm Giám khảo (Jury) cho sự kiện này."]);
        }

        Guid juryId = jury.Id;

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
