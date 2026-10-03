using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auth.Commands.RevokeSession;

public record RevokeSessionCommand(Guid UserId, Guid SessionId) : IRequest<(bool Success, string[] Errors)>;

public class RevokeSessionCommandHandler : IRequestHandler<RevokeSessionCommand, (bool Success, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public RevokeSessionCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, string[] Errors)> Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _db.RefreshTokens.FirstOrDefaultAsync(
            rt => rt.Id == request.SessionId && rt.UserId == request.UserId, cancellationToken);

        if (session is null)
        {
            return (false, new[] { "Session not found." });
        }

        session.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return (true, Array.Empty<string>());
    }
}
