using ArtCommission.Application.Common.Interfaces;
using MediatR;

namespace ArtCommission.Application.Auth.Commands.RevokeToken;

public record RevokeTokenCommand(
    string RefreshToken,
    string? ClientIp = null
) : IRequest<bool>;

public class RevokeTokenCommandHandler : IRequestHandler<RevokeTokenCommand, bool>
{
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public RevokeTokenCommandHandler(IJwtTokenGenerator jwtTokenGenerator)
    {
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public Task<bool> Handle(RevokeTokenCommand request, CancellationToken cancellationToken)
    {
        return _jwtTokenGenerator.RevokeTokenAsync(request.RefreshToken, request.ClientIp, cancellationToken);
    }
}
