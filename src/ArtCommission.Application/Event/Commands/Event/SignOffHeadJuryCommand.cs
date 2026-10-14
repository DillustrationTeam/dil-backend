using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Commands;

public record SignOffHeadJuryCommand(Guid EventId, Guid UserId)
    : IRequest<(bool Success, string[] Errors, DateTimeOffset? SignedOffAt)>;

public class SignOffHeadJuryCommandHandler
    : IRequestHandler<SignOffHeadJuryCommand, (bool Success, string[] Errors, DateTimeOffset? SignedOffAt)>
{
    private readonly IApplicationDbContext _db;

    public SignOffHeadJuryCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, string[] Errors, DateTimeOffset? SignedOffAt)> Handle(
        SignOffHeadJuryCommand request,
        CancellationToken cancellationToken)
    {
        var platformEvent = await _db.PlatformEvents
            .Include(e => e.Juries)
            .FirstOrDefaultAsync(e => e.Id == request.EventId && !e.IsDeleted, cancellationToken);

        if (platformEvent == null)
        {
            return (false, ["Không tìm thấy sự kiện."], null);
        }

        // Validate caller: Must be Head Jury of this event OR Administrator / Event Creator
        var isHeadJury = await _db.Juries
            .Include(j => j.Creator)
            .AnyAsync(j =>
                j.EventId == request.EventId &&
                !j.IsDeleted &&
                j.IsHeadJury &&
                (j.CreatorId == request.UserId || (j.Creator != null && j.Creator.UserId == request.UserId)),
                cancellationToken);

        var isEventAdmin = platformEvent.CreatedByAdminId == request.UserId;
        if (!isHeadJury && !isEventAdmin)
        {
            return (false, ["Bạn không có quyền Trưởng ban giám khảo để ký duyệt kết quả sự kiện này."], null);
        }

        var now = DateTimeOffset.UtcNow;
        platformEvent.IsHeadJurySignedOff = true;
        platformEvent.HeadJurySignedOffAt = now;
        platformEvent.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        return (true, Array.Empty<string>(), now);
    }
}
