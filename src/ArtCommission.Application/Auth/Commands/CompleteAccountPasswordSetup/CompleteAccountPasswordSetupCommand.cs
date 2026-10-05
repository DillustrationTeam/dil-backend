using ArtCommission.Application.Common.DTOs;
using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace ArtCommission.Application.Auth.Commands.CompleteAccountPasswordSetup;

/// <summary>
/// Hoàn tất thêm username + password cho một tài khoản ĐÃ TỒN TẠI nhưng chưa có mật khẩu
/// (vd tài khoản Google tạo trước khi tính năng bắt buộc username+password ra đời) — gọi sau khi
/// GoogleAuthCommand hoặc VerifyTwoFactorLoginCommand phát hiện HasPasswordAsync == false và trả
/// về setupTicket thay vì token. Chỉ sau khi form này submit thành công mới phát JWT.
/// </summary>
public record CompleteAccountPasswordSetupCommand(
    string Email,
    string SetupTicket,
    string Username,
    string Password,
    string? UserAgent = null
) : IRequest<(bool Success, AuthResponseDto? AuthResponse, string[] Errors)>;

public class CompleteAccountPasswordSetupCommandHandler : IRequestHandler<CompleteAccountPasswordSetupCommand, (bool Success, AuthResponseDto? AuthResponse, string[] Errors)>
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IEmailVerificationService _emailVerificationService;

    public CompleteAccountPasswordSetupCommandHandler(
        IIdentityService identityService,
        IJwtTokenGenerator jwtTokenGenerator,
        IEmailVerificationService emailVerificationService)
    {
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _emailVerificationService = emailVerificationService;
    }

    public async Task<(bool Success, AuthResponseDto? AuthResponse, string[] Errors)> Handle(CompleteAccountPasswordSetupCommand request, CancellationToken cancellationToken)
    {
        if (!_emailVerificationService.ValidateTicket(request.Email, request.SetupTicket))
        {
            return (false, null, new[] { "Your session has expired. Please log in again." });
        }

        var (findSuccess, user, roles, findErrors) = await _identityService.GetUserByEmailAsync(request.Email, cancellationToken);
        if (!findSuccess || user == null)
        {
            return (false, null, findErrors);
        }

        var (setupSuccess, setupErrors) = await _identityService.CompleteAccountSetupAsync(
            user.Id, request.Username, request.Password, cancellationToken);
        if (!setupSuccess)
        {
            return (false, null, setupErrors);
        }

        var tokens = await _jwtTokenGenerator.GenerateTokensAsync(user, roles, clientIp: null, request.UserAgent, cancellationToken);
        var authResponse = new AuthResponseDto(user, roles, tokens);

        return (true, authResponse, Array.Empty<string>());
    }
}

public class CompleteAccountPasswordSetupCommandValidator : AbstractValidator<CompleteAccountPasswordSetupCommand>
{
    public CompleteAccountPasswordSetupCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.SetupTicket)
            .NotEmpty().WithMessage("Your session has expired. Please log in again.");

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .Length(3, 30).WithMessage("Username must be between 3 and 30 characters.")
            .Matches("^[a-zA-Z0-9_.]+$").WithMessage("Username can only contain letters, numbers, underscores and dots.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters long.");
    }
}
