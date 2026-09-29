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
    EventStatus Status,
    DateTimeOffset StartAt,
    DateTimeOffset EndsAt,
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
            Status = request.Status,
            StartAt = request.StartAt,
            EndsAt = request.EndsAt,
            CreatedByAdminId = request.AdminId,
            CreatedAt = now,
            IsDeleted = false
        };

        _db.PlatformEvents.Add(platformEvent);
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
            admin.FullName,
            null,
            0,
            0,
            platformEvent.CreatedAt,
            platformEvent.UpdatedAt,
            platformEvent.Status == EventStatus.Open && platformEvent.StartAt <= now && platformEvent.EndsAt >= now
        );

        return (true, data, Array.Empty<string>());
    }
}
