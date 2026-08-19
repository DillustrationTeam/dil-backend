using ArtCommission.Application.Common.DTOs;
using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace ArtCommission.Application.Auth.Commands.Login;

public record LoginCommand(
    string Email,
    string Password,
    string? ClientIp = null
) : IRequest<(bool Success, AuthResponseDto? AuthResponse, string[] Errors)>;

public class LoginCommandHandler : IRequestHandler<LoginCommand, (bool Success, AuthResponseDto? AuthResponse, string[] Errors)>
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public LoginCommandHandler(IIdentityService identityService, IJwtTokenGenerator jwtTokenGenerator)
    {
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<(bool Success, AuthResponseDto? AuthResponse, string[] Errors)> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var (authSuccess, user, roles, authErrors) = await _identityService.AuthenticateUserAsync(
            request.Email, request.Password, cancellationToken);

        if (!authSuccess || user == null)
        {
            return (false, null, authErrors);
        }

        var tokens = await _jwtTokenGenerator.GenerateTokensAsync(user, roles, request.ClientIp, cancellationToken);
        var authResponse = new AuthResponseDto(user, roles, tokens);

        return (true, authResponse, Array.Empty<string>());
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
