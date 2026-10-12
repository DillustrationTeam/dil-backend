using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.Validators;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Commands;

public record UpdateJuryRoleCommand(
    Guid JuryId,
    Guid AdminId,
    bool IsHeadJury
) : IRequest<(bool Success, string[] Errors)>;

public class UpdateJuryRoleCommandHandler
    : IRequestHandler<UpdateJuryRoleCommand, (bool Success, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public UpdateJuryRoleCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, string[] Errors)> Handle(
        UpdateJuryRoleCommand request,
        CancellationToken cancellationToken)
    {
        var jury = await _db.Juries
            .FirstOrDefaultAsync(j => j.Id == request.JuryId && !j.IsDeleted, cancellationToken);

        if (jury == null)
        {
            return (false, ["Không tìm thấy thành viên ban giám khảo."]);
        }

        var platformEvent = await _db.PlatformEvents
            .FirstOrDefaultAsync(e => e.Id == jury.EventId && !e.IsDeleted, cancellationToken);

        if (platformEvent == null)
        {
            return (false, ["Sự kiện không tồn tại hoặc đã bị xóa."]);
        }

        var now = DateTimeOffset.UtcNow;
        var timelineErrors = AddJuryCommandValidator.ValidateCanAddJury(platformEvent, now);
        if (timelineErrors.Count > 0)
        {
            return (false, timelineErrors.ToArray());
        }

        if (request.IsHeadJury)
        {
            // If promoting to Head Jury, demote existing Head Jury if any
            var existingHeadJuries = await _db.Juries
                .Where(j => j.EventId == jury.EventId && j.Id != jury.Id && j.IsHeadJury && !j.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var existing in existingHeadJuries)
            {
                existing.IsHeadJury = false;
            }
        }

        jury.IsHeadJury = request.IsHeadJury;

        await _db.SaveChangesAsync(cancellationToken);

        return (true, Array.Empty<string>());
    }
}
