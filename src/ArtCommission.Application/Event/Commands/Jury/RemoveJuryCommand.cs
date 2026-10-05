using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.Validators;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Commands;

public record RemoveJuryCommand(
    Guid? EventId,
    Guid? CreatorId,
    Guid AdminId,
    Guid? JuryId = null
) : IRequest<(bool Success, string[] Errors)>
{
    public static RemoveJuryCommand ByJuryId(Guid juryId, Guid adminId) =>
        new(null, null, adminId, juryId);

    public static RemoveJuryCommand ByEventAndCreator(Guid eventId, Guid creatorId, Guid adminId) =>
        new(eventId, creatorId, adminId, null);
}

public class RemoveJuryCommandHandler
    : IRequestHandler<RemoveJuryCommand, (bool Success, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public RemoveJuryCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, string[] Errors)> Handle(
        RemoveJuryCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new RemoveJuryCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var jury = await _db.Juries
            .Include(j => j.Event)
            .FirstOrDefaultAsync(j =>
                ((request.JuryId.HasValue && j.Id == request.JuryId.Value) ||
                 (request.EventId.HasValue && request.CreatorId.HasValue &&
                  j.EventId == request.EventId.Value &&
                  (j.CreatorId == request.CreatorId.Value || j.Id == request.CreatorId.Value))) &&
                !j.IsDeleted,
                cancellationToken);

        if (jury == null)
        {
            return (false, ["Jury member not found for this event."]);
        }

        var platformEvent = jury.Event ?? await _db.PlatformEvents
            .FirstOrDefaultAsync(e => e.Id == jury.EventId && !e.IsDeleted, cancellationToken);

        if (platformEvent != null)
        {
            var now = DateTimeOffset.UtcNow;
            var timelineErrors = RemoveJuryCommandValidator.ValidateCanRemoveJury(platformEvent, now);
            if (timelineErrors.Count > 0)
            {
                return (false, timelineErrors.ToArray());
            }
        }

        jury.IsDeleted = true;
        jury.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return (true, Array.Empty<string>());
    }
}
