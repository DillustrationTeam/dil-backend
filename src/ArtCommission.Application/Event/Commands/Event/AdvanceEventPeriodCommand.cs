using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Commands;

public record AdvanceEventPeriodCommand(
    Guid EventId,
    string? TargetPeriod = null
) : IRequest<(bool Success, EventDetailDto? Data, string[] Errors, string Message)>;

public class AdvanceEventPeriodCommandHandler
    : IRequestHandler<AdvanceEventPeriodCommand, (bool Success, EventDetailDto? Data, string[] Errors, string Message)>
{
    private readonly IApplicationDbContext _db;

    public AdvanceEventPeriodCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, EventDetailDto? Data, string[] Errors, string Message)> Handle(
        AdvanceEventPeriodCommand request,
        CancellationToken cancellationToken)
    {
        var platformEvent = await _db.PlatformEvents
            .Include(e => e.Submissions)
            .Include(e => e.Criteria)
            .Include(e => e.CreatedByAdmin)
            .FirstOrDefaultAsync(e => e.Id == request.EventId && !e.IsDeleted, cancellationToken);

        if (platformEvent == null)
        {
            return (false, null, ["Sự kiện không tồn tại hoặc đã bị xóa."], string.Empty);
        }

        var now = DateTimeOffset.UtcNow;
        var target = request.TargetPeriod?.Trim().ToLowerInvariant();

        string currentPhase;
        if (platformEvent.Status == EventStatus.Ended)
        {
            currentPhase = "Ended";
        }
        else if (now < platformEvent.SubmissionStartAt)
        {
            currentPhase = "Upcoming";
        }
        else if (now <= platformEvent.SubmissionEndAt)
        {
            currentPhase = "Submission";
        }
        else if (now <= platformEvent.JudgingEndAt)
        {
            currentPhase = "Judging";
        }
        else if (now <= platformEvent.VotingEndAt)
        {
            currentPhase = "Voting";
        }
        else
        {
            currentPhase = "Ended";
        }

        string nextPhase = target switch
        {
            "submission" or "open" => "Submission",
            "judging" => "Judging",
            "voting" => "Voting",
            "ended" or "announcement" or "concluded" => "Ended",
            _ => currentPhase switch
            {
                "Upcoming" or "Draft" => "Submission",
                "Submission" => "Judging",
                "Judging" => "Voting",
                "Voting" => "Ended",
                _ => "Submission" // Wrap around from Ended to Submission for continuous testing
            }
        };

        string message;
        switch (nextPhase)
        {
            case "Submission":
                platformEvent.Status = EventStatus.Open;
                platformEvent.SubmissionStartAt = now.AddDays(-1);
                platformEvent.SubmissionEndAt = now.AddDays(7);
                platformEvent.JudgingStartAt = platformEvent.SubmissionEndAt.AddHours(1);
                platformEvent.JudgingEndAt = platformEvent.JudgingStartAt.AddDays(5);
                platformEvent.VotingStartAt = platformEvent.JudgingEndAt.AddHours(1);
                platformEvent.VotingEndAt = platformEvent.VotingStartAt.AddDays(3);
                platformEvent.ResultAnnouncementAt = platformEvent.VotingEndAt.AddHours(1);
                message = "Đã chuyển sự kiện sang giai đoạn Nhận bài thi (Submission).";
                break;

            case "Judging":
                platformEvent.Status = EventStatus.Judging;
                platformEvent.SubmissionStartAt = now.AddDays(-10);
                platformEvent.SubmissionEndAt = now.AddDays(-3);
                platformEvent.JudgingStartAt = now.AddDays(-2);
                platformEvent.JudgingEndAt = now.AddDays(5);
                platformEvent.VotingStartAt = platformEvent.JudgingEndAt.AddHours(1);
                platformEvent.VotingEndAt = platformEvent.VotingStartAt.AddDays(3);
                platformEvent.ResultAnnouncementAt = platformEvent.VotingEndAt.AddHours(1);
                message = "Đã kết thúc nhận bài và chuyển sự kiện sang giai đoạn Ban giám khảo chấm điểm (Judging).";
                break;

            case "Voting":
                platformEvent.Status = EventStatus.Open;
                platformEvent.SubmissionStartAt = now.AddDays(-15);
                platformEvent.SubmissionEndAt = now.AddDays(-8);
                platformEvent.JudgingStartAt = now.AddDays(-7);
                platformEvent.JudgingEndAt = now.AddDays(-2);
                platformEvent.VotingStartAt = now.AddDays(-1);
                platformEvent.VotingEndAt = now.AddDays(5);
                platformEvent.ResultAnnouncementAt = platformEvent.VotingEndAt.AddHours(1);
                message = "Đã kết thúc chấm điểm và chuyển sự kiện sang giai đoạn Bình chọn cộng đồng (Voting).";
                break;

            case "Ended":
            default:
                platformEvent.Status = EventStatus.Ended;
                platformEvent.SubmissionStartAt = now.AddDays(-20);
                platformEvent.SubmissionEndAt = now.AddDays(-13);
                platformEvent.JudgingStartAt = now.AddDays(-12);
                platformEvent.JudgingEndAt = now.AddDays(-7);
                platformEvent.VotingStartAt = now.AddDays(-6);
                platformEvent.VotingEndAt = now.AddDays(-2);
                platformEvent.ResultAnnouncementAt = now.AddDays(-1);
                message = "Đã kết thúc bình chọn và công bố kết quả chung cuộc của sự kiện (Ended).";
                break;
        }

        platformEvent.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        var data = new EventDetailDto
        {
            Id = platformEvent.Id,
            Title = platformEvent.Title,
            BannerUrl = platformEvent.BannerUrl,
            Description = platformEvent.Description,
            Rules = platformEvent.Rules,
            Prize = platformEvent.Prize,
            IsFeatured = platformEvent.IsFeatured,
            MaxVote = platformEvent.MaxVote,
            Status = platformEvent.Status.ToString(),
            SubmissionStartAt = platformEvent.SubmissionStartAt,
            SubmissionEndAt = platformEvent.SubmissionEndAt,
            JudgingStartAt = platformEvent.JudgingStartAt,
            JudgingEndAt = platformEvent.JudgingEndAt,
            VotingStartAt = platformEvent.VotingStartAt,
            VotingEndAt = platformEvent.VotingEndAt,
            ResultAnnouncementAt = platformEvent.ResultAnnouncementAt,
            CreatedByAdminName = platformEvent.CreatedByAdmin?.FullName,
            SubmissionCount = platformEvent.Submissions.Count,
            TotalVoteCount = platformEvent.Submissions.Sum(s => (int?)s.VoteCount) ?? 0,
            CreatedAt = platformEvent.CreatedAt,
            UpdatedAt = platformEvent.UpdatedAt,
            IsOpenForSubmission = platformEvent.Status == EventStatus.Open && platformEvent.SubmissionStartAt <= now && platformEvent.SubmissionEndAt >= now,
            Criteria = platformEvent.Criteria
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new EventCriteriaDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    MaxScore = c.MaxScore,
                    Weight = c.Weight,
                    DisplayOrder = c.DisplayOrder
                }).ToList()
        };

        return (true, data, Array.Empty<string>(), message);
    }
}
