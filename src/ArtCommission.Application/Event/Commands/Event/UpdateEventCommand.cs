using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Application.Event.Validators;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Commands;

public record UpdateEventCommand(
    Guid EventId,
    string Title,
    string Description,
    string? BannerUrl,
    string? Rules,
    string? Prize,
    int MaxVote,
    EventStatus Status,
    DateTimeOffset SubmissionStartAt,
    DateTimeOffset SubmissionEndAt,
    DateTimeOffset JudgingStartAt,
    DateTimeOffset JudgingEndAt,
    DateTimeOffset VotingStartAt,
    DateTimeOffset VotingEndAt,
    DateTimeOffset ResultAnnouncementAt,
    Guid AdminId,
    bool IsFeatured = false,
    List<EventCriteriaRequestDto>? Criteria = null
) : IRequest<(bool Success, EventDetailDto? Data, string[] Errors)>;

public class UpdateEventCommandHandler
    : IRequestHandler<UpdateEventCommand, (bool Success, EventDetailDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public UpdateEventCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, EventDetailDto? Data, string[] Errors)> Handle(
        UpdateEventCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new UpdateEventCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, null, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var platformEvent = await _db.PlatformEvents
            .Include(e => e.CreatedByAdmin)
            .Include(e => e.Submissions)
            .Include(e => e.Criteria)
            .FirstOrDefaultAsync(e => e.Id == request.EventId && !e.IsDeleted, cancellationToken);

        if (platformEvent == null)
        {
            return (false, null, ["Platform event not found."]);
        }

        var now = DateTimeOffset.UtcNow;

        var timelineErrors = UpdateEventCommandValidator.ValidateTimelineTransition(platformEvent, request, now);
        if (timelineErrors.Count > 0)
        {
            return (false, null, timelineErrors.ToArray());
        }

        bool isJudgingStarted = platformEvent.JudgingStartAt <= now 
            || platformEvent.Status == EventStatus.Judging 
            || platformEvent.Status == EventStatus.Ended;

        if (request.Criteria != null)
        {
            var activeExistingCriteria = platformEvent.Criteria
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.DisplayOrder)
                .ToList();

            bool isCriteriaModified = request.Criteria.Count != activeExistingCriteria.Count;
            if (!isCriteriaModified)
            {
                for (int i = 0; i < request.Criteria.Count; i++)
                {
                    var req = request.Criteria[i];
                    var exist = activeExistingCriteria[i];
                    if (req.Name.Trim() != exist.Name
                        || req.MaxScore != exist.MaxScore
                        || req.Weight != exist.Weight
                        || (req.Description?.Trim() ?? "") != (exist.Description?.Trim() ?? ""))
                    {
                        isCriteriaModified = true;
                        break;
                    }
                }
            }

            if (isCriteriaModified && isJudgingStarted)
            {
                return (false, null, ["Event scoring criteria cannot be modified once the judging stage has started."]);
            }
        }

        platformEvent.Title = request.Title.Trim();
        platformEvent.Description = request.Description.Trim();
        platformEvent.BannerUrl = string.IsNullOrWhiteSpace(request.BannerUrl) ? null : request.BannerUrl.Trim();
        platformEvent.Rules = string.IsNullOrWhiteSpace(request.Rules) ? null : request.Rules.Trim();
        platformEvent.Prize = string.IsNullOrWhiteSpace(request.Prize) ? null : request.Prize.Trim();
        platformEvent.IsFeatured = request.IsFeatured;
        platformEvent.MaxVote = request.MaxVote > 0 ? request.MaxVote : 1;
        platformEvent.Status = request.Status;
        platformEvent.SubmissionStartAt = request.SubmissionStartAt;
        platformEvent.SubmissionEndAt = request.SubmissionEndAt;
        platformEvent.JudgingStartAt = request.JudgingStartAt;
        platformEvent.JudgingEndAt = request.JudgingEndAt;
        platformEvent.VotingStartAt = request.VotingStartAt;
        platformEvent.VotingEndAt = request.VotingEndAt;
        platformEvent.ResultAnnouncementAt = request.ResultAnnouncementAt;
        platformEvent.UpdatedAt = now;

        if (!isJudgingStarted && request.Criteria != null && request.Criteria.Count > 0)
        {
            var existingCriteriaIds = platformEvent.Criteria.Select(c => c.Id).ToList();
            var hasScores = await _db.CriteriaScores.AnyAsync(
                cs => existingCriteriaIds.Contains(cs.EventCriteriaId),
                cancellationToken);

            if (!hasScores)
            {
                foreach (var oldCrit in platformEvent.Criteria.Where(c => !c.IsDeleted))
                {
                    oldCrit.IsDeleted = true;
                    oldCrit.UpdatedAt = now;
                }

                int order = 1;
                foreach (var c in request.Criteria)
                {
                    if (string.IsNullOrWhiteSpace(c.Name)) continue;

                    var newCrit = new Domain.Entities.Event.EventCriteria
                    {
                        Id = Guid.NewGuid(),
                        EventId = platformEvent.Id,
                        Name = c.Name.Trim(),
                        Description = string.IsNullOrWhiteSpace(c.Description) ? null : c.Description.Trim(),
                        MaxScore = c.MaxScore > 0 ? c.MaxScore : 10.0m,
                        Weight = c.Weight > 0 ? c.Weight : 1.0m,
                        DisplayOrder = c.DisplayOrder > 0 ? c.DisplayOrder : order++,
                        CreatedAt = now,
                        IsDeleted = false
                    };
                    _db.EventCriteria.Add(newCrit);
                    platformEvent.Criteria.Add(newCrit);
                }
            }
        }

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
            CreatedByAdminAvatarUrl = null,
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
                    EventId = c.EventId,
                    Name = c.Name,
                    Description = c.Description,
                    MaxScore = c.MaxScore,
                    Weight = c.Weight,
                    DisplayOrder = c.DisplayOrder
                }).ToList()
        };

        return (true, data, Array.Empty<string>());
    }
}
