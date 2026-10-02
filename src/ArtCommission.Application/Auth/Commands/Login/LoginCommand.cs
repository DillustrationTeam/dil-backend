using ArtCommission.Application.Common.DTOs;
using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace ArtCommission.Application.Auth.Commands.Login;

public record LoginCommand(
    string Email,
    string Password,
    string? ClientIp = null,
    string? UserAgent = null
) : IRequest<(bool Success, AuthResponseDto? AuthResponse, bool RequiresTwoFactor, string? TwoFactorTicket, string? TwoFactorEmail, string[] Errors)>;

public class LoginCommandHandler : IRequestHandler<LoginCommand, (bool Success, AuthResponseDto? AuthResponse, bool RequiresTwoFactor, string? TwoFactorTicket, string? TwoFactorEmail, string[] Errors)>
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IEmailVerificationService _emailVerificationService;

    public LoginCommandHandler(IIdentityService identityService, IJwtTokenGenerator jwtTokenGenerator, IEmailVerificationService emailVerificationService)
    {
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _emailVerificationService = emailVerificationService;
    }

    public async Task<(bool Success, AuthResponseDto? AuthResponse, bool RequiresTwoFactor, string? TwoFactorTicket, string? TwoFactorEmail, string[] Errors)> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var (authSuccess, user, roles, authErrors) = await _identityService.AuthenticateUserAsync(
            request.Email, request.Password, cancellationToken);

        if (!authSuccess || user == null)
        {
            return (false, null, false, null, null, authErrors);
        }

        var twoFactorEnabled = await _identityService.IsTwoFactorEnabledAsync(user.Id, cancellationToken);
        if (twoFactorEnabled)
        {
            // Mật khẩu đúng nhưng chưa phát token — bắt buộc xác minh mã 2FA ở bước /login/verify-2fa.
            var ticket = _emailVerificationService.GenerateTicket(user.Email);
            return (true, null, true, ticket, user.Email, Array.Empty<string>());
        }

        var tokens = await _jwtTokenGenerator.GenerateTokensAsync(user, roles, request.ClientIp, request.UserAgent, cancellationToken);
        var authResponse = new AuthResponseDto(user, roles, tokens);

        return (true, authResponse, false, null, null, Array.Empty<string>());
    }
}

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
