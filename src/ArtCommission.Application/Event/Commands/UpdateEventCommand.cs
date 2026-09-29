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
    EventStatus Status,
    DateTimeOffset StartAt,
    DateTimeOffset EndsAt,
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
        platformEvent.Title = request.Title.Trim();
        platformEvent.Description = request.Description.Trim();
        platformEvent.BannerUrl = string.IsNullOrWhiteSpace(request.BannerUrl) ? null : request.BannerUrl.Trim();
        platformEvent.Rules = string.IsNullOrWhiteSpace(request.Rules) ? null : request.Rules.Trim();
        platformEvent.Prize = string.IsNullOrWhiteSpace(request.Prize) ? null : request.Prize.Trim();
        platformEvent.Status = request.Status;
        platformEvent.StartAt = request.StartAt;
        platformEvent.EndsAt = request.EndsAt;
        platformEvent.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        var data = new EventDetailDto(
            platformEvent.Id,
            platformEvent.Title,
            platformEvent.BannerUrl,
            platformEvent.Description,
            platformEvent.Rules,
            platformEvent.Prize,
            platformEvent.Status.ToString(),
            platformEvent.StartAt,
            platformEvent.EndsAt,
            platformEvent.CreatedByAdminId,
            platformEvent.CreatedByAdmin?.FullName,
            null,
            platformEvent.Submissions.Count,
            platformEvent.Submissions.Sum(s => (int?)s.VoteCount) ?? 0,
            platformEvent.CreatedAt,
            platformEvent.UpdatedAt,
            platformEvent.Status == EventStatus.Open && platformEvent.StartAt <= now && platformEvent.EndsAt >= now
        );

        return (true, data, Array.Empty<string>());
    }
}
