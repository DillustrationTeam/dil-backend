using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auth.Queries.GetMySessions;

public record GetMySessionsQuery(Guid UserId, Guid? CurrentSessionId) : IRequest<IReadOnlyList<SessionDto>>;

public record SessionDto(Guid Id, string? CreatedByIp, string? UserAgent, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt, bool IsCurrent);

public class GetMySessionsQueryHandler : IRequestHandler<GetMySessionsQuery, IReadOnlyList<SessionDto>>
{
    private readonly IApplicationDbContext _db;

    public GetMySessionsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SessionDto>> Handle(GetMySessionsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        return await _db.RefreshTokens
            .Where(rt => rt.UserId == request.UserId && rt.RevokedAt == null && rt.ExpiresAt > now)
            .OrderByDescending(rt => rt.LastUsedAt ?? rt.CreatedAt)
            .Select(rt => new SessionDto(
                rt.Id,
                rt.CreatedByIp,
                rt.UserAgent,
                rt.CreatedAt,
                rt.LastUsedAt,
                request.CurrentSessionId != null && rt.Id == request.CurrentSessionId))
            .ToListAsync(cancellationToken);
    }
}
