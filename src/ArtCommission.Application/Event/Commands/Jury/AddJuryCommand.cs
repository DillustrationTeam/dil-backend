using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Application.Event.Validators;
using ArtCommission.Domain.Entities.Event;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Commands;

public record AddJuryCommand(
    Guid EventId,
    Guid CreatorId,
    Guid AdminId,
    bool IsHeadJury = false
) : IRequest<(bool Success, JuryDto? Data, string[] Errors)>;

public class AddJuryCommandHandler
    : IRequestHandler<AddJuryCommand, (bool Success, JuryDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public AddJuryCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, JuryDto? Data, string[] Errors)> Handle(
        AddJuryCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new AddJuryCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, null, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var platformEvent = await _db.PlatformEvents
            .FirstOrDefaultAsync(e => e.Id == request.EventId && !e.IsDeleted, cancellationToken);

        if (platformEvent == null)
        {
            return (false, null, ["Event not found or has been deleted."]);
        }

        var now = DateTimeOffset.UtcNow;
        var timelineErrors = AddJuryCommandValidator.ValidateCanAddJury(platformEvent, now);
        if (timelineErrors.Count > 0)
        {
            return (false, null, timelineErrors.ToArray());
        }

        var creator = await _db.CreatorProfiles
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => (c.Id == request.CreatorId || c.UserId == request.CreatorId) && !c.IsDeleted, cancellationToken);

        if (creator == null)
        {
            return (false, null, ["Creator profile not found."]);
        }

        // Condition: Jury invited/added must have role = CREATOR
        var creatorRoleId = await _db.Set<IdentityRole<Guid>>()
            .Where(r => r.Name == UserRoleNames.Creator)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var hasCreatorRole = await _db.Set<IdentityUserRole<Guid>>()
            .AnyAsync(ur => ur.UserId == creator.UserId && ur.RoleId == creatorRoleId, cancellationToken);

        if (!hasCreatorRole)
        {
            return (false, null, ["The invited jury member must have the CREATOR role."]);
        }

        var isAlreadyJury = await _db.Juries
            .AnyAsync(j => j.EventId == request.EventId && j.CreatorId == creator.Id && !j.IsDeleted, cancellationToken);

        if (isAlreadyJury)
        {
            return (false, null, ["This creator is already a jury member for this event."]);
        }

        if (request.IsHeadJury)
        {
            var hasHeadJury = await _db.Juries
                .AnyAsync(j => j.EventId == request.EventId && j.IsHeadJury && !j.IsDeleted, cancellationToken);

            if (hasHeadJury)
            {
                return (false, null, ["This event already has a Head Jury. An event can only have one Head Jury."]);
            }
        }

        var jury = new Jury
        {
            Id = Guid.NewGuid(),
            EventId = request.EventId,
            CreatorId = creator.Id,
            IsHeadJury = request.IsHeadJury,
            CreatedAt = now,
            IsDeleted = false
        };

        _db.Juries.Add(jury);
        await _db.SaveChangesAsync(cancellationToken);

        var data = new JuryDto
        {
            Id = jury.Id,
            EventId = jury.EventId,
            EventTitle = platformEvent.Title,
            CreatorId = creator.Id,
            CreatorName = creator.User?.FullName,
            CreatorDisplayName = creator.DisplayName,
            CreatorAvatarUrl = null,
            CreatorBio = creator.Bio,
            IsHeadJury = jury.IsHeadJury,
            CreatedAt = jury.CreatedAt
        };

        return (true, data, Array.Empty<string>());
    }
}
