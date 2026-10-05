using ArtCommission.Application.Common.DTOs;
using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace ArtCommission.Application.Auth.Commands.CompleteGoogleRegistration;

/// <summary>
/// Bước 2 của đăng ký qua Google: nhận ticket đã phát ở GoogleAuthCommand (khi phát hiện tài khoản mới)
/// kèm username + password do user điền ở form, rồi mới thực sự tạo tài khoản.
/// </summary>
public record CompleteGoogleRegistrationCommand(
    string RegistrationTicket,
    string Username,
    string Password,
    string? UserAgent = null
) : IRequest<(bool Success, AuthResponseDto? AuthResponse, string[] Errors)>;

public class CompleteGoogleRegistrationCommandHandler : IRequestHandler<CompleteGoogleRegistrationCommand, (bool Success, AuthResponseDto? AuthResponse, string[] Errors)>
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IExternalRegistrationTicketService _externalRegistrationTicketService;

    public CompleteGoogleRegistrationCommandHandler(
        IIdentityService identityService,
        IJwtTokenGenerator jwtTokenGenerator,
        IExternalRegistrationTicketService externalRegistrationTicketService)
    {
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _externalRegistrationTicketService = externalRegistrationTicketService;
    }

    public async Task<(bool Success, AuthResponseDto? AuthResponse, string[] Errors)> Handle(CompleteGoogleRegistrationCommand request, CancellationToken cancellationToken)
    {
        if (!_externalRegistrationTicketService.TryValidateTicket(
            request.RegistrationTicket, out var provider, out var providerKey, out var email, out var emailVerified, out var fullName))
        {
            return (false, null, new[] { "Your Google sign-up session has expired. Please try again." });
        }

        var (registerSuccess, user, roles, registerErrors) = await _identityService.RegisterExternalUserAsync(
            provider, providerKey, email, emailVerified, fullName, request.Username, request.Password, cancellationToken);

        if (!registerSuccess || user == null)
        {
            return (false, null, registerErrors);
        }

        var tokens = await _jwtTokenGenerator.GenerateTokensAsync(user, roles, clientIp: null, request.UserAgent, cancellationToken);
        var authResponse = new AuthResponseDto(user, roles, tokens);

        return (true, authResponse, Array.Empty<string>());
    }
}

public class CompleteGoogleRegistrationCommandValidator : AbstractValidator<CompleteGoogleRegistrationCommand>
{
    public CompleteGoogleRegistrationCommandValidator()
    {
        RuleFor(x => x.RegistrationTicket)
            .NotEmpty().WithMessage("Your Google sign-up session has expired. Please try again.");

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .Length(3, 30).WithMessage("Username must be between 3 and 30 characters.")
            .Matches("^[a-zA-Z0-9_.]+$").WithMessage("Username can only contain letters, numbers, underscores and dots.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters long.");
    }
}
