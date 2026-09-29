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

        platformEvent.IsDeleted = true;
        platformEvent.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return (true, Array.Empty<string>());
    }
}
