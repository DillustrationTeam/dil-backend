using ArtCommission.Application.Common.DTOs;
using ArtCommission.Application.Common.Interfaces;
using MediatR;

namespace ArtCommission.Application.Auth.Commands.RefreshToken;

public record RefreshTokenCommand(
    string RefreshToken,
    string? ClientIp = null
) : IRequest<(bool Success, AuthResponseDto? AuthResponse, string[] Errors)>;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, (bool Success, AuthResponseDto? AuthResponse, string[] Errors)>
{
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public RefreshTokenCommandHandler(IJwtTokenGenerator jwtTokenGenerator)
    {
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public Task<(bool Success, AuthResponseDto? AuthResponse, string[] Errors)> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        return _jwtTokenGenerator.RefreshTokenAsync(request.RefreshToken, request.ClientIp, cancellationToken);
    }
}
