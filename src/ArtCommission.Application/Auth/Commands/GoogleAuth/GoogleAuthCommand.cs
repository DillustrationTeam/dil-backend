using ArtCommission.Application.Common.DTOs;
using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace ArtCommission.Application.Auth.Commands.GoogleAuth;

public record GoogleAuthCommand(
    string AccessToken,
    string? ClientIp = null
) : IRequest<(bool Success, AuthResponseDto? AuthResponse, string[] Errors)>;

public class GoogleAuthCommandHandler : IRequestHandler<GoogleAuthCommand, (bool Success, AuthResponseDto? AuthResponse, string[] Errors)>
{
    private readonly IGoogleUserInfoService _googleUserInfoService;
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public GoogleAuthCommandHandler(
        IGoogleUserInfoService googleUserInfoService,
        IIdentityService identityService,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _googleUserInfoService = googleUserInfoService;
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<(bool Success, AuthResponseDto? AuthResponse, string[] Errors)> Handle(GoogleAuthCommand request, CancellationToken cancellationToken)
    {
        var googleUser = await _googleUserInfoService.GetUserInfoAsync(request.AccessToken, cancellationToken);
        if (googleUser == null)
        {
            return (false, null, new[] { "Google sign-in failed. Please try again." });
        }

        var (success, user, roles, errors) = await _identityService.AuthenticateOrRegisterExternalAsync(
            "Google", googleUser.Sub, googleUser.Email, googleUser.EmailVerified, googleUser.Name, cancellationToken);

        if (!success || user == null)
        {
            return (false, null, errors);
        }

        var tokens = await _jwtTokenGenerator.GenerateTokensAsync(user, roles, request.ClientIp, cancellationToken);
        var authResponse = new AuthResponseDto(user, roles, tokens);

        return (true, authResponse, Array.Empty<string>());
    }
}

public class GoogleAuthCommandValidator : AbstractValidator<GoogleAuthCommand>
{
    public GoogleAuthCommandValidator()
    {
        RuleFor(x => x.AccessToken)
            .NotEmpty().WithMessage("Google access token is required.");
    }
}
