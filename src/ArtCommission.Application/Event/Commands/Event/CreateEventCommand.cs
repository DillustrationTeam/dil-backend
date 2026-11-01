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
    Guid AdminId,
    bool IsFeatured = false,
    List<EventCriteriaRequestDto>? Criteria = null
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
            IsFeatured = request.IsFeatured,
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

        var criteriaToSave = new List<EventCriteria>();

        if (request.Criteria != null && request.Criteria.Count > 0)
        {
            int order = 1;
            foreach (var c in request.Criteria)
            {
                if (string.IsNullOrWhiteSpace(c.Name)) continue;

                criteriaToSave.Add(new EventCriteria
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
                });
            }
        }

        if (criteriaToSave.Count == 0)
        {
            criteriaToSave.AddRange(new[]
            {
                new EventCriteria
                {
                    Id = Guid.NewGuid(),
                    EventId = platformEvent.Id,
                    Name = "Sáng tạo và ý tưởng",
                    Description = "Độ độc đáo, tính nguyên bản và thông điệp truyền tải của tác phẩm",
                    MaxScore = 40.0m,
                    Weight = 0.40m,
                    DisplayOrder = 1,
                    CreatedAt = now,
                    IsDeleted = false
                },
                new EventCriteria
                {
                    Id = Guid.NewGuid(),
                    EventId = platformEvent.Id,
                    Name = "Kỹ thuật thể hiện",
                    Description = "Bố cục, xử lý ánh sáng, màu sắc, chi tiết nét vẽ và độ hoàn thiện",
                    MaxScore = 35.0m,
                    Weight = 0.35m,
                    DisplayOrder = 2,
                    CreatedAt = now,
                    IsDeleted = false
                },
                new EventCriteria
                {
                    Id = Guid.NewGuid(),
                    EventId = platformEvent.Id,
                    Name = "Bám sát chủ đề",
                    Description = "Sự phù hợp và thể hiện đúng tinh thần thể lệ sự kiện đặt ra",
                    MaxScore = 25.0m,
                    Weight = 0.25m,
                    DisplayOrder = 3,
                    CreatedAt = now,
                    IsDeleted = false
                }
            });
        }

        _db.EventCriteria.AddRange(criteriaToSave);

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
            CreatedByAdminName = admin.FullName,
            CreatedByAdminAvatarUrl = null,
            SubmissionCount = 0,
            TotalVoteCount = 0,
            CreatedAt = platformEvent.CreatedAt,
            UpdatedAt = platformEvent.UpdatedAt,
            IsOpenForSubmission = platformEvent.Status == EventStatus.Open && platformEvent.SubmissionStartAt <= now && platformEvent.SubmissionEndAt >= now,
            Criteria = criteriaToSave.Select(c => new EventCriteriaDto
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
