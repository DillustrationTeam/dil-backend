using ArtCommission.Application.Common.DTOs;
using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace ArtCommission.Application.Auth.Commands.Register;

public record RegisterCommand(
    string Email,
    string Password,
    string FullName,
    string VerificationTicket,
    string? Role = null
) : IRequest<(bool Success, AuthResponseDto? AuthResponse, string[] Errors)>;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, (bool Success, AuthResponseDto? AuthResponse, string[] Errors)>
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IEmailVerificationService _emailVerificationService;

    public RegisterCommandHandler(IIdentityService identityService, IJwtTokenGenerator jwtTokenGenerator, IEmailVerificationService emailVerificationService)
    {
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _emailVerificationService = emailVerificationService;
    }

    public async Task<(bool Success, AuthResponseDto? AuthResponse, string[] Errors)> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (!_emailVerificationService.ValidateTicket(request.Email, request.VerificationTicket))
        {
            return (false, null, new[] { "Please verify your email before registering." });
        }

        var (registerSuccess, userId, registerErrors) = await _identityService.RegisterUserAsync(
            request.Email, request.Password, request.FullName, request.Role, isVerified: true, cancellationToken: cancellationToken);

        if (!registerSuccess)
        {
            return (false, null, registerErrors);
        }

        var (authSuccess, user, roles, authErrors) = await _identityService.GetUserByIdAsync(userId, cancellationToken);
        if (!authSuccess || user == null)
        {
            return (false, null, authErrors);
        }

        var tokens = await _jwtTokenGenerator.GenerateTokensAsync(user, roles, clientIp: null, cancellationToken);
        var authResponse = new AuthResponseDto(user, roles, tokens);

        return (true, authResponse, Array.Empty<string>());
    }
}

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(150).WithMessage("Full name must not exceed 150 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters long.");

        RuleFor(x => x.VerificationTicket)
            .NotEmpty().WithMessage("Please verify your email before registering.");
    }
}
