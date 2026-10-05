using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.Validators;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Commands;

public record DeleteEventCommand(
    Guid EventId,
    Guid AdminId
) : IRequest<(bool Success, string[] Errors)>;

public class DeleteEventCommandHandler
    : IRequestHandler<DeleteEventCommand, (bool Success, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public DeleteEventCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, string[] Errors)> Handle(
        DeleteEventCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new DeleteEventCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var platformEvent = await _db.PlatformEvents
            .FirstOrDefaultAsync(e => e.Id == request.EventId && !e.IsDeleted, cancellationToken);

        if (platformEvent == null)
        {
            return (false, ["Platform event not found."]);
        }

        var now = DateTimeOffset.UtcNow;

        var deleteErrors = DeleteEventCommandValidator.ValidateCanDelete(platformEvent, now);
        if (deleteErrors.Count > 0)
        {
            return (false, deleteErrors.ToArray());
        }

        if (platformEvent.Status == ArtCommission.Domain.Enums.EventStatus.Draft &&
            platformEvent.SubmissionStartAt > now)
        {
            platformEvent.IsDeleted = true;
            platformEvent.UpdatedAt = now;

            await _db.SaveChangesAsync(cancellationToken);

            return (true, Array.Empty<string>());
        }

        return (false, ["The event cannot be deleted because it does not meet the deletion criteria."]);
    }
}
