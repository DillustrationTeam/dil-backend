using ArtCommission.Application.Common.DTOs;
using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace ArtCommission.Application.Auth.Commands.VerifyTwoFactorLogin;

public record VerifyTwoFactorLoginCommand(
    string Email,
    string TwoFactorTicket,
    string Code,
    string? ClientIp = null,
    string? UserAgent = null
) : IRequest<(bool Success, AuthResponseDto? AuthResponse, bool RequiresPasswordSetup, string? PasswordSetupTicket, string? SuggestedFullName, string[] Errors)>;

public class VerifyTwoFactorLoginCommandValidator : AbstractValidator<VerifyTwoFactorLoginCommand>
{
    public VerifyTwoFactorLoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("A valid email address is required.");
        RuleFor(x => x.TwoFactorTicket).NotEmpty().WithMessage("Two-factor ticket is required.");
        RuleFor(x => x.Code).NotEmpty().WithMessage("Verification code is required.");
    }
}

public class VerifyTwoFactorLoginCommandHandler : IRequestHandler<VerifyTwoFactorLoginCommand, (bool Success, AuthResponseDto? AuthResponse, bool RequiresPasswordSetup, string? PasswordSetupTicket, string? SuggestedFullName, string[] Errors)>
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IEmailVerificationService _emailVerificationService;

    public VerifyTwoFactorLoginCommandHandler(
        IIdentityService identityService,
        IJwtTokenGenerator jwtTokenGenerator,
        IEmailVerificationService emailVerificationService)
    {
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _emailVerificationService = emailVerificationService;
    }

    public async Task<(bool Success, AuthResponseDto? AuthResponse, bool RequiresPasswordSetup, string? PasswordSetupTicket, string? SuggestedFullName, string[] Errors)> Handle(VerifyTwoFactorLoginCommand request, CancellationToken cancellationToken)
    {
        var validation = new VerifyTwoFactorLoginCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, false, null, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        if (!_emailVerificationService.ValidateTicket(request.Email, request.TwoFactorTicket))
        {
            return (false, null, false, null, null, new[] { "Two-factor session expired, please log in again." });
        }

        var (findSuccess, user, roles, findErrors) = await _identityService.GetUserByEmailAsync(request.Email, cancellationToken);
        if (!findSuccess || user == null)
        {
            return (false, null, false, null, null, findErrors);
        }

        var (codeValid, _, codeAlreadyUsed) = await _identityService.VerifyTwoFactorCodeAsync(user.Id, request.Code, cancellationToken);
        if (!codeValid)
        {
            return (false, null, false, null, null, new[] { codeAlreadyUsed ? "This recovery code has already been used." : "Invalid verification code." });
        }

        var hasPassword = await _identityService.HasPasswordAsync(user.Id, cancellationToken);
        if (!hasPassword)
        {
            // Tài khoản Google có sẵn, đã bật 2FA, nhưng chưa có mật khẩu — vẫn phải hoàn tất
            // form username + password trước khi phát token (giống luồng Google login không-2FA).
            var setupTicket = _emailVerificationService.GenerateTicket(user.Email);
            return (true, null, true, setupTicket, user.FullName, Array.Empty<string>());
        }

        var tokens = await _jwtTokenGenerator.GenerateTokensAsync(user, roles, request.ClientIp, request.UserAgent, cancellationToken);
        var authResponse = new AuthResponseDto(user, roles, tokens);

        return (true, authResponse, false, null, null, Array.Empty<string>());
    }
}
