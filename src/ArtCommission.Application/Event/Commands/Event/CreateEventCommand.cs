using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Application.Event.Validators;
using ArtCommission.Domain.Entities.Event;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Commands;

public record CreateEventCommand(
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

public class CreateEventCommandHandler
    : IRequestHandler<CreateEventCommand, (bool Success, EventDetailDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public CreateEventCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, EventDetailDto? Data, string[] Errors)> Handle(
        CreateEventCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new CreateEventCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, null, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var admin = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.AdminId && !u.IsDeleted, cancellationToken);
        if (admin == null)
        {
            return (false, null, ["Admin user not found."]);
        }

        var now = DateTimeOffset.UtcNow;
        var platformEvent = new PlatformEvent
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            BannerUrl = string.IsNullOrWhiteSpace(request.BannerUrl) ? null : request.BannerUrl.Trim(),
            Description = request.Description.Trim(),
            Rules = string.IsNullOrWhiteSpace(request.Rules) ? null : request.Rules.Trim(),
            Prize = string.IsNullOrWhiteSpace(request.Prize) ? null : request.Prize.Trim(),
            MaxVote = request.MaxVote > 0 ? request.MaxVote : 1,
            Status = request.Status,
            SubmissionStartAt = request.SubmissionStartAt,
            SubmissionEndAt = request.SubmissionEndAt,
            JudgingStartAt = request.JudgingStartAt,
            JudgingEndAt = request.JudgingEndAt,
            VotingStartAt = request.VotingStartAt,
            VotingEndAt = request.VotingEndAt,
            ResultAnnouncementAt = request.ResultAnnouncementAt,
            CreatedByAdminId = request.AdminId,
            CreatedAt = now,
            IsDeleted = false
        };

        _db.PlatformEvents.Add(platformEvent);
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
            CreatedByAdminName = admin.FullName,
            CreatedByAdminAvatarUrl = null,
            SubmissionCount = 0,
            TotalVoteCount = 0,
            CreatedAt = platformEvent.CreatedAt,
            UpdatedAt = platformEvent.UpdatedAt,
            IsOpenForSubmission = platformEvent.Status == EventStatus.Open && platformEvent.SubmissionStartAt <= now && platformEvent.SubmissionEndAt >= now
        };

        return (true, data, Array.Empty<string>());
    }
}
