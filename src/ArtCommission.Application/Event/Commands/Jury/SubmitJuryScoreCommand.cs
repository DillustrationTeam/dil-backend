using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Application.Event.Validators;
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
        var validator = new SubmitJuryScoreCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, null, validationResult.Errors.Select(e => e.ErrorMessage).Distinct().ToArray());
        }

        if (request.Dto.TotalScore < 0)
        {
            return (false, null, ["Điểm số không được là số âm."]);
        }

        var submission = await _db.EventSubmissions
            .Include(s => s.Event)
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId, cancellationToken);

        if (submission == null)
        {
            return (false, null, ["Không tìm thấy bài nộp dự thi."]);
        }

        if (submission.IsDisqualified)
        {
            return (false, null, ["Bài dự thi này đã bị truất quyền thi đấu do vi phạm quy chế hoặc khiếu nại."]);
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

        if (now < platformEvent.JudgingStartAt)
        {
            return (false, null, ["Thời gian chấm điểm của sự kiện chưa bắt đầu."]);
        }

        if (now > platformEvent.JudgingEndAt)
        {
            return (false, null, ["Thời gian chấm điểm của sự kiện đã kết thúc, điểm số đã được khóa."]);
        }

        // Check if this jury member already graded this submission (scores are locked once submitted)
        var alreadyScored = await _db.CriteriaScores
            .AnyAsync(cs => cs.SubmissionId == submission.Id && cs.GradedByJuryId == juryId, cancellationToken);

        if (alreadyScored)
        {
            return (false, null, ["Điểm số của bạn cho bài dự thi này đã được gửi và khóa, không thể chỉnh sửa."]);
        }

        // Process Criteria Scores if criteria provided
        if (request.Dto.CriteriaScores != null && request.Dto.CriteriaScores.Count > 0)
        {
            if (request.Dto.CriteriaScores.Any(c => c.Score < 0))
            {
                return (false, null, ["Điểm số không được là số âm."]);
            }

            if (request.Dto.CriteriaScores.Any(c => !c.CriteriaId.HasValue || c.CriteriaId.Value == Guid.Empty))
            {
                return (false, null, ["Mã tiêu chí chấm điểm (CriteriaId) không hợp lệ hoặc thiếu thông tin."]);
            }

            var duplicate = request.Dto.CriteriaScores
                .GroupBy(c => c.CriteriaId!.Value)
                .Any(g => g.Count() > 1);
            if (duplicate)
            {
                return (false, null, ["Không được chấm điểm trùng lặp cho cùng một tiêu chí."]);
            }

            var validCriteria = await _db.EventCriteria
                .Where(c => c.EventId == submission.EventId && !c.IsDeleted)
                .ToDictionaryAsync(c => c.Id, cancellationToken);

            if (validCriteria.Count == 0)
            {
                var defaultCriteria = new List<EventCriteria>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        EventId = submission.EventId,
                        Name = "Sáng tạo và ý tưởng",
                        Description = "Độ độc đáo, tính nguyên bản và thông điệp truyền tải của tác phẩm",
                        MaxScore = 40.0m,
                        Weight = 0.40m,
                        DisplayOrder = 1,
                        CreatedAt = now
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        EventId = submission.EventId,
                        Name = "Kỹ thuật thể hiện",
                        Description = "Bố cục, xử lý ánh sáng, màu sắc, chi tiết nét vẽ và độ hoàn thiện",
                        MaxScore = 35.0m,
                        Weight = 0.35m,
                        DisplayOrder = 2,
                        CreatedAt = now
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        EventId = submission.EventId,
                        Name = "Bám sát chủ đề",
                        Description = "Sự phù hợp và thể hiện đúng tinh thần thể lệ sự kiện đặt ra",
                        MaxScore = 25.0m,
                        Weight = 0.25m,
                        DisplayOrder = 3,
                        CreatedAt = now
                    }
                };
                _db.EventCriteria.AddRange(defaultCriteria);
                await _db.SaveChangesAsync(cancellationToken);
                validCriteria = defaultCriteria.ToDictionary(c => c.Id);
            }

            foreach (var item in request.Dto.CriteriaScores)
            {
                var critId = item.CriteriaId!.Value;

                if (!validCriteria.TryGetValue(critId, out var criterion))
                {
                    return (false, null, ["Tiêu chí chấm điểm không thuộc sự kiện của bài dự thi này."]);
                }

                if (item.Score < 0)
                {
                    return (false, null, [$"Điểm chấm cho tiêu chí '{criterion.Name}' không được là số âm."]);
                }

                if (item.Score > criterion.MaxScore)
                {
                    return (false, null, [$"Điểm chấm cho tiêu chí '{criterion.Name}' không được vượt quá điểm tối đa ({criterion.MaxScore})."]);
                }

                var scoreValue = item.Score;

                var existingScore = await _db.CriteriaScores
                    .FirstOrDefaultAsync(cs =>
                        cs.EventCriteriaId == critId &&
                        cs.SubmissionId == submission.Id &&
                        cs.GradedByJuryId == juryId,
                        cancellationToken);

                if (existingScore != null)
                {
                    existingScore.Score = scoreValue;
                    existingScore.UpdatedAt = now;
                }
                else
                {
                    var newScore = new CriteriaScore
                    {
                        EventCriteriaId = critId,
                        SubmissionId = submission.Id,
                        GradedByJuryId = juryId,
                        Score = scoreValue,
                        UpdatedAt = now
                    };
                    _db.CriteriaScores.Add(newScore);
                }
            }
        }

        // Calculate and update submission overall score
        var criteriaSum = request.Dto.CriteriaScores?.Sum(c => c.Score) ?? 0;
        var currentJuryScore = request.Dto.TotalScore > 0 
            ? request.Dto.TotalScore 
            : (criteriaSum > 0 ? criteriaSum : request.Dto.TotalScore);

        // Save criteria scores first to database so they can be accurately aggregated
        await _db.SaveChangesAsync(cancellationToken);

        // Calculate average score across all juries who have graded this submission
        var juryScores = await _db.CriteriaScores
            .Where(cs => cs.SubmissionId == submission.Id)
            .GroupBy(cs => cs.GradedByJuryId)
            .Select(g => g.Sum(cs => cs.Score))
            .ToListAsync(cancellationToken);

        var hasCriteriaScores = request.Dto.CriteriaScores != null && request.Dto.CriteriaScores.Count > 0;
        if (!hasCriteriaScores && currentJuryScore >= 0)
        {
            juryScores.Add(currentJuryScore);
        }

        if (juryScores.Count > 0)
        {
            submission.Score = Math.Round(juryScores.Average(), 2, MidpointRounding.AwayFromZero);
        }
        else if (currentJuryScore >= 0)
        {
            submission.Score = currentJuryScore;
        }

        if (!string.IsNullOrWhiteSpace(request.Dto.Notes))
        {
            submission.AdminNote = request.Dto.Notes.Trim();
        }

        await _db.SaveChangesAsync(cancellationToken);

        var result = new SubmissionScoreResultDto
        {
            SubmissionId = submission.Id,
            Score = currentJuryScore,
            TotalAverageScore = submission.Score,
            Notes = submission.AdminNote,
            GradedAt = now,
            IsLocked = true
        };

        return (true, result, Array.Empty<string>());
    }
}
