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
) : IRequest<(bool Success, AuthResponseDto? AuthResponse, string[] Errors)>;

public class VerifyTwoFactorLoginCommandValidator : AbstractValidator<VerifyTwoFactorLoginCommand>
{
    public VerifyTwoFactorLoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("A valid email address is required.");
        RuleFor(x => x.TwoFactorTicket).NotEmpty().WithMessage("Two-factor ticket is required.");
        RuleFor(x => x.Code).NotEmpty().WithMessage("Verification code is required.");
    }
}

public class VerifyTwoFactorLoginCommandHandler : IRequestHandler<VerifyTwoFactorLoginCommand, (bool Success, AuthResponseDto? AuthResponse, string[] Errors)>
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

    public async Task<(bool Success, AuthResponseDto? AuthResponse, string[] Errors)> Handle(VerifyTwoFactorLoginCommand request, CancellationToken cancellationToken)
    {
        var validation = new VerifyTwoFactorLoginCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        if (!_emailVerificationService.ValidateTicket(request.Email, request.TwoFactorTicket))
        {
            return (false, null, new[] { "Two-factor session expired, please log in again." });
        }

        var (findSuccess, user, roles, findErrors) = await _identityService.GetUserByEmailAsync(request.Email, cancellationToken);
        if (!findSuccess || user == null)
        {
            return (false, null, findErrors);
        }

        var (codeValid, _) = await _identityService.VerifyTwoFactorCodeAsync(user.Id, request.Code, cancellationToken);
        if (!codeValid)
        {
            return (false, null, new[] { "Invalid verification code." });
        }

        var tokens = await _jwtTokenGenerator.GenerateTokensAsync(user, roles, request.ClientIp, request.UserAgent, cancellationToken);
        var authResponse = new AuthResponseDto(user, roles, tokens);

        return (true, authResponse, Array.Empty<string>());
    }
}
