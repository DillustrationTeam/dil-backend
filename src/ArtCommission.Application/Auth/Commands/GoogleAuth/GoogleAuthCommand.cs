using ArtCommission.Application.Common.DTOs;
using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace ArtCommission.Application.Auth.Commands.GoogleAuth;

public record GoogleAuthCommand(
    string AccessToken,
    string? ClientIp = null,
    string? UserAgent = null
) : IRequest<(bool Success, AuthResponseDto? AuthResponse, bool RequiresTwoFactor, string? TwoFactorTicket, string? TwoFactorEmail, string[] Errors)>;

public class GoogleAuthCommandHandler : IRequestHandler<GoogleAuthCommand, (bool Success, AuthResponseDto? AuthResponse, bool RequiresTwoFactor, string? TwoFactorTicket, string? TwoFactorEmail, string[] Errors)>
{
    private readonly IGoogleUserInfoService _googleUserInfoService;
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IEmailVerificationService _emailVerificationService;

    public GoogleAuthCommandHandler(
        IGoogleUserInfoService googleUserInfoService,
        IIdentityService identityService,
        IJwtTokenGenerator jwtTokenGenerator,
        IEmailVerificationService emailVerificationService)
    {
        _googleUserInfoService = googleUserInfoService;
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _emailVerificationService = emailVerificationService;
    }

    public async Task<(bool Success, AuthResponseDto? AuthResponse, bool RequiresTwoFactor, string? TwoFactorTicket, string? TwoFactorEmail, string[] Errors)> Handle(GoogleAuthCommand request, CancellationToken cancellationToken)
    {
        var googleUser = await _googleUserInfoService.GetUserInfoAsync(request.AccessToken, cancellationToken);
        if (googleUser == null)
        {
            return (false, null, false, null, null, new[] { "Google sign-in failed. Please try again." });
        }

        var (success, user, roles, errors) = await _identityService.AuthenticateOrRegisterExternalAsync(
            "Google", googleUser.Sub, googleUser.Email, googleUser.EmailVerified, googleUser.Name, cancellationToken);

        if (!success || user == null)
        {
            return (false, null, false, null, null, errors);
        }

        var twoFactorEnabled = await _identityService.IsTwoFactorEnabledAsync(user.Id, cancellationToken);
        if (twoFactorEnabled)
        {
            // Google xác thực đúng danh tính nhưng tài khoản đã bật 2FA — vẫn phải qua bước /login/verify-2fa
            // trước khi phát token, không được bỏ qua chỉ vì đăng nhập qua Google.
            var ticket = _emailVerificationService.GenerateTicket(user.Email);
            return (true, null, true, ticket, user.Email, Array.Empty<string>());
        }

        var tokens = await _jwtTokenGenerator.GenerateTokensAsync(user, roles, request.ClientIp, request.UserAgent, cancellationToken);
        var authResponse = new AuthResponseDto(user, roles, tokens);

        return (true, authResponse, false, null, null, Array.Empty<string>());
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
