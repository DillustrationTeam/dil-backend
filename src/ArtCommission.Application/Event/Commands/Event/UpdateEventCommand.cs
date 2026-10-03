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
    Guid AdminId
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

        platformEvent.Title = request.Title.Trim();
        platformEvent.Description = request.Description.Trim();
        platformEvent.BannerUrl = string.IsNullOrWhiteSpace(request.BannerUrl) ? null : request.BannerUrl.Trim();
        platformEvent.Rules = string.IsNullOrWhiteSpace(request.Rules) ? null : request.Rules.Trim();
        platformEvent.Prize = string.IsNullOrWhiteSpace(request.Prize) ? null : request.Prize.Trim();
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

        await _db.SaveChangesAsync(cancellationToken);

        var data = new EventDetailDto
        {
            Id = platformEvent.Id,
            Title = platformEvent.Title,
            BannerUrl = platformEvent.BannerUrl,
            Description = platformEvent.Description,
            Rules = platformEvent.Rules,
            Prize = platformEvent.Prize,
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
            IsOpenForSubmission = platformEvent.Status == EventStatus.Open && platformEvent.SubmissionStartAt <= now && platformEvent.SubmissionEndAt >= now
        };

        return (true, data, Array.Empty<string>());
    }
}
