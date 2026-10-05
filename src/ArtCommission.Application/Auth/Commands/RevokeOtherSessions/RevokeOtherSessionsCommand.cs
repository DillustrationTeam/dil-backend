using ArtCommission.Application.Common.Interfaces;
using MediatR;

namespace ArtCommission.Application.Auth.Commands.RevokeOtherSessions;

public record RevokeOtherSessionsCommand(Guid UserId, Guid? CurrentSessionId) : IRequest<int>;

public class RevokeOtherSessionsCommandHandler : IRequestHandler<RevokeOtherSessionsCommand, int>
{
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public RevokeOtherSessionsCommandHandler(IJwtTokenGenerator jwtTokenGenerator)
    {
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public Task<int> Handle(RevokeOtherSessionsCommand request, CancellationToken cancellationToken) =>
        _jwtTokenGenerator.RevokeAllTokensExceptAsync(request.UserId, request.CurrentSessionId, cancellationToken);
}
