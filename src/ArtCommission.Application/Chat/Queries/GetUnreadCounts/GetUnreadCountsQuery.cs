using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Chat.Queries.GetUnreadCounts;

public sealed record WorkroomUnreadCountDto(
    Guid RoomId,
    Guid CommissionId,
    int UnreadCount,
    DateTimeOffset? LastMessageAt);

public record GetUnreadCountsQuery(Guid UserId) : IRequest<IReadOnlyList<WorkroomUnreadCountDto>>;

public class GetUnreadCountsQueryHandler
    : IRequestHandler<GetUnreadCountsQuery, IReadOnlyList<WorkroomUnreadCountDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<GetUnreadCountsQueryHandler> _logger;

    public GetUnreadCountsQueryHandler(
        IApplicationDbContext db,
        ILogger<GetUnreadCountsQueryHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<WorkroomUnreadCountDto>> Handle(
        GetUnreadCountsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return Array.Empty<WorkroomUnreadCountDto>();
        }

        var creatorProfileId = await _db.CreatorProfiles
            .AsNoTracking()
            .Where(p => p.UserId == request.UserId && !p.IsDeleted)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var activeRooms = await _db.Commissions
            .AsNoTracking()
            .Where(c =>
                !c.IsDeleted &&
                c.Status != CommissionStatus.Completed &&
                c.Status != CommissionStatus.Cancelled &&
                (c.ClientId == request.UserId ||
                 (creatorProfileId.HasValue && c.CreatorId == creatorProfileId.Value) ||
                 c.CreatorId == request.UserId))
            .Join(
                _db.ChatRooms.AsNoTracking().Where(r => !r.IsDeleted && !r.IsLocked && r.CommissionId != null),
                c => c.Id,
                r => r.CommissionId!.Value,
                (c, r) => new
                {
                    RoomId = r.Id,
                    CommissionId = c.Id,
                    r.LastMessageAt
                })
            .ToListAsync(cancellationToken);

        if (activeRooms.Count == 0)
        {
            return Array.Empty<WorkroomUnreadCountDto>();
        }

        var roomIds = activeRooms.Select(x => x.RoomId).Distinct().ToList();

        var memberLastReads = await _db.ChatRoomMembers
            .AsNoTracking()
            .Where(m => roomIds.Contains(m.RoomId) && m.UserId == request.UserId && !m.HasLeft && !m.IsDeleted)
            .Select(m => new { m.RoomId, m.LastReadAt })
            .ToListAsync(cancellationToken);

        var lastReadByRoom = memberLastReads
            .GroupBy(x => x.RoomId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.LastReadAt));

        var incomingMessages = await _db.Messages
            .AsNoTracking()
            .Where(msg => msg.RoomId.HasValue && roomIds.Contains(msg.RoomId.Value) && !msg.IsDeleted && msg.SenderId != request.UserId)
            .Select(msg => new { RoomId = msg.RoomId!.Value, msg.SentAt })
            .ToListAsync(cancellationToken);

        var unreadByRoom = new Dictionary<Guid, int>();

        foreach (var msg in incomingMessages)
        {
            var lastRead = lastReadByRoom.GetValueOrDefault(msg.RoomId);
            if (!lastRead.HasValue || msg.SentAt > lastRead.Value)
            {
                unreadByRoom[msg.RoomId] = unreadByRoom.GetValueOrDefault(msg.RoomId) + 1;
            }
        }

        var result = activeRooms
            .GroupBy(x => x.CommissionId)
            .Select(g => new WorkroomUnreadCountDto(
                g.OrderByDescending(x => x.LastMessageAt).First().RoomId,
                g.Key,
                g.Sum(x => unreadByRoom.GetValueOrDefault(x.RoomId)),
                g.Max(x => x.LastMessageAt)))
            .ToList();

        _logger.LogInformation(
            "GetUnreadCounts user={UserId} creatorProfile={CreatorProfileId} rooms={RoomCount} unreadRooms={UnreadRooms}",
            request.UserId,
            creatorProfileId,
            result.Count,
            string.Join(",", result.Where(r => r.UnreadCount > 0).Select(r => $"{r.CommissionId}:{r.UnreadCount}")));

        return result;
    }
}
