using ArtCommission.Application.Common.DTOs;
using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace ArtCommission.Application.Auth.Commands.GoogleAuth;

public record GoogleAuthCommand(
    string AccessToken,
    string? ClientIp = null,
    string? UserAgent = null
) : IRequest<(bool Success, AuthResponseDto? AuthResponse, bool RequiresTwoFactor, string? TwoFactorTicket, string? TwoFactorEmail, bool RequiresRegistration, string? RegistrationTicket, bool RequiresPasswordSetup, string? PasswordSetupTicket, string? SuggestedEmail, string? SuggestedFullName, string[] Errors)>;

public class GoogleAuthCommandHandler : IRequestHandler<GoogleAuthCommand, (bool Success, AuthResponseDto? AuthResponse, bool RequiresTwoFactor, string? TwoFactorTicket, string? TwoFactorEmail, bool RequiresRegistration, string? RegistrationTicket, bool RequiresPasswordSetup, string? PasswordSetupTicket, string? SuggestedEmail, string? SuggestedFullName, string[] Errors)>
{
    private readonly IGoogleUserInfoService _googleUserInfoService;
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly IExternalRegistrationTicketService _externalRegistrationTicketService;

    public GoogleAuthCommandHandler(
        IGoogleUserInfoService googleUserInfoService,
        IIdentityService identityService,
        IJwtTokenGenerator jwtTokenGenerator,
        IEmailVerificationService emailVerificationService,
        IExternalRegistrationTicketService externalRegistrationTicketService)
    {
        _googleUserInfoService = googleUserInfoService;
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _emailVerificationService = emailVerificationService;
        _externalRegistrationTicketService = externalRegistrationTicketService;
    }

    public async Task<(bool Success, AuthResponseDto? AuthResponse, bool RequiresTwoFactor, string? TwoFactorTicket, string? TwoFactorEmail, bool RequiresRegistration, string? RegistrationTicket, bool RequiresPasswordSetup, string? PasswordSetupTicket, string? SuggestedEmail, string? SuggestedFullName, string[] Errors)> Handle(GoogleAuthCommand request, CancellationToken cancellationToken)
    {
        var googleUser = await _googleUserInfoService.GetUserInfoAsync(request.AccessToken, cancellationToken);
        if (googleUser == null)
        {
            return (false, null, false, null, null, false, null, false, null, null, null, new[] { "Google sign-in failed. Please try again." });
        }

        var (success, user, roles, pendingRegistration, errors) = await _identityService.AuthenticateOrRegisterExternalAsync(
            "Google", googleUser.Sub, googleUser.Email, googleUser.EmailVerified, googleUser.Name, cancellationToken);

        if (!success)
        {
            return (false, null, false, null, null, false, null, false, null, null, null, errors);
        }

        if (pendingRegistration != null)
        {
            // Tài khoản Google hoàn toàn mới -> chưa tạo user, bắt FE hoàn tất username + password trước.
            var registrationTicket = _externalRegistrationTicketService.GenerateTicket(
                pendingRegistration.Provider, pendingRegistration.ProviderKey, pendingRegistration.Email,
                pendingRegistration.EmailVerified, pendingRegistration.FullName);

            return (true, null, false, null, null, true, registrationTicket, false, null, pendingRegistration.Email, pendingRegistration.FullName, Array.Empty<string>());
        }

        if (user == null)
        {
            return (false, null, false, null, null, false, null, false, null, null, null, errors);
        }

        var twoFactorEnabled = await _identityService.IsTwoFactorEnabledAsync(user.Id, cancellationToken);
        if (twoFactorEnabled)
        {
            // Google xác thực đúng danh tính nhưng tài khoản đã bật 2FA — vẫn phải qua bước /login/verify-2fa
            // trước khi phát token, không được bỏ qua chỉ vì đăng nhập qua Google.
            var ticket = _emailVerificationService.GenerateTicket(user.Email);
            return (true, null, true, ticket, user.Email, false, null, false, null, null, null, Array.Empty<string>());
        }

        var hasPassword = await _identityService.HasPasswordAsync(user.Id, cancellationToken);
        if (!hasPassword)
        {
            // Tài khoản Google có sẵn (vd tạo trước khi tính năng này ra đời) nhưng chưa có mật khẩu —
            // bắt FE hoàn tất form username + password trước khi phát token, chưa cho vào home.
            var setupTicket = _emailVerificationService.GenerateTicket(user.Email);
            return (true, null, false, null, null, false, null, true, setupTicket, user.Email, user.FullName, Array.Empty<string>());
        }

        var tokens = await _jwtTokenGenerator.GenerateTokensAsync(user, roles, request.ClientIp, request.UserAgent, cancellationToken);
        var authResponse = new AuthResponseDto(user, roles, tokens);

        return (true, authResponse, false, null, null, false, null, false, null, null, null, Array.Empty<string>());
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
